// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    /// <summary>Maps the included rig's stable pose channels to an imported humanoid's bone axes and proportions.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class HumanoidRetargeter : MonoBehaviour
    {
        sealed class Joint { public Transform Target; public Quaternion Correction; }
        readonly Dictionary<PoseJoint,Joint> joints = new();
        AvatarPoseRig source;
        Transform frame;
        Vector3 hipsPosition;
        float heightRatio;
        public Transform Bone(PoseJoint joint) => joints.TryGetValue(joint,out var entry) ? entry.Target : null;

        public void Initialize(AvatarPoseRig canonical, Animator humanoid)
        {
            if (!humanoid || !humanoid.avatar || !humanoid.avatar.isHuman || !humanoid.avatar.isValid)
                throw new ModelImportException("Use a GLB or VRM with a valid humanoid skeleton for Maestro.");
            source = canonical; frame = canonical.transform;
            foreach (PoseJoint id in Enum.GetValues(typeof(PoseJoint)))
            {
                if (!Enum.TryParse<HumanBodyBones>(id.ToString(),out var human)) continue;
                var bone = humanoid.GetBoneTransform(human);
                if (bone && source.CanonicalBone(id)) joints.Add(id,new Joint { Target = bone });
            }
            foreach (var required in new[] { PoseJoint.Hips,PoseJoint.Spine,PoseJoint.Head,PoseJoint.LeftUpperArm,PoseJoint.LeftLowerArm,PoseJoint.LeftHand,PoseJoint.RightUpperArm,PoseJoint.RightLowerArm,PoseJoint.RightHand,PoseJoint.LeftUpperLeg,PoseJoint.LeftLowerLeg,PoseJoint.LeftFoot,PoseJoint.RightUpperLeg,PoseJoint.RightLowerLeg,PoseJoint.RightFoot })
                if (!joints.ContainsKey(required)) throw new ModelImportException("The avatar is missing a required humanoid joint: " + required);
            var alignment = new Dictionary<PoseJoint,Quaternion>();
            foreach (var pair in joints)
            {
                var id = pair.Key; var bone = pair.Value.Target;
                var turn = Quaternion.identity;
                var child = Child(id);
                if (child.HasValue && Bone(child.Value))
                {
                    var from = frame.InverseTransformDirection(Bone(child.Value).position - bone.position);
                    var to = source.BindPosition(child.Value) - source.BindPosition(id);
                    if (from.sqrMagnitude > .000001f && to.sqrMagnitude > .000001f) turn = Quaternion.FromToRotation(from,to);
                }
                else if (Parent(id) is PoseJoint parent && alignment.TryGetValue(parent,out var inherited)) turn = inherited;
                alignment[id] = turn;
                var alignedBind = turn * Quaternion.Inverse(frame.rotation) * bone.rotation;
                pair.Value.Correction = Quaternion.Inverse(source.BindRotation(id)) * alignedBind;
            }
            hipsPosition = frame.InverseTransformPoint(Bone(PoseJoint.Hips).position);
            heightRatio = Mathf.Clamp(Vector3.Distance(Bone(PoseJoint.Hips).position,Bone(PoseJoint.Head).position) / frame.lossyScale.y /
                Vector3.Distance(source.BindPosition(PoseJoint.Hips),source.BindPosition(PoseJoint.Head)),.3f,3);
            humanoid.enabled = false;
        }
        static PoseJoint? Child(PoseJoint joint) => joint switch {
            PoseJoint.Hips => PoseJoint.Spine, PoseJoint.Spine => PoseJoint.Chest, PoseJoint.Chest => PoseJoint.Neck, PoseJoint.Neck => PoseJoint.Head,
            PoseJoint.LeftUpperArm => PoseJoint.LeftLowerArm, PoseJoint.LeftLowerArm => PoseJoint.LeftHand,
            PoseJoint.RightUpperArm => PoseJoint.RightLowerArm, PoseJoint.RightLowerArm => PoseJoint.RightHand,
            PoseJoint.LeftUpperLeg => PoseJoint.LeftLowerLeg, PoseJoint.LeftLowerLeg => PoseJoint.LeftFoot,
            PoseJoint.RightUpperLeg => PoseJoint.RightLowerLeg, PoseJoint.RightLowerLeg => PoseJoint.RightFoot, _ => null };
        static PoseJoint? Parent(PoseJoint joint) => joint switch {
            PoseJoint.Head => PoseJoint.Neck, PoseJoint.LeftHand => PoseJoint.LeftLowerArm, PoseJoint.RightHand => PoseJoint.RightLowerArm,
            PoseJoint.LeftFoot => PoseJoint.LeftLowerLeg, PoseJoint.RightFoot => PoseJoint.RightLowerLeg, _ => null };
        public void ApplyPose()
        {
            if (!source || !frame) return;
            foreach (var pair in joints)
                if (pair.Value.Target) pair.Value.Target.rotation = source.CanonicalBone(pair.Key).rotation * pair.Value.Correction;
            var offset = frame.InverseTransformPoint(source.CanonicalBone(PoseJoint.Hips).position) - source.BindPosition(PoseJoint.Hips);
            Bone(PoseJoint.Hips).position = frame.TransformPoint(hipsPosition + offset * heightRatio);
        }
        public Quaternion ToCanonicalRotation(PoseJoint joint, Quaternion worldRotation) =>
            joints.TryGetValue(joint,out var entry) ? worldRotation * Quaternion.Inverse(entry.Correction) : worldRotation;
        void LateUpdate() => ApplyPose();
    }
}
