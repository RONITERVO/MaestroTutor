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
