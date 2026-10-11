// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Maestro.Quest.Book;
using AudioSettings = UnityEngine.AudioSettings;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        PcmVoice Begin(int slot, float dry = 1,
                       float wet = 1) => sink.Owner.BeginPcm(PreparedGraph.VoiceIds[slot], sink.Owner.Generation,
                                                             AudioSettings.dspTime, .02, dry, wet);
        static void Queue(PcmVoice voice, int frequency, int chunks = 20, long firstSequence = 1)
        {
            var data = new short[SpeechPcmStream.MaxChunk];
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = (short)(900 * Math.Sin(2 * Math.PI * frequency * (chunk * data.Length + i) / 24000));
                Assert.IsTrue(voice.Write(firstSequence + chunk, data, AudioSettings.dspTime, out string error), error);
            }
        }
        IEnumerator PcmReady(int slot)
        {
            var owner = sink.Owner;
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (owner.Failure == null && Time.realtimeSinceStartupAsDouble < end &&
                   (!owner.Active.PcmWetReady(slot) || owner.Updating))
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsNull(owner.Failure, owner.Failure?.ToString());
            Assert.IsTrue(owner.Active.PcmWetReady(slot), "Fresh post-reset response required");
        }
        [UnityTest]
        public IEnumerator PcmStreamUsesNativeOutputAndActualPlaybackReceipts()
        {
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var voice = Begin(0, wet: 0);
            Queue(voice, 440);
            Assert.AreEqual(96000, voice.Read(AudioSettings.dspTime + 60).Submitted);
            Assert.AreEqual(0, voice.Read(AudioSettings.dspTime + 60).Played,
                            "Clock advancement without the app render path cannot claim playback");
            Assert.IsFalse(voice.Write(20, new short[1], AudioSettings.dspTime, out _), "Duplicate packet");
            sink.BypassRendering = false;
            yield return PcmReady(0);
            yield return Measure("pcm-direct-real-output");
            Tone(440);
            var state = voice.Read(AudioSettings.dspTime);
            Assert.Greater(state.Played, 12000);
            Assert.Less(state.Played, state.Submitted);
            Assert.IsFalse(state.Faulted);
            var graph = sink.Owner.Active;
            int simulations = graph.Simulations;
            double left = MeasuredPower(440), right = Power(measuredRight, AudioSettings.outputSampleRate, 440);
            sink.Owner.UpdateListener(AcousticFrame.Pose(-1, 0, 0, (float)Math.PI));
            yield return Measure("pcm-direct-rotated");
            Tone(440);
            double turnedLeft = MeasuredPower(440),
                   turnedRight = Power(measuredRight, AudioSettings.outputSampleRate, 440);
            Assert.Less(Math.Log(left / right) * Math.Log(turnedLeft / turnedRight), 0);
            Assert.Greater(Math.Abs(Math.Log(left / right)), .1);
            Assert.AreEqual(simulations, graph.Simulations);
            Debug.Log("PCM_RECEIPTS submitted=" + state.Submitted + " played=" + state.Played);
        }
        [UnityTest]
        public IEnumerator PcmDryContinuesWhileReflectionsAwaitLatestScene()
        {
            var owner = sink.Owner;
            var old = Begin(0);
            Queue(old, 440);
            yield return PcmReady(0);
            yield return Measure("pcm-before-pending-geometry");
            Tone(440);
            preparationGate = new ManualResetEventSlim();
            using var entered = new ManualResetEventSlim();
            owner.UpdateScene(Scene(first: .5f), () =>
                                                 {
                                                     entered.Set();
                                                     if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                                         throw new TimeoutException("PCM simulation gate");
                                                 });
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsTrue(entered.IsSet);
            owner.UpdateScene(Scene(room: false));
            var next = Begin(0);
            Queue(next, 880);
            Assert.IsFalse(old.Write(21, new short[1], AudioSettings.dspTime, out _));
            yield return Measure("pcm-dry-while-wet-pending");
            Tone(880);
            Assert.IsFalse(owner.Active.PcmWetReady(0));
            Assert.Greater(next.Read(AudioSettings.dspTime).Played, 12000);
            Assert.Less(MeasuredPower(440), MeasuredPower(880) * .001);
            preparationGate.Set();
            yield return PcmReady(0);
            yield return Measure("pcm-after-response-refresh");
            Tone(880);
            Assert.IsFalse(next.Read(AudioSettings.dspTime).Faulted);
        }
        [UnityTest]
        public IEnumerator PcmReplacementClearsTailsAndKeepsOtherVoicePlaying()
        {
            var owner = sink.Owner;
            var first = Begin(0, dry: 0); // Reflections alone must recover after effect reset.
            var other = Begin(1);
            Queue(first, 440);
            Queue(other, 660, chunks: 30);
            yield return PcmReady(0);
            yield return PcmReady(1);
            yield return Measure("pcm-wet-and-independent-voice");
            Tone(440);
            Tone(660);
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], first));
            var empty = Begin(0, dry: 0);
            Queue(empty, 0);
            yield return PcmReady(0);
            yield return Measure("pcm-empty-replacement-no-old-tail");
            Tone(660);
            Assert.Less(MeasuredPower(440), MeasuredPower(660) * .001);
            var replacement = Begin(0, dry: 0);
            Queue(replacement, 880);
            Assert.IsFalse(owner.StopPcm(PreparedGraph.VoiceIds[0], first), "Late Stop cannot close a new utterance");
            Assert.IsFalse(first.Write(21, new short[1], AudioSettings.dspTime, out _));
            Assert.IsFalse(empty.Write(21, new short[1], AudioSettings.dspTime, out _));
            yield return PcmReady(0);
            yield return Measure("pcm-new-wet-response-without-old-speech");
            Tone(880);
            Tone(660);
            Assert.Less(MeasuredPower(440), MeasuredPower(880) * .001);
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], replacement));
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[1], other));
            yield return Measure("pcm-all-stopped");
            Silence();
        }
        [UnityTest]
        public IEnumerator PcmFormatResetClosesOldStreamAndResamplesNewSpeech()
        {
            var owner = sink.Owner;
            foreach (int rate in new[] { 24000, 48000 })
            {
                var old = Begin(0);
                Queue(old, 440);
                yield return PcmReady(0);
                yield return Measure("pcm-before-device-reset");
                Tone(440);
                long generation = owner.Generation;
                var format = AudioSettings.GetConfiguration();
                format.sampleRate = rate;
                format.dspBufferSize = rate == 24000 ? 256 : 512;
                Assert.IsTrue(AudioSettings.Reset(format));
                yield return Ready(owner);
                Assert.AreEqual(rate, AudioSettings.outputSampleRate);
                Assert.IsFalse(old.Open);
                Assert.IsFalse(old.Write(21, new short[1], AudioSettings.dspTime, out _));
                Assert.IsNull(owner.BeginPcm(PreparedGraph.VoiceIds[0], generation, AudioSettings.dspTime, .02));
                yield return Measure("pcm-reset-no-auto-replay");
                Silence();
                var fresh = Begin(0);
                Queue(fresh, 880);
                yield return PcmReady(0);
                yield return Measure("pcm-explicit-new-format");
                Tone(880);
                Assert.Greater(fresh.Read(AudioSettings.dspTime).Played, 12000);
                Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], fresh));
            }
        }
        [UnityTest]
        public IEnumerator PcmEmptyReplacementRejectsFirstBufferedTail()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var old = Begin(0);
            Queue(old, 440);
            yield return PcmReady(0);
            yield return Measure("pcm-history-before-empty-replacement");
            Tone(440);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], old));
            var fresh = Begin(0); // No packets: even the first native block must be silent.
            var block = new float[graph.Frame * 2];
            double time = AudioSettings.dspTime;
            double end = Time.realtimeSinceStartupAsDouble + 10;
            int blocks = 0;
            do
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                foreach (float sample in block)
                    Assert.AreEqual(0, sample, "Old direct or convolution history replayed");
                time += (double)graph.Frame / graph.Rate;
                blocks++;
                owner.Tick();
                yield return null;
            } while ((!graph.PcmWetReady(0) || owner.Updating || blocks < 4) &&
                     Time.realtimeSinceStartupAsDouble < end);
            Assert.IsTrue(graph.PcmWetReady(0));
            Assert.AreEqual(0, fresh.Read(time).Submitted);
            Assert.AreEqual(0, fresh.Read(time).Played);
            Debug.Log("PCM_EMPTY_REPLACEMENT silentBlocks=" + blocks);
            owner.StopPcm(PreparedGraph.VoiceIds[0], fresh);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmRoomEditAtOutputBoundarySuppressesOnlyWetAudio()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var wetVoice = Begin(0, dry: 0);
            Queue(wetVoice, 440);
            yield return PcmReady(0);
            yield return Measure("pcm-wet-before-boundary-edit");
            Tone(440);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var block = new float[graph.Frame * 2];
            double now = AudioSettings.dspTime;
            // The regular callback has quiesced. This invokes the same native DSP
            // on the control thread to inject an edit after processing, before mix.
            graph.BeforeOutput = () => owner.UpdateScene(Scene(room: false));
            try
            {
                Assert.IsTrue(owner.Render(block, 2, now));
            }
            finally
            {
                graph.BeforeOutput = null;
            }
            foreach (float sample in block)
                Assert.AreEqual(0, sample, "Outdated wet block must be rejected");
            var dryVoice = Begin(0);
            Queue(dryVoice, 880);
            Assert.IsTrue(owner.Render(block, 2, now + (double)graph.Frame / graph.Rate));
            double energy = 0;
            foreach (float sample in block)
                energy += sample * (double)sample;
            Assert.Greater(energy, 1e-6, "Direct speech must remain available before the room response");
            Assert.IsFalse(graph.PcmWetReady(0));
            sink.BypassRendering = false;
            yield return PcmReady(0);
        }
        [UnityTest]
        public IEnumerator PcmStopAtSubmissionDiscardsAlreadyProcessedVoice()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0);
            Queue(voice, 440);
            yield return PcmReady(0);
            yield return Measure("pcm-before-stop-at-submit");
            Tone(440);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var block = new float[graph.Frame * 2];
            graph.BeforeOutput = () => Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], voice));
            try
            {
                Assert.IsTrue(owner.Render(block, 2, AudioSettings.dspTime));
            }
            finally
            {
                graph.BeforeOutput = null;
            }
            foreach (float sample in block)
                Assert.AreEqual(0, sample, "Stopped block escaped into final output");
            Assert.IsFalse(voice.Write(21, new short[1], AudioSettings.dspTime, out _));
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmNaturalCompletionDrainsWetTailWithoutInventingPlayedSamples()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0, dry: 0);
            yield return PcmReady(0);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            Queue(voice, 440, chunks: 1);
            Assert.IsTrue(voice.Complete(1, out var completeError), completeError);
            Assert.IsFalse(voice.Write(2, new short[1], AudioSettings.dspTime, out _));
            var block = new float[graph.Frame * 2];
            double time = AudioSettings.dspTime;
            int inputBlocks = (int)Math.Ceiling(4800d * graph.Rate / 24000 / graph.Frame);
            for (int i = 0; i < inputBlocks; i++)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                time += (double)graph.Frame / graph.Rate;
            }
            double tailEnergy = 0;
            int tailBlocks = (int)Math.Ceiling(.35 * graph.Rate / graph.Frame);
            for (int i = 0; i < tailBlocks; i++)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                foreach (float value in block)
                    tailEnergy += value * (double)value;
                time += (double)graph.Frame / graph.Rate;
            }
            Assert.Greater(tailEnergy, 1e-8, "Natural completion should retain the reflected tail");
            foreach (float value in block)
                Assert.Less(Math.Abs(value), 1e-8, "Bounded tail must eventually drain");
            var receipt = voice.Read(time);
            Assert.AreEqual(4800, receipt.Submitted);
            Assert.AreEqual(4800, receipt.Played, "Reflections must not inflate the source PCM playback cursor");
            Assert.IsFalse(receipt.Faulted);
            Assert.IsTrue(receipt.InputComplete);
            Assert.IsTrue(receipt.SourceDrained);
            Assert.IsTrue(voice.Open, "Completing source input does not implicitly terminate its renderer owner");
            Debug.Log("PCM_TAIL energy=" + tailEnergy + " played=" + receipt.Played);
            owner.StopPcm(PreparedGraph.VoiceIds[0], voice);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmPausePreservesSourceWhileOtherVoiceAndSceneContinue()
        {
            var owner = sink.Owner;
            var first = Begin(0);
            var second = Begin(1);
            Queue(first, 440, chunks: 32);
            Queue(second, 660, chunks: 40);
            Assert.IsTrue(first.Complete(32, out _));
            yield return PcmReady(0);
            yield return PcmReady(1);
            yield return Measure("pcm-before-pause");
            Tone(440);
            Tone(660);
            first.SetPaused(true);
            yield return new WaitForSecondsRealtime(.1f);
            long played = first.Read(AudioSettings.dspTime).Played;
            yield return Updated(owner, owner.UpdateScene(Scene(first: 30)));
            yield return Updated(owner, owner.UpdateScene(Scene(first: .5f)));
            yield return Measure("pcm-paused-consumes-current-response");
            Tone(660);
            Assert.Less(MeasuredPower(440), MeasuredPower(660) * .001);
            var paused = first.Read(AudioSettings.dspTime);
            Assert.IsTrue(paused.Paused);
            Assert.IsTrue(paused.InputComplete);
            Assert.IsFalse(paused.SourceDrained);
            Assert.AreEqual(played, paused.Played);
            first.SetPaused(false);
            yield return Measure("pcm-resume-retains-source-and-latest-room");
            Tone(440);
            Tone(660);
            Assert.Greater(first.Read(AudioSettings.dspTime).Played, played);
            Assert.IsFalse(first.Read(AudioSettings.dspTime).Paused);
            owner.StopPcm(PreparedGraph.VoiceIds[0], first);
            owner.StopPcm(PreparedGraph.VoiceIds[1], second);
        }
        [UnityTest]
        public IEnumerator PcmPauseAfterDspKeepsAdmittedBlockAndThenFencesConsumption()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0);
            Queue(voice, 440);
            yield return PcmReady(0);
            yield return Measure("pcm-before-pause-at-submit");
            Tone(440);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var block = new float[graph.Frame * 2];
            double now = AudioSettings.dspTime;
            graph.BeforeOutput = () => voice.SetPaused(true);
            try
            {
                Assert.IsTrue(owner.Render(block, 2, now));
            }
            finally
            {
                graph.BeforeOutput = null;
            }
            double energy = 0;
            foreach (float sample in block)
                energy += sample * (double)sample;
            Assert.Greater(energy, 1e-6, "The already-admitted block must not be discarded after consuming its PCM");
            double next = now + (double)graph.Frame / graph.Rate;
            long played = voice.Read(next + .1).Played;
            Assert.IsTrue(owner.Render(block, 2, next));
            foreach (float sample in block)
                Assert.AreEqual(0, sample);
            Assert.AreEqual(played, voice.Read(next + 1).Played);
            voice.SetPaused(false);
            Assert.IsTrue(owner.Render(block, 2, next + 1));
            energy = 0;
            foreach (float sample in block)
                energy += sample * (double)sample;
            Assert.Greater(energy, 1e-6);
            owner.StopPcm(PreparedGraph.VoiceIds[0], voice);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmInvalidCallbackCannotConsumeAndCloseRejectsLateData()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            var voice = Begin(0);
            Queue(voice, 523);
            double now = AudioSettings.dspTime;
            Assert.IsFalse(owner.Render(new float[graph.Frame], 1, now));
            Assert.IsFalse(owner.Render(new float[graph.Frame], 2, now));
            Assert.AreEqual(0, voice.Read(now + 60).Played);
            sink.BypassRendering = false;
            yield return PcmReady(0);
            yield return Measure("pcm-after-invalid-callback");
            Tone(523);
            owner.Close();
            Assert.IsFalse(voice.Open);
            Assert.IsFalse(voice.Write(21, new short[1], AudioSettings.dspTime, out _));
            Assert.IsNull(owner.BeginPcm(PreparedGraph.VoiceIds[0], owner.Generation, AudioSettings.dspTime, .02));
            yield return Measure("pcm-closed-output");
            Silence();
            Assert.IsTrue(owner.Drained);
        }
    }
}
