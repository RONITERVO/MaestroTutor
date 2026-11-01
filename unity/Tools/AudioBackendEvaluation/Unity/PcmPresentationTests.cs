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
        double StereoPower(int frequency) => Power(measured, AudioSettings.outputSampleRate, frequency) +
            Power(measuredRight, AudioSettings.outputSampleRate, frequency);
        static AudioClip ReferenceTone()
        {
            var clip = AudioClip.Create("Unity reference PCM", 4800, 1, 24000, false);
            var data = new float[4800];
            for (int i = 0; i < data.Length; i++) data[i] = (short)(900 * Math.Sin(2 * Math.PI * 440 * i / 24000)) / 32768f;
            Assert.IsTrue(clip.SetData(data, 0));
            return clip;
        }
        static void ConfigureReference(AudioSource source, AudioClip clip, bool spatial)
        {
            source.playOnAwake = false; source.loop = true; source.clip = clip;
            source.volume = 1; source.spatialBlend = spatial ? 1 : 0; source.spatialize = false;
            source.dopplerLevel = 0; source.spread = 0; source.minDistance = 1; source.maxDistance = 3;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        [UnityTest]
        public IEnumerator PcmMutableGainPreservesDryWetAndIndependentProgress()
        {
            var owner = sink.Owner;
            foreach (bool wetOnly in new[] { false, true })
            {
                var first = Begin(0, wetOnly ? 0 : 1, wetOnly ? 1 : 0);
                var other = Begin(1, wet: 0);
                yield return PcmReady(0); yield return PcmReady(1);
                Queue(first, 440, 40); Queue(other, 660, 40);
                yield return Measure("gain-baseline-" + wetOnly);
                double full = StereoPower(440), independent = StereoPower(660);
                Assert.Greater(full, 1e-9);
                long played = first.Read(AudioSettings.dspTime).Played;
                long revision = owner.RequestedSceneRevision;
                Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], first, .25f));
                yield return Measure("gain-quarter-" + wetOnly);
                Assert.That(StereoPower(440) / full, Is.EqualTo(.0625).Within(.008));
                Assert.That(StereoPower(660) / independent, Is.EqualTo(1).Within(.08));
                Assert.AreEqual(revision, owner.RequestedSceneRevision, "Gain must not resimulate the scene");
                Assert.Greater(first.Read(AudioSettings.dspTime).Played, played);
                Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetPcmGain(PreparedGraph.VoiceIds[0], first, float.NaN));
                Assert.AreEqual(.25f, first.Gain);
                Assert.IsFalse(owner.SetPcmGain(PreparedGraph.VoiceIds[1], first, .5f));
                Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], first, 0));
                Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[1], other, 0));
                played = first.Read(AudioSettings.dspTime).Played;
                yield return Measure("gain-muted-" + wetOnly);
                Silence();
                Assert.Greater(first.Read(AudioSettings.dspTime).Played, played, "Zero gain is not Pause");
                Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], first, .5f));
                yield return Measure("gain-resumed-" + wetOnly);
                Assert.That(StereoPower(440) / full, Is.EqualTo(.25).Within(.02));
                Assert.IsTrue(owner.StopPcm(PreparedGraph.VoiceIds[0], first));
                var replacement = Begin(0);
                Assert.IsFalse(owner.SetPcmGain(PreparedGraph.VoiceIds[0], first, 0));
                Assert.AreEqual(1, replacement.Gain);
                Assert.IsTrue(other.Open);
            }
        }
        [UnityTest]
        public IEnumerator PcmGainAtSubmissionMutesOnlyTheOutputNotSourceProgress()
        {
            var owner = sink.Owner;
            var voice = Begin(0, wet: 0);
            yield return PcmReady(0);
            sink.BypassRendering = true;
            yield return new WaitForSecondsRealtime(.1f);
            Queue(voice, 440, 2);
            var graph = owner.Active;
            var block = new float[graph.Frame * 2];
            double time = AudioSettings.dspTime;
            graph.BeforeOutput = () => Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], voice, 0));
            try { Assert.IsTrue(owner.Render(block, 2, time)); }
            finally { graph.BeforeOutput = null; }
            Assert.AreEqual(0, Energy(block));
            Assert.Greater(voice.Read(time + .1).Played, 0);
            Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], voice, 1));
            Assert.IsTrue(owner.Render(block, 2, time + (double)graph.Frame / graph.Rate));
            Assert.Greater(Energy(block), 1e-8);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmMutedFinalTailStillCompletesItsExactOwner()
        {
            var owner = sink.Owner;
            var graph = owner.Active;
            var voice = Begin(0, dry: 0);
            yield return ManualFiniteInput(voice);
            var block = new float[graph.Frame * 2];
            double time = SubmitFiniteSource(owner, graph, voice, block, AudioSettings.dspTime);
            Assert.Greater(Energy(block), 1e-8, "Reflections must exist before the mute");
            graph.BeforeOutput = () => Assert.IsTrue(owner.SetPcmGain(PreparedGraph.VoiceIds[0], voice, 0));
            int count = 0;
            try
            {
                while (!voice.RendererDrained(time + 60) && count++ < 100)
                {
                    Assert.IsTrue(owner.Render(block, 2, time));
                    Assert.AreEqual(0, Energy(block), "Gain zero must include the remaining wet tail");
                    time += (double)graph.Frame / graph.Rate;
                }
            }
            finally { graph.BeforeOutput = null; }
            Assert.Less(count, 100);
            Assert.IsTrue(owner.FinishPcm(PreparedGraph.VoiceIds[0], voice, time + .1, out var final));
            Assert.AreEqual(4800, final.Played);
            Assert.IsTrue(final.SourceDrained);
            sink.BypassRendering = false;
        }
        [UnityTest]
        public IEnumerator PcmNonspatialMatchesUnityAndIgnoresWorldMovement()
        {
            var reference = new GameObject("Nonspatial Unity reference");
            var clip = ReferenceTone();
            try
            {
                var source = reference.AddComponent<AudioSource>();
                ConfigureReference(source, clip, false); source.Play();
                yield return Measure("unity-nonspatial-reference");
                double referencePower = StereoPower(440);
                source.Stop();
                var owner = sink.Owner;
                var voice = owner.BeginPcm("nonspatial", owner.Generation, FreshStream(), .02,
                    settings: new PcmSpatialSettings(false, 1, 3));
                yield return PcmReady(voice.Slot);
                Queue(voice, 440, 40);
                yield return Measure("candidate-nonspatial");
                double power = StereoPower(440);
                Debug.Log("PCM_UNITY_2D reference=" + referencePower + " candidate=" + power + " ratio=" + power / referencePower);
                Assert.That(power / referencePower, Is.EqualTo(1).Within(.05));
                for (int i = 0; i < measured.Length; i++) Assert.AreEqual(measured[i], measuredRight[i], 1e-7);
                listener.transform.SetPositionAndRotation(new Vector3(4, 2, 5), Quaternion.Euler(0, 128, 0));
                owner.UpdateScene(new AcousticFrame(false, PcmTransformBinding.Pose(listener.transform),
                    AcousticFrame.Pose(-3, 0, 12), AcousticFrame.Pose(1, 0, 0)));
                yield return Measure("nonspatial-after-world-listener-movement");
                Assert.That(StereoPower(440) / power, Is.EqualTo(1).Within(.05));
                for (int i = 0; i < measured.Length; i++) Assert.AreEqual(measured[i], measuredRight[i], 1e-7);
                Assert.IsFalse(voice.Read(AudioSettings.dspTime).Faulted);
            }
            finally { UnityEngine.Object.Destroy(reference); UnityEngine.Object.Destroy(clip); }
        }
        [UnityTest]
        public IEnumerator PcmDistanceRolloffMatchesUnityReference()
        {
            var reference = new GameObject("Logarithmic Unity reference");
            var clip = ReferenceTone();
            float[] distances = { 0, .5f, 1, 2, 4, 8 };
            var powers = new double[distances.Length];
            try
            {
                var source = reference.AddComponent<AudioSource>();
                foreach (float minimum in new[] { 1f, 2f })
                {
                    ConfigureReference(source, clip, true); source.minDistance = minimum; source.Play();
                    for (int i = 0; i < distances.Length; i++)
                    {
                        reference.transform.position = new Vector3(0, 0, distances[i]);
                        yield return Measure("unity-distance-" + minimum + "-" + distances[i]);
                        powers[i] = StereoPower(440);
                        Assert.Greater(powers[i], 1e-9);
                    }
                    source.Stop();
                    var owner = sink.Owner;
                    owner.UpdateScene(new AcousticFrame(false, AcousticFrame.Pose(0, 0, 0),
                        AcousticFrame.Pose(0, 0, -.5f), AcousticFrame.Pose(1, 0, 0)));
                    var voice = owner.BeginPcm("distance", owner.Generation, FreshStream(), .02, wetGain: 0,
                        settings: new PcmSpatialSettings(true, minimum, 3));
                    yield return PcmReady(voice.Slot);
                    Queue(voice, 440, 40);
                    double baseline = 0;
                    for (int i = 0; i < distances.Length; i++)
                    {
                        Assert.IsTrue(owner.SetPcmPose("distance", voice, AcousticFrame.Pose(0, 0, -distances[i])));
                        yield return Measure("candidate-distance-" + minimum + "-" + distances[i]);
                        double power = StereoPower(440);
                        if (i == 0) baseline = power;
                        double actual = Math.Sqrt(power / baseline), expected = Math.Sqrt(powers[i] / powers[0]);
                        Debug.Log("PCM_UNITY_DISTANCE minimum=" + minimum + " metres=" + distances[i] +
                                  " reference=" + expected + " candidate=" + actual);
                        Assert.That(actual / expected, Is.EqualTo(1).Within(.05));
                    }
                    Assert.IsTrue(owner.StopPcm("distance", voice));
                }
            }
            finally { UnityEngine.Object.Destroy(reference); UnityEngine.Object.Destroy(clip); }
        }
        [UnityTest]
        public IEnumerator PcmAnchorFollowsParentTranslationRotationAndScale()
        {
            var parent = new GameObject("Moving avatar reference");
            try
            {
                parent.transform.position = new Vector3(1, -2, 0);
                parent.transform.localScale = new Vector3(2, 1.5f, 1);
                var mouth = new GameObject("Borrowed mouth anchor"); mouth.transform.SetParent(parent.transform, false);
                mouth.transform.localPosition = new Vector3(.2f, 1.4f, .1f);
                var owner = sink.Owner;
                owner.UpdateListener(AcousticFrame.Pose(0, 0, 0));
                var voice = owner.BeginPcm("mouth", owner.Generation, FreshStream(), .02, wetGain: 0);
                var follower = mouth.AddComponent<PcmAnchorFollower>(); follower.Bind(owner, "mouth", voice);
                Queue(voice, 440, 40);
                yield return PcmReady(voice.Slot);
                yield return Measure("mouth-parent-right");
                double before = Power(measured, AudioSettings.outputSampleRate, 440) /
                                Power(measuredRight, AudioSettings.outputSampleRate, 440);
                long revision = owner.RequestedSceneRevision;
                yield return new WaitForSecondsRealtime(.1f);
                Assert.AreEqual(revision, owner.RequestedSceneRevision, "Unmoved anchors must not resimulate every frame");
                parent.transform.SetPositionAndRotation(new Vector3(-1, -2, 0), Quaternion.Euler(0, 180, 0));
                parent.transform.localScale = new Vector3(3, 1.5f, 2);
                // No SetPcmPose or Refresh call: the actual LateUpdate binding follows the parent.
                yield return Measure("mouth-parent-left");
                double after = Power(measured, AudioSettings.outputSampleRate, 440) /
                               Power(measuredRight, AudioSettings.outputSampleRate, 440);
                Assert.Less(Math.Log(before) * Math.Log(after), 0);
                Assert.Greater(Math.Abs(Math.Log(before)), .1);
                Assert.Greater(owner.RequestedSceneRevision, revision);
                Assert.IsTrue(voice.Open);
                Assert.Greater(voice.Read(AudioSettings.dspTime).Played, 12000);
                var replacement = owner.BeginPcm("mouth", owner.Generation, FreshStream(), .02);
                yield return null;
                Assert.IsFalse(follower.enabled, "The obsolete binding must retire itself");
                UnityEngine.Object.Destroy(parent);
                yield return null;
                Assert.IsTrue(replacement.Open, "A stale anchor cannot stop its replacement");
            }
            finally { if (parent) UnityEngine.Object.Destroy(parent); }
        }
        [UnityTest]
        public IEnumerator PcmAnchorLossStopsOnlyItsVoiceAndCannotReopenIt()
        {
            var anchor = new GameObject("Temporary sound anchor");
            try
            {
                var owner = sink.Owner;
                var voice = owner.BeginPcm("attached", owner.Generation, FreshStream(), .02);
                var other = owner.BeginPcm("independent", owner.Generation, FreshStream(), .02);
                var follower = anchor.AddComponent<PcmAnchorFollower>(); follower.Bind(owner, "attached", voice);
                Queue(voice, 440); Queue(other, 660);
                yield return null;
                anchor.SetActive(false);
                Assert.IsFalse(voice.Open);
                Assert.IsTrue(other.Open);
                anchor.SetActive(true);
                yield return null;
                Assert.IsFalse(voice.Open);
                Assert.IsFalse(follower.enabled);
                yield return Measure("independent-after-anchor-loss");
                Tone(660);
                Assert.IsFalse(owner.SetPcmPose("attached", voice, AcousticFrame.Pose(0, 0, 0)));
                Assert.Throws<ArgumentException>(() => new PcmSpatialSettings(true, 0, 3));
                Assert.Throws<ArgumentException>(() => new PcmSpatialSettings(true, 2, 1));
                Assert.Throws<ArgumentException>(() => new PcmSpatialSettings(false, 1, float.NaN));
            }
            finally { UnityEngine.Object.Destroy(anchor); }
        }
    }
}
