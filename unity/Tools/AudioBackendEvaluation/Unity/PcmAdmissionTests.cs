// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        static SpeechPcmStream FreshStream() => new SpeechPcmStream(AudioSettings.dspTime, .02);
        IEnumerator SetVoiceCapacity(int capacity)
        {
            var previous = sink.Owner;
            previous.Close();
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (!previous.Drained && Time.realtimeSinceStartupAsDouble < end)
            {
                previous.Tick();
                yield return null;
            }
            Assert.IsTrue(previous.Drained, "Drain the old sink before replacing its owner");
            var owner = new OwnedLifecycle(capacity);
            var poses = new SteamAudio.CoordinateSpace3[capacity];
            for (int i = 0; i < capacity; i++) poses[i] = AcousticFrame.Pose(1, 0, -.8f + .2f * i);
            owner.UpdateScene(new AcousticFrame(true, AcousticFrame.Pose(-1, 0, 0), poses));
            sink.Owner = owner;
            var format = AudioSettings.GetConfiguration();
            owner.Configure(format.sampleRate, format.dspBufferSize);
            yield return Ready(owner);
        }
        [UnityTest]
        public IEnumerator PcmExistingStreamTransferPreservesProducerAndCompletion()
        {
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var source = FreshStream();
            var samples = new short[4800];
            for (int i = 0; i < samples.Length; i++) samples[i] = (short)(900 * Math.Sin(2 * Math.PI * 440 * i / 24000));
            Assert.IsTrue(source.TryWrite(1, samples, AudioSettings.dspTime, out _));
            Assert.IsTrue(source.TryComplete(1, out _));
            var owner = sink.Owner;
            var voice = owner.BeginPcm("lesson/utterance-17", owner.Generation, source, .02);
            Assert.IsNotNull(voice);
            Assert.AreEqual(4800, voice.Read(AudioSettings.dspTime).Submitted);
            Assert.AreEqual(0, source.Read(AudioSettings.dspTime + 60).Played);
            Assert.IsTrue(voice.Read(AudioSettings.dspTime).InputComplete);
            Assert.Throws<InvalidOperationException>(() => owner.BeginPcm("another-use", owner.Generation, source, .02));
            var otherOwner = new OwnedLifecycle();
            try
            {
                Assert.Throws<InvalidOperationException>(() => otherOwner.BeginPcm("other-owner", 0, source, .02));
            }
            finally { otherOwner.Close(); }
            Assert.IsFalse(source.Read(AudioSettings.dspTime).Closed, "Reject aliases without cancelling the real owner");
            capture.Clear();
            sink.BypassRendering = false;
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (!voice.RendererDrained(AudioSettings.dspTime) && Time.realtimeSinceStartupAsDouble < end)
                yield return null;
            Assert.IsTrue(voice.RendererDrained(AudioSettings.dspTime));
            var captured = capture.Read();
            Assert.Greater(captured.blocks, 0);
            Assert.IsTrue(captured.finite);
            Assert.Greater(Energy(captured.samples), 1e-8, "The transferred queue must reach actual native output");
            Assert.AreEqual(4800, source.Read(AudioSettings.dspTime).Played);
            Assert.IsTrue(owner.FinishPcm("lesson/utterance-17", voice, AudioSettings.dspTime, out var final));
            Assert.IsTrue(final.SourceDrained);
            Assert.AreEqual(4800, final.Played);
            Assert.IsTrue(source.Read(AudioSettings.dspTime).Closed);
            Assert.Throws<InvalidOperationException>(() => owner.BeginPcm("reuse-closed", owner.Generation, source, .02));
        }
        [UnityTest]
        public IEnumerator PcmNineVoicesKeepIndependentBindingsAndResponses()
        {
            // Eight app world owners plus the independent Maestro speech owner.
            yield return SetVoiceCapacity(9);
            var owner = sink.Owner;
            var voices = new PcmVoice[9];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = owner.BeginPcm("playback/" + i, owner.Generation, FreshStream(), .02, i == 8 ? 0 : 1, 1);
                Assert.IsNotNull(voices[i]);
                Assert.AreEqual(i, voices[i].Slot);
            }
            for (int i = 0; i < voices.Length; i++) yield return PcmReady(i);
            // Keep each fundamental separate from every half/double-rate check.
            for (int i = 0; i < voices.Length; i++) Queue(voices[i], 400 + 30 * i);
            yield return Measure("nine-independent-pcm-voices");
            for (int i = 0; i < voices.Length; i++)
            {
                Tone(400 + 30 * i);
                Assert.Greater(voices[i].Read(AudioSettings.dspTime).Played, 12000);
                Assert.IsFalse(voices[i].Read(AudioSettings.dspTime).Faulted);
            }
            Assert.AreEqual(9, owner.Active.Playing);
            var overflow = FreshStream();
            Assert.IsNull(owner.BeginPcm("over-budget", owner.Generation, overflow, .02));
            Assert.IsTrue(overflow.Read(AudioSettings.dspTime).Closed);
            Assert.AreEqual(9, owner.Active.Playing, "Overflow cannot evict existing voices");
            Assert.IsTrue(owner.StopPcm("playback/8", voices[8]));
            var replacement = owner.BeginPcm("replacement/ninth", owner.Generation, FreshStream(), .02, 0, 1);
            Assert.IsNotNull(replacement);
            Assert.AreEqual(8, replacement.Slot);
            Assert.IsFalse(owner.StopPcm("playback/8", voices[8]));
            Assert.IsFalse(owner.StopPcm("replacement/ninth", voices[8]));
            Assert.IsFalse(owner.FinishPcm("replacement/ninth", voices[8], AudioSettings.dspTime + 60, out _));
            yield return PcmReady(8);
            Queue(replacement, 3000);
            yield return Measure("reused-ninth-pcm-response");
            Tone(3000);
            Tone(400);
            Assert.IsTrue(replacement.Open);
            Assert.AreEqual(9, owner.Active.Playing);
            Debug.Log("PCM_CAPACITY voices=9 highestSlot=8 overflow=rejected staleHandle=rejected");
        }
        [UnityTest]
        public IEnumerator PcmRejectedTransfersCloseOnlyTheirFreshInputs()
        {
            var owner = sink.Owner;
            var first = owner.BeginPcm("first", owner.Generation, FreshStream(), .02);
            var second = owner.BeginPcm("second", owner.Generation, FreshStream(), .02);
            foreach (var id in new[] { "third", null, " ", new string('a', 129) })
            {
                var source = FreshStream();
                Assert.IsNull(owner.BeginPcm(id, owner.Generation, source, .02));
                Assert.IsTrue(source.Read(AudioSettings.dspTime).Closed);
                Assert.IsTrue(first.Open);
                Assert.IsTrue(second.Open);
                Assert.AreEqual(2, owner.Active.Playing);
            }
            var invalid = FreshStream();
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.BeginPcm("first", owner.Generation, invalid, .02, float.NaN));
            Assert.IsTrue(invalid.Read(AudioSettings.dspTime).Closed);
            var stale = FreshStream();
            Assert.IsNull(owner.BeginPcm("first", owner.Generation - 1, stale, .02));
            Assert.IsTrue(stale.Read(AudioSettings.dspTime).Closed);
            Assert.Throws<ArgumentNullException>(() => owner.BeginPcm("first", owner.Generation, null, .02));
            var unavailable = FreshStream();
            unavailable.Close();
            Assert.Throws<InvalidOperationException>(() => owner.BeginPcm("first", owner.Generation, unavailable, .02));
            var faulted = FreshStream();
            Assert.IsFalse(faulted.Render(new float[1], 1, 0, AudioSettings.dspTime));
            Assert.Throws<InvalidOperationException>(() => owner.BeginPcm("first", owner.Generation, faulted, .02));
            Assert.IsTrue(faulted.Read(AudioSettings.dspTime).Closed);
            var unprepared = new OwnedLifecycle();
            var early = FreshStream();
            try
            {
                Assert.IsNull(unprepared.BeginPcm("early", unprepared.Generation, early, .02));
                Assert.IsTrue(early.Read(AudioSettings.dspTime).Closed);
            }
            finally { unprepared.Close(); }
            Assert.IsTrue(first.Open);
            Assert.IsTrue(second.Open);
            var next = owner.BeginPcm("first", owner.Generation, FreshStream(), .02);
            Assert.IsNotNull(next);
            Assert.AreEqual(first.Slot, next.Slot);
            Assert.IsFalse(first.Open);
            Assert.IsFalse(owner.StopPcm("first", first));
            Assert.IsTrue(next.Open);
            Assert.IsTrue(second.Open);
            owner.Close();
            var afterClose = FreshStream();
            Assert.IsNull(owner.BeginPcm("late", owner.Generation, afterClose, .02));
            Assert.IsTrue(afterClose.Read(AudioSettings.dspTime).Closed);
            yield return null;
        }
        [UnityTest]
        public IEnumerator PcmCapacitySurvivesDeviceResetWithoutReplayingNineStreams()
        {
            yield return SetVoiceCapacity(9);
            var owner = sink.Owner;
            var voices = new PcmVoice[9];
            var sources = new SpeechPcmStream[9];
            long oldGeneration = owner.Generation;
            for (int i = 0; i < voices.Length; i++)
            {
                sources[i] = FreshStream();
                voices[i] = owner.BeginPcm("world/" + i, oldGeneration, sources[i], .02);
                Queue(voices[i], 440);
            }
            var format = AudioSettings.GetConfiguration();
            Assert.IsTrue(AudioSettings.Reset(format));
            yield return Ready(owner);
            Assert.AreEqual(9, owner.VoiceCapacity);
            Assert.AreEqual(9, owner.Active.VoiceCount);
            Assert.AreEqual(9, owner.InterruptedVoices);
            Assert.AreEqual(0, owner.Active.Playing);
            foreach (var source in sources) Assert.IsTrue(source.Read(AudioSettings.dspTime).Closed);
            yield return Measure("nine-stream-reset-silence");
            Silence();
            for (int i = 0; i < voices.Length; i++)
            {
                Assert.IsFalse(owner.StopPcm("world/" + i, voices[i]));
                var late = FreshStream();
                Assert.IsNull(owner.BeginPcm("world/" + i, oldGeneration, late, .02));
                Assert.IsTrue(late.Read(AudioSettings.dspTime).Closed);
                var fresh = owner.BeginPcm("new/" + i, owner.Generation, FreshStream(), .02);
                Assert.IsNotNull(fresh);
                Assert.AreEqual(i, fresh.Slot);
            }
            Assert.AreEqual(9, owner.Active.Playing);
        }
        [UnityTest]
        public IEnumerator AcousticCapacityAndSnapshotsRejectTopologyMutation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OwnedLifecycle(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OwnedLifecycle(33));
            var poses = new[] { AcousticFrame.Pose(1, 0, 0), AcousticFrame.Pose(2, 0, 0) };
            var frame = new AcousticFrame(true, AcousticFrame.Pose(-1, 0, 0), poses);
            poses[0] = AcousticFrame.Pose(99, 0, 0);
            Assert.AreEqual(1, frame.Source(0).origin.x, "Saved frame cannot follow caller mutation");
            var moved = frame.WithListener(AcousticFrame.Pose(-2, 0, 0));
            Assert.AreEqual(1, moved.Source(0).origin.x);
            Assert.AreEqual(-1, frame.Listener.origin.x);
            var owner = sink.Owner;
            long revision = owner.RequestedSceneRevision;
            Assert.Throws<ArgumentException>(() => owner.UpdateScene(AcousticFrame.CreateDefault(9)));
            Assert.Throws<ArgumentNullException>(() => owner.UpdateScene(null));
            Assert.AreEqual(revision, owner.RequestedSceneRevision);
            Assert.IsNotNull(owner.Active);
            Assert.AreEqual(2, owner.Active.VoiceCount);
            var invalid = AcousticFrame.Pose(0, 0, 0);
            invalid.origin.x = float.NaN;
            Assert.Throws<ArgumentException>(() => new AcousticFrame(true, AcousticFrame.Pose(0, 0, 0), invalid));
            yield return null;
        }
    }
}
