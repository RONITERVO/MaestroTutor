// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>Rotation-only posing preserves bone lengths and model proportions.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class AvatarPoseRig : MonoBehaviour
    {
        readonly Dictionary<PoseJoint,Transform> bones = new();
        readonly Dictionary<PoseJoint,Quaternion> rest = new();
        readonly Dictionary<PoseJoint,Quaternion> bindRotations = new();
        readonly Dictionary<PoseJoint,Vector3> bindPositions = new();
        readonly List<JointHandle> handles = new();
        HumanoidRetargeter displayRig;
        Animator animator;
        Material paint;
        public bool IsPosing { get; private set; }
        public bool IsHolding => handles.Any(x => x && x.Item.Grab.isSelected);
        public event Action PoseChanged;

        public void Initialize(Animator source)
        {
            animator = source;
            foreach (PoseJoint joint in Enum.GetValues(typeof(PoseJoint)))
            {
                var bone = source.GetComponentsInChildren<Transform>().SingleOrDefault(x => x.name == joint.ToString());
                if (!bone) continue;
                bones.Add(joint,bone); rest.Add(joint,bone.localRotation);
                bindRotations.Add(joint,Quaternion.Inverse(transform.rotation)*bone.rotation);
                bindPositions.Add(joint,transform.InverseTransformPoint(bone.position));
            }
        }
        public JointPose[] Capture() => bones.Select(x => new JointPose { joint = x.Key, rotation = x.Value.localRotation.normalized }).ToArray();
        public void Apply(JointPose[] pose)
        {
            if (pose == null) return;
            foreach (var value in pose) if (bones.TryGetValue(value.joint,out var bone)) bone.localRotation = value.rotation;
            if (displayRig) displayRig.ApplyPose();
        }
        public void SetManual(bool manual) { if (animator) animator.enabled = !manual; }
        public void ResetPose() { foreach (var pair in bones) pair.Value.localRotation = rest[pair.Key]; PoseChanged?.Invoke(); }
        public void SetPosing(bool enabled)
        {
            IsPosing = enabled;
            if (enabled && handles.Count == 0) BuildHandles();
            foreach (var handle in handles) handle.gameObject.SetActive(enabled);
        }
        void BuildHandles()
        {
            paint = IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88"));
            var visibleBones = bones.Keys.Select(Bone).Where(value => value).ToArray();
            // One handle at the end of each limb segment; head and torso use short levers.
            foreach (var pair in bones)
            {
                if (pair.Key == PoseJoint.Hips || pair.Key == PoseJoint.Neck) continue;
                var visible = Bone(pair.Key); if (!visible) continue;
                var child = visible.GetComponentsInChildren<Transform>().FirstOrDefault(x => x != visible && visibleBones.Contains(x));
                var worldLever = child ? (child.position - visible.position) * .8f : visible.up * .12f;
                if (worldLever.magnitude < .06f) worldLever = visible.up * .12f;
                var offset = visible.InverseTransformVector(worldLever);
                var root = GameObject.CreatePrimitive(PrimitiveType.Sphere); root.name = pair.Key + " pose handle";
                root.transform.SetParent(transform,false); root.transform.localScale = Vector3.one * .042f;
                root.GetComponent<Renderer>().sharedMaterial = paint;
                var pointer = GameObject.CreatePrimitive(PrimitiveType.Cube); pointer.name = "Joint direction"; pointer.transform.SetParent(root.transform,false);
                pointer.transform.localPosition = Vector3.forward * .55f; pointer.transform.localScale = new Vector3(.2f,.2f,.65f);
                pointer.GetComponent<Collider>().enabled = false; ArtResources.Release(pointer.GetComponent<Collider>()); pointer.GetComponent<Renderer>().sharedMaterial = paint;
                var item = root.AddComponent<RoomItem>(); item.Configure(new[] { root.GetComponent<Collider>() },1,1);
                item.Grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;
                var handle = root.AddComponent<JointHandle>();
                handle.Initialize(this,item,pair.Key,visible,offset); handles.Add(handle);
            }
        }
        public void Rotate(PoseJoint joint, Quaternion worldRotation)
        {
            if (!bones.TryGetValue(joint,out var bone)) return;
            if (displayRig) worldRotation = displayRig.ToCanonicalRotation(joint,worldRotation);
            var local = Quaternion.Inverse(bone.parent.rotation) * worldRotation;
            float limit = joint == PoseJoint.Head ? 75 : joint == PoseJoint.Spine || joint == PoseJoint.Chest ? 45 : 150;
            bone.localRotation = Quaternion.RotateTowards(rest[joint],local,limit).normalized;
            if (displayRig) displayRig.ApplyPose();
        }
        public void FinishedHandle() => PoseChanged?.Invoke();
        public Transform Bone(PoseJoint joint) => displayRig ? displayRig.Bone(joint) : CanonicalBone(joint);
        public Transform CanonicalBone(PoseJoint joint) => bones.TryGetValue(joint,out var value) ? value : null;
        public Quaternion BindRotation(PoseJoint joint) => bindRotations[joint];
        public Vector3 BindPosition(PoseJoint joint) => bindPositions[joint];
        public void SetDisplayRig(HumanoidRetargeter value)
        {
            SetPosing(false);
            foreach (var handle in handles) if (handle) { handle.gameObject.SetActive(false); ArtResources.Release(handle.gameObject); }
            handles.Clear(); ArtResources.Release(paint); paint = null; displayRig = value;
        }
        void OnDestroy() { foreach (var handle in handles) if (handle) ArtResources.Release(handle.gameObject); ArtResources.Release(paint); }
    }

    [DefaultExecutionOrder(200)]
    public sealed class JointHandle : MonoBehaviour
    {
        AvatarPoseRig rig;
        PoseJoint joint;
        Transform bone;
        Vector3 offset, startDirection;
        Quaternion startRotation, startHandleRotation;
        bool held;
        public RoomItem Item { get; private set; }
        public void Initialize(AvatarPoseRig owner, RoomItem item, PoseJoint id, Transform target, Vector3 lever)
        {
            rig = owner; Item = item; joint = id; bone = target; offset = lever;
            Follow(); item.GrabStarted += Begin; item.GrabFinished += End;
        }
        void Begin(RoomItem _) { startDirection = transform.position - bone.position; startRotation = bone.rotation; startHandleRotation = transform.rotation; held = true; }
        void End(RoomItem _) { held = false; if (rig) rig.FinishedHandle(); }
        void LateUpdate()
        {
            if (!held) { Follow(); return; }
            var direction = transform.position - bone.position;
            if (direction.magnitude > .7f || direction.magnitude < .025f) return;
            var swing = Quaternion.FromToRotation(startDirection,direction);
            var residual = transform.rotation * Quaternion.Inverse(startHandleRotation) * Quaternion.Inverse(swing);
            var axis = direction.normalized;
            var vector = Vector3.Project(new Vector3(residual.x,residual.y,residual.z),axis);
            var twist = new Quaternion(vector.x,vector.y,vector.z,residual.w);
            twist = Quaternion.Dot(twist,twist) > .0001f ? twist.normalized : Quaternion.identity;
            rig.Rotate(joint,twist * swing * startRotation);
        }
        void Follow() { if (bone) transform.SetPositionAndRotation(bone.TransformPoint(offset),bone.rotation); }
        void OnDestroy() { if (Item) { Item.GrabStarted -= Begin; Item.GrabFinished -= End; } }
    }
}
