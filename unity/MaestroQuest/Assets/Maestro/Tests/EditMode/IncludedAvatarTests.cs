// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public class IncludedAvatarTests
    {
        [Test] public void WalkLoopsAndMovesBothLegsWithoutTranslatingTheRoot()
        {
            const string path = "Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(value => value.name=="Walk");
            Assert.That(clip.isLooping,Is.True);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var bones = model.GetComponentsInChildren<Transform>(); var left = bones.Single(bone => bone.name=="LeftUpperLeg"); var right = bones.Single(bone => bone.name=="RightUpperLeg");
                clip.SampleAnimation(model,clip.length*.25f); var a = left.localRotation; var b = right.localRotation; var position = model.transform.localPosition;
                clip.SampleAnimation(model,clip.length*.75f);
                Assert.That(Quaternion.Angle(a,left.localRotation),Is.GreaterThan(10)); Assert.That(Quaternion.Angle(b,right.localRotation),Is.GreaterThan(10));
                Assert.That(model.transform.localPosition,Is.EqualTo(position));
            }
            finally { Object.DestroyImmediate(model); }
        }
        [Test]
        public void IncludedGestureClipsActuallyMoveTheImportedSkeleton()
        {
            const string path = "Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            foreach (var name in new[] { "Greeting", "Listening", "Speaking", "Pointing" })
            {
                var clip = clips.Single(value => value.name == name);
                Assert.Greater(clip.length, 1);
                var model = Object.Instantiate(prefab);
                try
                {
                    var bones = model.GetComponentsInChildren<Transform>();
                    var before = bones.Select(bone => bone.localRotation).ToArray();
                    clip.SampleAnimation(model, clip.length * .47f);
                    float movement = bones.Select((bone,index) => Quaternion.Angle(before[index], bone.localRotation)).Sum();
                    Assert.Greater(movement, 1, name + " must change the skeleton's pose.");
                }
                finally { Object.DestroyImmediate(model); }
            }
        }
    }
}
