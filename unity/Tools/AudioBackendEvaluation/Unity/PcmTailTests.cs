// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        IEnumerator ManualFiniteInput(PcmVoice voice)
        {
            yield return PcmReady(0);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            Queue(voice, 440, chunks: 1);
            Assert.IsTrue(voice.Complete(1, out var error), error);
        }
        static double SubmitFiniteSource(OwnedLifecycle owner, PreparedGraph graph, PcmVoice voice, float[] block,
                                         double time)
        {
            int limit = (int)Math.Ceiling(graph.Rate / (double)graph.Frame);
            for (int i = 0; i < limit && !voice.Read(time).SourceDrained; i++)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                time += (double)graph.Frame / graph.Rate;
            }
            Assert.IsTrue(voice.Read(time).SourceDrained);
            Assert.IsFalse(voice.RendererDrained(time + 60), "Source playback cannot invent native tail completion");
            return time;
        }
        static double Energy(float[] values)
        {
            double sum = 0;
            foreach (float value in values)
                sum += value * (double)value;
            return sum;
        }
        [UnityTest]
        public IEnumerator PcmRendererCompletionWaitsForFinalSubmissionAndOutputClock()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0, dry: 0);
            yield return ManualFiniteInput(voice);
            double time = AudioSettings.dspTime;
            Assert.AreEqual(0, voice.Read(time + 60).Played);
            Assert.IsFalse(voice.RendererDrained(time + 60));
            var block = new float[graph.Frame * 2];
            time = SubmitFiniteSource(owner, graph, voice, block, time);
            double energy = 0;
            int count = 0;
            graph.BeforeOutput = () =>
                Assert.IsFalse(voice.RendererDrained(time + 60), "Completion escaped before final output submission");
            try
            {
                while (!voice.RendererDrained(time + 60) && count++ < 100)
                {
                    Assert.IsTrue(owner.Render(block, 2, time));
                    energy += Energy(block);
                    time += (double)graph.Frame / graph.Rate;
                }
            }
            finally
            {
                graph.BeforeOutput = null;
            }
            Assert.Less(count, 100, "Renderer never acknowledged its finite native tail");
            Assert.Greater(energy, 1e-8, "Natural reflected tail was discarded");
            Assert.IsFalse(voice.RendererDrained(time), "The final block still has output latency");
            Assert.IsTrue(voice.RendererDrained(time + .1));
            Assert.IsFalse(voice.RendererDrained(double.PositiveInfinity));
            Assert.AreEqual(4800, voice.Read(time + .1).Played);
            Assert.IsTrue(voice.Open, "The owner must be able to inspect final receipts before releasing the handle");
            Assert.IsFalse(owner.FinishPcm(PreparedGraph.VoiceIds[0], voice, time, out _));
            Assert.IsTrue(owner.FinishPcm(PreparedGraph.VoiceIds[0], voice, time + .1, out var final));
            Assert.AreEqual(4800, final.Played);
            Assert.IsTrue(final.SourceDrained);
            Assert.IsFalse(voice.Open);
            Assert.IsFalse(owner.FinishPcm(PreparedGraph.VoiceIds[0], voice, time + 1, out _));
            Assert.IsFalse(voice.RendererDrained(time + 60), "A released handle cannot keep claiming ownership");
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmFinishedVoiceConsumesFutureResponsesWithoutReplayOrBlockingOtherVoice()
        {
            var owner = sink.Owner;
            var first = Begin(0);
            var second = Begin(1);
            Queue(first, 440, chunks: 1);
            Queue(second, 660, chunks: 40);
            Assert.IsTrue(first.Complete(1, out _));
            yield return PcmReady(0);
            yield return PcmReady(1);
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!first.RendererDrained(AudioSettings.dspTime) && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsTrue(first.RendererDrained(AudioSettings.dspTime));
            foreach (float position in new[] { 30f, .5f })
            {
                yield return Updated(owner, owner.UpdateScene(Scene(first: position)));
                yield return Measure("completed-voice-keeps-consuming-response");
                Tone(660);
                Assert.Less(MeasuredPower(440), MeasuredPower(660) * .001);
                Assert.IsTrue(first.RendererDrained(AudioSettings.dspTime));
                Assert.AreEqual(4800, first.Read(AudioSettings.dspTime).Played);
            }
            Assert.IsTrue(owner.FinishPcm(PreparedGraph.VoiceIds[0], first, AudioSettings.dspTime, out var final));
            Assert.AreEqual(4800, final.Played);
            yield return Measure("finished-owner-released-other-voice-continues");
            Tone(660);
            Assert.IsTrue(second.Open);
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[1], second));
        }
        [UnityTest]
        public IEnumerator PcmTailPauseAndReplacementCannotCompleteAnOpenEmptyStream()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var old = Begin(0);
            yield return ManualFiniteInput(old);
            var block = new float[graph.Frame * 2];
            double time = SubmitFiniteSource(owner, graph, old, block, AudioSettings.dspTime);
            old.SetPaused(true);
            Assert.IsTrue(owner.Render(block, 2, time));
            Assert.AreEqual(0, Energy(block));
            Assert.AreEqual(4800, old.Read(time + 1).Played);
            time += (double)graph.Frame / graph.Rate;
            var fresh = Begin(0);
            Assert.IsFalse(old.Open);
            Assert.IsFalse(old.RendererDrained(time + 60));
            Assert.IsFalse(owner.FinishPcm(PreparedGraph.VoiceIds[0], old, time + 60, out _));
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                Assert.AreEqual(0, Energy(block));
                time += (double)graph.Frame / graph.Rate;
                Assert.IsFalse(fresh.RendererDrained(time + 60), "A network underrun is not end of input");
            }
            Assert.IsTrue(fresh.Complete(0, out _));
            int count = 0;
            while (!fresh.RendererDrained(time + 1) && count++ < 100)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                Assert.AreEqual(0, Energy(block));
                time += (double)graph.Frame / graph.Rate;
            }
            Assert.Less(count, 100);
            Assert.AreEqual(0, fresh.Read(time + 1).Played);
            Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], fresh));
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmDeviceResetDuringTailInvalidatesOldCompletion()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var old = Begin(0);
            yield return ManualFiniteInput(old);
            var block = new float[graph.Frame * 2];
            SubmitFiniteSource(owner, graph, old, block, AudioSettings.dspTime);
            var format = AudioSettings.GetConfiguration();
            format.sampleRate = 24000;
            format.dspBufferSize = 256;
            Assert.IsTrue(AudioSettings.Reset(format));
            yield return Ready(owner);
            Assert.IsFalse(old.Open);
            Assert.IsFalse(old.RendererDrained(AudioSettings.dspTime + 60));
            Assert.IsFalse(old.Write(2, new short[1], AudioSettings.dspTime, out _));
            var empty = Begin(0);
            Assert.IsTrue(empty.Complete(0, out _));
            graph = owner.Active;
            block = new float[graph.Frame * 2];
            Assert.IsTrue(owner.Render(block, 2, AudioSettings.dspTime));
            Assert.AreEqual(0, Energy(block));
            Assert.IsTrue(empty.RendererDrained(AudioSettings.dspTime + 1));
            owner.StopPcm(PreparedGraph.VoiceIds[0], empty);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmRoomReplacementCannotReviveObsoleteTailGeometry()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0, dry: 0);
            yield return ManualFiniteInput(voice);
            var block = new float[graph.Frame * 2];
            double time = SubmitFiniteSource(owner, graph, voice, block, AudioSettings.dspTime);
            Assert.IsTrue(owner.Render(block, 2, time));
            Assert.Greater(Energy(block), 1e-8);
            time += (double)graph.Frame / graph.Rate;
            long revision = owner.UpdateScene(Scene(room: false));
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while ((graph.CompletedRevision < revision || owner.Updating) && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.GreaterOrEqual(graph.CompletedRevision, revision);
            Assert.Less(graph.RenderedRevision, revision,
                        "Tail-only callbacks cannot acknowledge a newly published IR");
            for (int i = 0; i < 40; i++)
            {
                Assert.IsTrue(owner.Render(block, 2, time));
                Assert.AreEqual(0, Energy(block), "Old-room tail reappeared after the new simulation");
                time += (double)graph.Frame / graph.Rate;
            }
            Assert.IsTrue(voice.RendererDrained(time + .1));
            Assert.GreaterOrEqual(graph.RenderedRevision, revision, "Drained voices must resume silent IR consumption");
            owner.StopPcm(PreparedGraph.VoiceIds[0], voice);
            sink.BypassRendering = false;
        }
    }
}
