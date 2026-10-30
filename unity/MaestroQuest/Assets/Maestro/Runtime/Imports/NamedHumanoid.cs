// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using UniGLTF;
using UnityEngine;

namespace Maestro.Quest.Imports
{
    /// <summary>Recognizes explicit Mixamo/Unity bone names on a skinned hierarchy, never by file brand.</summary>
    public static class NamedHumanoid
    {
        static readonly HumanBodyBones[] required = {
            HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot
        };
        static readonly Dictionary<string,HumanBodyBones> names = Names();
        static Dictionary<string,HumanBodyBones> Names()
        {
            var result = new Dictionary<string,HumanBodyBones>(StringComparer.OrdinalIgnoreCase);
            foreach (var bone in required) result.Add(bone.ToString(),bone);
            foreach (var bone in new[] { HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.LeftShoulder,HumanBodyBones.RightShoulder,HumanBodyBones.LeftToes,HumanBodyBones.RightToes }) result.Add(bone.ToString(),bone);
            result.Add("Spine1",HumanBodyBones.Chest); result.Add("Spine2",HumanBodyBones.UpperChest);
            foreach (string side in new[] { "Left","Right" })
                foreach (var pair in new[] { ("Arm","UpperArm"),("ForeArm","LowerArm"),("UpLeg","UpperLeg"),("Leg","LowerLeg"),("ToeBase","Toes") })
                    result.Add(side+pair.Item1,Enum.Parse<HumanBodyBones>(side+pair.Item2));
            return result;
        }
        static string Key(string name)
        {
            int separator = name.LastIndexOf(':'); if (separator >= 0) name = name.Substring(separator+1);
            if (name.StartsWith("mixamorig",StringComparison.OrdinalIgnoreCase)) name = name.Substring(9).TrimStart('0','1','2','3','4','5','6','7','8','9','_');
            return name;
        }
        public static UnityEngine.Avatar TryCreate(RuntimeGltfInstance instance, out string issue)
        {
            issue = "Choose a skinned GLB with Mixamo/Unity humanoid bone names, or a VRM";
            var skeleton = new HashSet<Transform>();
            foreach (var skin in instance.SkinnedMeshRenderers)
                foreach (var bone in skin.bones)
                    for (var node = bone; node && node != instance.transform && node.IsChildOf(instance.transform); node = node.parent) skeleton.Add(node);
            if (skeleton.Count == 0) return null;
            var map = new Dictionary<HumanBodyBones,Transform>();
            foreach (var bone in skeleton)
            {
                if (!names.TryGetValue(Key(bone.name),out var human)) continue;
                if (map.ContainsKey(human)) { issue = "The model has more than one bone named for " + human + ". Export a single humanoid skeleton."; return null; }
                map.Add(human,bone);
            }
            foreach (var bone in required)
                if (!map.ContainsKey(bone)) { issue = "Humanoid mapping needs a named " + bone + " bone. Export with the Mixamo skeleton or use VRM."; return null; }
            bool Chain(params HumanBodyBones[] chain)
            {
                Transform previous = null;
                foreach (var id in chain)
                {
                    if (!map.TryGetValue(id,out var node)) continue;
                    if (previous && (!node.IsChildOf(previous) || Vector3.Distance(node.position,previous.position) < .000001f)) return false;
                    previous = node;
                }
                return true;
            }
            if (!Chain(HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.Head) ||
                !Chain(HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.LeftShoulder,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand) ||
                !Chain(HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.RightShoulder,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand) ||
                !Chain(HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes) ||
                !Chain(HumanBodyBones.Hips,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes))
            { issue = "The humanoid bone hierarchy or limb lengths are invalid. Export the character in its rest pose."; return null; }
            // AvatarBuilder resolves names, not Transform references. Refuse ambiguous names.
            var nodes = instance.GetComponentsInChildren<Transform>(true);
            if (nodes.GroupBy(node => node.name).Any(group => group.Count() > 1)) { issue = "The humanoid hierarchy needs unique bone and object names."; return null; }
            UnityEngine.Avatar avatar = null;
            try
            {
                var traitNames = HumanTrait.BoneName.ToDictionary(name => Enum.Parse<HumanBodyBones>(name.Replace(" ","")),name => name);
                avatar = AvatarBuilder.BuildHumanAvatar(instance.gameObject,new HumanDescription {
                    human = map.Select(pair => new HumanBone { boneName = pair.Value.name, humanName = traitNames[pair.Key], limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                    skeleton = nodes.Select(node => new SkeletonBone { name = node.name, position = node.localPosition, rotation = node.localRotation, scale = node.localScale }).ToArray(),
                    armStretch = .05f, legStretch = .05f, upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f
                });
                if (!avatar || !avatar.isValid || !avatar.isHuman) { ArtResources.Release(avatar); issue = "Unity could not validate this humanoid. Export a T-pose Mixamo rig or use VRM."; return null; }
                avatar.name = "Imported named humanoid";
                var animator = instance.gameObject.AddComponent<Animator>(); animator.enabled = false; animator.avatar = avatar;
                issue = null; return avatar;
            }
            catch (Exception) { ArtResources.Release(avatar); issue = "This GLB can be a room object, but its humanoid rig could not be mapped."; return null; }
        }
    }
}
