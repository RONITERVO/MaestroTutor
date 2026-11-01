// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class BundledAvatarRuntimeTests
    {
        [UnityTest] public IEnumerator SpeechEmitterKeepsItsOwnerAndUsesTheVisibleHeadAfterAnAvatarReplacement()
        {
            var included = BundledAvatarFixture.Write(Path.Combine(directory, "speech-avatar"), ModelFixture.Mixamo());
            Open(included); yield return Loaded();
            var voice = avatar.SpeechOutput; var firstHead = avatar.PoseRig.Bone(PoseJoint.Head);
            Assert.AreNotSame(avatar.PoseRig.CanonicalBone(PoseJoint.Head), firstHead);
            var offset = new Vector3(.01f, .04f, .06f);
            voice.ConfigureAnchor(avatar.PoseRig, avatar.transform, offset, Vector3.up);
            voice.FollowAnchor(); var original = voice.transform.position;
            avatar.transform.position += new Vector3(1, 2, 3);
            voice.FollowAnchor();
            Assert.That(Vector3.Distance(voice.transform.position, original + new Vector3(1, 2, 3)), Is.LessThan(.0001f));
            var custom = ModelLibrary.Inspect("Replacement voice model", ModelFixture.Create(avatar: true));
            var saved = editor.Models.SaveAsync(custom); yield return new WaitUntil(() => saved.IsCompleted);
            Assert.That(saved.Exception, Is.Null); Assert.IsTrue(editor.SetMaestroModel(custom.Hash)); yield return Loaded();
            Assert.AreSame(voice, avatar.SpeechOutput);
            var visible = avatar.PoseRig.Bone(PoseJoint.Head); Assert.AreNotSame(firstHead, visible);
            voice.FollowAnchor();
            var expected = visible.position + avatar.PoseRig.CanonicalBone(PoseJoint.Head).rotation * Vector3.Scale(avatar.transform.lossyScale, offset);
            Assert.That(Vector3.Distance(expected, voice.transform.position), Is.LessThan(.0001f));
        }
    }
}
