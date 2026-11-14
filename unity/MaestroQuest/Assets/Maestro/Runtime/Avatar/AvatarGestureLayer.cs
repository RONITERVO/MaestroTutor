// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using System.Linq;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>A sampled upper-body overlay. It never writes root, hips, legs, neck or head.</summary>
    sealed class AvatarGestureLayer : IDisposable
    {
        sealed class Joint { public Transform Target, Sample; public Quaternion Base; }
        readonly GameObject sampler;
        readonly Joint[] joints;
        readonly AnimationClip[] clips;
        AnimationClip clip;
        float time;
        bool applied;
        public string Owner { get; private set; }
        public bool Active => Owner != null;
        public AvatarGestureLayer(GameObject included, Animator animator, AvatarPoseRig rig)
        {
            Transform Copy(Transform source,Transform parent)
            {
                var copy=new GameObject(source.name).transform;copy.SetParent(parent,false);
                copy.SetLocalPositionAndRotation(source.localPosition,source.localRotation);copy.localScale=source.localScale;
                foreach(Transform child in source)Copy(child,copy);
                return copy;
            }
            sampler=Copy(included.transform,included.transform.parent).gameObject;
            sampler.name="Gesture sampling skeleton";
            // Generic FBX clips need an Animator binding even for manual sampling.
            // This hidden rig has no renderers or scripts and never updates itself.
            var binding=sampler.AddComponent<Animator>();binding.avatar=animator.avatar;
            binding.applyRootMotion=false;binding.enabled=false;
            clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            var names=new[] {PoseJoint.Spine,PoseJoint.Chest,PoseJoint.LeftUpperArm,PoseJoint.LeftLowerArm,PoseJoint.LeftHand,
                PoseJoint.RightUpperArm,PoseJoint.RightLowerArm,PoseJoint.RightHand};
            joints=names.Select(id=>new Joint {Target=rig.CanonicalBone(id),
                Sample=sampler.GetComponentsInChildren<Transform>().Single(x=>x.name==id.ToString())}).ToArray();
        }
        public bool Begin(string owner,string name)
        {
            if(string.IsNullOrEmpty(owner)||Active||name=="Walk")return false;
            var selected=clips.FirstOrDefault(x=>x.name.Split('|').Last()==name);
            if(!selected||selected.length<=0)return false;
            Owner=owner;clip=selected;time=0;return true;
        }
        // Runs before the next base animation evaluation. A frozen authored pose
        // must not accumulate the overlay into its own input on successive frames.
        public void RestoreBase()
        {
            if(!applied)return;
            foreach(var joint in joints)if(joint.Target)joint.Target.localRotation=joint.Base;
            applied=false;
        }
        public void Apply(float delta,bool reducedMotion)
        {
            if(!Active)return;
            time+=Mathf.Min(Mathf.Max(0,delta),.05f);
            float sample=clip.isLooping?Mathf.Repeat(time,clip.length):Mathf.Min(time,clip.length);
            clip.SampleAnimation(sampler,sample);
            float weight=reducedMotion?1:Mathf.Clamp01(time/.15f);
            foreach(var joint in joints)
            {
                joint.Base=joint.Target.localRotation;
                joint.Target.localRotation=Quaternion.Slerp(joint.Base,joint.Sample.localRotation,weight);
            }
            applied=true;
        }
        public void End(string owner) {if(Owner==owner)Stop();}
        public void Stop() {RestoreBase();Owner=null;clip=null;}
        public void Dispose() {Stop();if(sampler)UnityEngine.Object.Destroy(sampler);}
    }
}
