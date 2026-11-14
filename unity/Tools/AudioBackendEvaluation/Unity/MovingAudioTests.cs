// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;

namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        static AcousticFrame Scene(bool room = true, float listener = -1, float first = 1,
                                   float second = 1) => new AcousticFrame(room, AcousticFrame.Pose(listener, 0, 0),
                                                                          AcousticFrame.Pose(first, 0, 0),
                                                                          AcousticFrame.Pose(second, 0, .4f));
        static IEnumerator Updated(OwnedLifecycle owner, long revision)
        {
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (owner.Failure == null && Time.realtimeSinceStartupAsDouble < end &&
                   (owner.Active == null || owner.Active.RenderedRevision < revision || owner.Updating))
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsNull(owner.Failure, owner.Failure?.ToString());
            Assert.IsNotNull(owner.Active);
            Assert.AreEqual(revision, owner.Active.CompletedRevision);
            Assert.GreaterOrEqual(owner.Active.RenderedRevision, revision);
            Assert.IsFalse(owner.Updating);
        }
        double MeasuredPower(int frequency) => Power(measured, AudioSettings.outputSampleRate, frequency);
        [UnityTest]
        public IEnumerator MovingEmittersAndRoomEditsUpdateWithoutVoiceRestart()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            long generation = owner.Generation;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], generation));
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[1], generation));
            yield return Measure("moving-baseline");
            Tone(440);
            Tone(660);
            double baseline = MeasuredPower(440);
            yield return Updated(owner, owner.UpdateScene(Scene(first: 30)));
            yield return Measure("first-emitter-outside");
            Tone(660);
            Assert.Less(MeasuredPower(440), baseline * .01, "Moved emitter must not retain its old room response");
            yield return Updated(owner, owner.UpdateScene(Scene(first: .4f, second: 1.5f)));
            yield return Measure("emitters-returned");
            Tone(440);
            Tone(660);
            for (int i = 0; i < 3; i++)
            {
                yield return Updated(owner, owner.UpdateScene(Scene(room: false)));
                yield return Measure("room-removed-" + i);
                Assert.Less(MeasuredPower(440) + MeasuredPower(660), baseline * .001);
                yield return Updated(owner, owner.UpdateScene(Scene()));
                yield return Measure("room-restored-" + i);
                Tone(440);
                Tone(660);
            }
            Assert.AreSame(graph, owner.Active);
            Assert.AreEqual(generation, owner.Generation);
            Assert.AreEqual(2, graph.Playing);
            Assert.AreEqual(9, graph.Simulations);
        }
        [UnityTest]
        public IEnumerator MovingListenerAndHeadRotationChangeMeasuredStereo()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            yield return Updated(owner, owner.UpdateScene(Scene(listener: -2, first: 2)));
            yield return Measure("listener-inside");
            Tone(440);
            double baseline = MeasuredPower(440);
            yield return Updated(owner, owner.UpdateListener(AcousticFrame.Pose(30, 0, 0)));
            yield return Measure("listener-outside");
            Assert.Less(MeasuredPower(440), baseline * .01);
            yield return Updated(owner, owner.UpdateScene(Scene(listener: -2, first: 2)));
            yield return Measure("listener-returned");
            Tone(440);
            int simulations = graph.Simulations;
            double left = MeasuredPower(440), right = Power(measuredRight, AudioSettings.outputSampleRate, 440);
            owner.UpdateListener(AcousticFrame.Pose(-2, 0, 0, (float)Math.PI));
            yield return Measure("head-turned");
            Tone(440);
            double turnedLeft = MeasuredPower(440),
                   turnedRight = Power(measuredRight, AudioSettings.outputSampleRate, 440);
            Debug.Log("MOVING_STEREO left=" + left + " right=" + right + " turnedLeft=" + turnedLeft +
                      " turnedRight=" + turnedRight);
            Assert.Less(Math.Log(left / right) * Math.Log(turnedLeft / turnedRight), 0,
                        "Turning around should reverse the ear-level difference");
            // Room simulation is stochastic; its absolute ear imbalance is not
            // a contract. The turned output must resemble the exchanged ears,
            // and be substantially closer to that than unchanged orientation.
            double swappedError = Math.Abs(turnedLeft - right) + Math.Abs(turnedRight - left);
            double unchangedError = Math.Abs(turnedLeft - left) + Math.Abs(turnedRight - right);
            Assert.Less(swappedError, unchangedError * .25, "Rotation must exchange the measured ear response");
            Assert.AreEqual(simulations, graph.Simulations, "Head rotation must not rebuild room responses");
            Assert.AreSame(graph, owner.Active);
        }
        [UnityTest]
        public IEnumerator SceneUpdatesCoalesceWhileAudioContinues()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            int before = graph.Simulations;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            preparationGate = new ManualResetEventSlim();
            var entered = new ManualResetEventSlim();
            owner.UpdateScene(Scene(first: .5f), () =>
                                                 {
                                                     entered.Set();
                                                     if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                                         throw new TimeoutException("Simulation completion gate");
                                                 });
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsTrue(entered.IsSet);
            yield return Measure("worker-retained-output");
            Tone(440);
            long latest = 0;
            for (int i = 0; i < 20; i++)
                latest = owner.UpdateScene(Scene(room: i == 19, first: .1f + i * .05f));
            for (int i = 0; i < 3; i++)
            {
                owner.Tick();
                yield return null;
            }
            Assert.AreEqual(before + 1, graph.Simulations, "Do not create workers for each incoming pose");
            yield return Measure("outdated-boundary-muted");
            Silence();
            preparationGate.Set();
            yield return Updated(owner, latest);
            yield return Measure("latest-scene-output");
            Tone(440);
            Assert.AreEqual(before + 2, graph.Simulations);
            Assert.AreSame(graph, owner.Active);
            entered.Dispose();
        }
        [UnityTest]
        public IEnumerator ResetDuringSimulationRejectsStaleFailureAndKeepsLatestRoom()
        {
            var owner = sink.Owner;
            var oldGraph = owner.Active;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            preparationGate = new ManualResetEventSlim();
            var entered = new ManualResetEventSlim();
            owner.UpdateScene(Scene(room: false), () =>
                                                  {
                                                      entered.Set();
                                                      if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                                          throw new TimeoutException("Reset simulation gate");
                                                      throw new InvalidOperationException("Retired simulation failure");
                                                  });
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsTrue(entered.IsSet);
            var format = AudioSettings.GetConfiguration();
            format.sampleRate = format.sampleRate == 24000 ? 48000 : 24000;
            Assert.IsTrue(AudioSettings.Reset(format));
            for (int i = 0; i < 3; i++)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsNull(owner.Active);
            Assert.IsFalse(oldGraph.Disposed, "A retained worker still owns this graph");
            preparationGate.Set();
            yield return Ready(owner);
            Assert.IsTrue(oldGraph.Disposed);
            Assert.AreEqual("Retired simulation failure", owner.RetiredFailure?.Message);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            yield return Measure("reset-does-not-resurrect-room");
            Silence();
            yield return Updated(owner, owner.UpdateScene(Scene()));
            yield return Measure("explicit-room-restoration");
            Tone(440);
            entered.Dispose();
        }
        [UnityTest]
        public IEnumerator MutedVoiceConsumesUpdatesBeforeItRestarts()
        {
            var owner = sink.Owner;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[1], owner.Generation));
            yield return Measure("both-before-mute");
            Tone(440);
            Tone(660);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[1], owner.Generation, false));
            yield return Updated(owner, owner.UpdateScene(Scene(second: 30)));
            yield return Updated(owner, owner.UpdateScene(Scene(second: .5f)));
            yield return Measure("first-continues-while-second-muted");
            Tone(440);
            Assert.Less(MeasuredPower(660), MeasuredPower(440) * .001);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[1], owner.Generation));
            yield return Measure("muted-voice-resumes-latest-response");
            Tone(440);
            Tone(660);
        }
        [UnityTest]
        public IEnumerator ProducerWaitsForAudioConsumerThenAppliesLatestScene()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            yield return Measure("before-consumer-hold");
            Tone(440);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            int before = graph.Simulations;
            long first = owner.UpdateScene(Scene(first: .4f));
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while ((owner.Updating || graph.CompletedRevision < first) && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.AreEqual(first, graph.CompletedRevision);
            Assert.Less(graph.RenderedRevision, first);
            long latest = 0;
            for (int i = 0; i < 20; i++)
                latest = owner.UpdateScene(Scene(first: .2f + i * .03f));
            for (int i = 0; i < 4; i++)
            {
                owner.Tick();
                yield return null;
            }
            Assert.AreEqual(before + 1, graph.Simulations, "One unconsumed response must stop further simulation");
            sink.BypassRendering = false;
            yield return Updated(owner, latest);
            yield return Measure("consumer-resumes-latest");
            Tone(440);
            Assert.AreEqual(before + 2, graph.Simulations);
            Assert.AreSame(graph, owner.Active);
        }
        [UnityTest]
        public IEnumerator FailedSceneUpdateRetiresOutputUntilExplicitRetry()
        {
            var owner = sink.Owner;
            var oldGraph = owner.Active;
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            owner.UpdateScene(Scene(first: .8f), () => throw new InvalidOperationException("Injected scene failure"));
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (owner.Failure == null && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.AreEqual("Injected scene failure", owner.Failure?.Message);
            Assert.IsNull(owner.Active);
            yield return Measure("failed-scene-silence");
            Silence();
            Assert.IsTrue(oldGraph.Disposed);
            var actual = AudioSettings.GetConfiguration();
            owner.Configure(actual.sampleRate, actual.dspBufferSize);
            yield return Ready(owner);
            Assert.IsTrue(owner.Play(PreparedGraph.VoiceIds[0], owner.Generation));
            yield return Measure("scene-retry-output");
            Tone(440);
        }
    }
}
