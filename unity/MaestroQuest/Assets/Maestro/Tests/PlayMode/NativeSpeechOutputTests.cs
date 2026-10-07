// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class NativeSpeechOutputTests
    {
        GameObject root;
        NativeSpeechOutput output;
        double clock;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Speech output test"); output = root.AddComponent<NativeSpeechOutput>();
            clock = AudioSettings.dspTime + 5; output.Clock = () => clock; output.TailOverride = .05;
        }
        [UnityTearDown] public IEnumerator TearDown() { if (root) UnityEngine.Object.Destroy(root); yield return null; }
        void Write(long generation, long sequence, short[] pcm)
        {
            Assert.IsTrue(output.TryWrite(generation, sequence, pcm, out var error), error);
            // These verify native source/clip scheduling, not audible-device acceptance.
            foreach (var source in root.GetComponentsInChildren<AudioSource>()) source.volume = 0;
        }
        [UnityTest] public IEnumerator PcmIsMonoCopiedAndCompletionIncludesTheDspTail()
        {
            var generation = output.Begin(24000);
            var pcm = new short[2400]; pcm[0] = short.MinValue; pcm[1] = short.MaxValue;
            Write(generation, 1, pcm); pcm[0] = 0;
            var source = root.GetComponentInChildren<AudioSource>();
            Assert.AreEqual(1, source.clip.channels); Assert.AreEqual(24000, source.clip.frequency);
            Assert.AreEqual(1, source.spatialBlend); Assert.AreEqual(0, source.dopplerLevel);
            var samples = new float[2400]; Assert.IsTrue(source.clip.GetData(samples, 0));
            Assert.AreEqual(-1f, samples[0]); Assert.AreEqual(32767f / 32768f, samples[1]);
            Assert.AreEqual(0, output.Read().playedSamples);
            clock += .16; Assert.That(output.Read().playedSamples, Is.InRange(239, 241));
            clock += .08; Assert.Less(output.Read().playedSamples, 2400);
            clock += .011; Assert.AreEqual(2400, output.Read().playedSamples);
            yield return null; Assert.IsTrue(source == null);
        }
        [UnityTest] public IEnumerator DuplicateOutOfOrderStoppedAndDisabledPacketsNeverPlay()
        {
            var generation = output.Begin(24000); Write(generation, 1, new short[2400]);
            Assert.IsFalse(output.TryWrite(generation, 1, new short[2400], out _));
            Assert.IsFalse(output.TryWrite(generation, 3, new short[2400], out _));
            Assert.AreEqual(2400, output.Read().submittedSamples);
            output.Stop(); Assert.IsFalse(output.TryWrite(generation, 2, new short[2400], out _));
            var newer = output.Begin(24000); Assert.AreNotEqual(generation, newer);
            Assert.IsFalse(output.TryWrite(generation, 1, new short[2400], out _));
            Write(newer, 1, new short[2400]); root.SendMessage("OnApplicationFocus", false);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            Assert.Throws<InvalidOperationException>(() => output.Begin(24000));
            root.SendMessage("OnApplicationFocus", true);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            newer = output.Begin(24000); Write(newer, 1, new short[2400]);
            root.SendMessage("AudioConfigurationChanged", true);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            newer = output.Begin(24000); Write(newer, 1, new short[2400]); root.SetActive(false);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            Assert.Throws<InvalidOperationException>(() => output.Begin(24000));
            yield return null; Assert.AreEqual(0, root.GetComponentsInChildren<AudioSource>(true).Length);
        }
        [UnityTest] public IEnumerator QueueBoundsRejectBeforeAllocationAndLateAudioCountsNoGapSamples()
        {
            var generation = output.Begin(24000);
            Assert.Throws<ArgumentException>(() => output.Begin(48000));
            Assert.IsFalse(output.TryWrite(generation, 1, new short[4801], out _));
            for (int i = 1; i <= 40; i++) Write(generation, i, new short[4800]);
            Assert.IsFalse(output.TryWrite(generation, 41, new short[1], out var error));
            Assert.That(error, Does.Contain("full"));
            Assert.AreEqual(40, root.GetComponentsInChildren<AudioSource>().Length);
            clock += 12; Assert.AreEqual(192000, output.Read().playedSamples);
            Write(generation, 41, new short[2400]); Assert.AreEqual(192000, output.Read().playedSamples);
            clock += .26; Assert.AreEqual(194400, output.Read().playedSamples);
            yield return null;
        }
        [UnityTest] public IEnumerator AvatarOwnsTheEmitterAndFollowsPosedHeadAndRootScale()
        {
            var avatar = root.AddComponent<MaestroAvatar>(); var voice = avatar.SpeechOutput;
            Assert.IsNotNull(voice); Assert.IsNotNull(avatar.PoseRig.Bone(PoseJoint.Head));
            var head = avatar.PoseRig.Bone(PoseJoint.Head);
            root.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 60, 0));
            root.transform.localScale = Vector3.one * 1.5f;
            head.localRotation *= Quaternion.Euler(0, 25, 0);
            voice.FollowAnchor();
            Assert.That(Vector3.Distance(head.position + head.rotation * new Vector3(0, .12f, .12f), voice.transform.position), Is.LessThan(.0001f));
            voice.ConfigureAnchor(null, root.transform, Vector3.zero, new Vector3(0, 1, 0));
            Assert.That(Vector3.Distance(root.transform.TransformPoint(Vector3.up), voice.transform.position), Is.LessThan(.0001f));
            yield return null;
        }
    }
}
