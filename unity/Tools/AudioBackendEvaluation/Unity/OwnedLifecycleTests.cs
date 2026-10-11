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
    public sealed class OwnedSink : MonoBehaviour
    {
        internal OwnedLifecycle Owner = new OwnedLifecycle();
        internal volatile bool BypassRendering;
        AudioSource source;
        AudioClip carrier;
        void OnEnable()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0;
            source.loop = true;
            AudioSettings.OnAudioConfigurationChanged += Changed;
            Changed(false);
        }
        void Changed(bool deviceChanged)
        {
            var actual = AudioSettings.GetConfiguration();
            Owner.Configure(actual.sampleRate, actual.dspBufferSize);
            // Unity discards generated clips on device reset. They are ephemeral.
            source.Stop();
            if (carrier)
                Destroy(carrier);
            carrier = AudioClip.Create("Silent lifecycle carrier", actual.sampleRate, 2, actual.sampleRate, false);
            source.clip = carrier;
            source.Play();
        }
        void Update() => Owner.Tick();
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (BypassRendering)
                Array.Clear(data, 0, data.Length);
            else
                Owner.Render(data, channels, AudioSettings.dspTime);
        }
        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= Changed;
            Owner.Close();
            if (source)
                source.Stop();
            if (carrier)
                Destroy(carrier);
        }
    }
    public sealed class CapturedListener : MonoBehaviour
    {
        readonly object gate = new object();
        readonly float[] captured = new float[262144], capturedRight = new float[262144];
        int count, blocks;
        bool finite = true;
        void OnAudioFilterRead(float[] samples, int channels)
        {
            lock (gate)
            {
                blocks++;
                for (int i = 0; i < samples.Length; i += channels)
                {
                    for (int c = 0; c < channels; c++)
                        finite &= !float.IsNaN(samples[i + c]) && !float.IsInfinity(samples[i + c]);
                    if (count < captured.Length)
                    {
                        captured[count] = samples[i];
                        capturedRight[count] = samples[i + (channels > 1 ? 1 : 0)];
                        count++;
                    }
                }
            }
            Array.Clear(samples, 0, samples.Length); // Native output measured, never played to speakers.
        }
        internal void Clear()
        {
            lock (gate)
            {
                count = blocks = 0;
                finite = true;
            }
        }
        internal (float[] samples, float[] right, int blocks, bool finite) Read()
        {
            lock (gate)
            {
                var copy = new float[count];
                Array.Copy(captured, copy, count);
                var right = new float[count];
                Array.Copy(capturedRight, right, count);
                return (copy, right, blocks, finite);
            }
        }
    }
    public sealed partial class OwnedLifecycleTests
    {
        GameObject emitter, listener;
        OwnedSink sink;
        CapturedListener capture;
        OwnedLifecycle standalone;
        ManualResetEventSlim preparationGate;
        PreparedGraph heldGraph;
        AudioConfiguration original;
        AudioListener[] others;
        bool[] enabled;
        float volume;
        bool pause;
        int opened, closed, diagnostics;
        float[] measured, measuredRight;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            original = AudioSettings.GetConfiguration();
            volume = AudioListener.volume;
            pause = AudioListener.pause;
            opened = PreparedGraph.Opened;
            closed = PreparedGraph.Closed;
            diagnostics = PreparedGraph.Diagnostics;
            others = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            enabled = new bool[others.Length];
            for (int i = 0; i < others.Length; i++)
            {
                enabled[i] = others[i].enabled;
                others[i].enabled = false;
            }
            AudioListener.volume = 1;
            AudioListener.pause = false;
            Assert.AreEqual(AudioSpeakerMode.Stereo, original.speakerMode, "Probe requires actual stereo output");
            listener = new GameObject("Measured then muted listener");
            listener.AddComponent<AudioListener>();
            capture = listener.AddComponent<CapturedListener>();
            emitter = new GameObject("Owned native output");
            sink = emitter.AddComponent<OwnedSink>();
            yield return Ready(sink.Owner);
        }
        static IEnumerator Ready(OwnedLifecycle owner)
        {
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (owner.Active == null && owner.Failure == null && Time.realtimeSinceStartupAsDouble < end)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsNull(owner.Failure, owner.Failure?.ToString());
            Assert.IsNotNull(owner.Active, "Native preparation deadline");
        }
        IEnumerator Measure(string label)
        {
            yield return new WaitForSecondsRealtime(.35f);
            capture.Clear();
            yield return new WaitForSecondsRealtime(.3f);
            var result = capture.Read();
            Assert.Greater(result.blocks, 0, "Real Unity listener callbacks required");
            Assert.IsTrue(result.finite);
            measured = result.samples;
            measuredRight = result.right;
            double energy = 0;
            foreach (float sample in measured)
                energy += sample * (double)sample;
            Debug.Log("OWNED_AUDIO " + label + " rate=" + AudioSettings.outputSampleRate +
                      " samples=" + measured.Length + " energy=" + energy);
        }
        static double Power(float[] samples, int rate, double frequency)
        {
            double re = 0, im = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double phase = 2 * Math.PI * frequency * i / rate;
                re += samples[i] * Math.Cos(phase);
                im += samples[i] * Math.Sin(phase);
            }
            return (re * re + im * im) / Math.Max(1, (double)samples.Length * samples.Length);
        }
        void Tone(int frequency)
        {
            int rate = AudioSettings.outputSampleRate;
            double desired = Power(measured, rate, frequency);
            Assert.Greater(desired, 1e-10, "Expected actual native reflected tone");
            Assert.Greater(desired, Power(measured, rate, frequency / 2.0) * 20,
                           "Requested tone missing or unexpected half-rate component");
            Assert.Greater(desired, Power(measured, rate, frequency * 2.0) * 20,
                           "Requested tone missing or unexpected double-rate component");
        }
        void Silence()
        {
            foreach (float sample in measured)
                Assert.Less(Math.Abs(sample), 1e-8, "Interrupted audio must not replay");
        }
        [UnityTest]
        public IEnumerator DeviceResetRebuildsFormatWithoutReplayingVoices()
        {
            string[] ids = (string[])PreparedGraph.VoiceIds.Clone();
            foreach (bool changeRate in new[] { false, true, true })
            {
                var owner = sink.Owner;
                long old = owner.Generation;
                Assert.IsTrue(owner.Play(ids[0], old));
                Assert.IsTrue(owner.Play(ids[1], old));
                yield return Measure("two-voices");
                Tone(440);
                Tone(660);
                Assert.IsTrue(owner.Play(ids[0], old, false));
                yield return Measure("first-stopped");
                Tone(660);
                Assert.Less(Power(measured, AudioSettings.outputSampleRate, 440),
                            Power(measured, AudioSettings.outputSampleRate, 660) * .01);
                Assert.IsTrue(owner.Play(ids[0], old));
                yield return Measure("two-voices-before-reset");
                Tone(440);
                Tone(660);
                Assert.AreEqual(2, owner.Active.Playing);
                var configuration = AudioSettings.GetConfiguration();
                int previousRate = configuration.sampleRate;
                int previousFrame = configuration.dspBufferSize;
                if (changeRate)
                {
                    configuration.sampleRate = previousRate == 24000 ? 48000 : 24000;
                    configuration.dspBufferSize = previousFrame == 256 ? 512 : 256;
                }
                Assert.IsTrue(AudioSettings.Reset(configuration));
                yield return Ready(owner);
                var actual = AudioSettings.GetConfiguration();
                if (changeRate)
                {
                    Assert.AreNotEqual(previousRate, actual.sampleRate, "Device really changed rate");
                    Assert.AreNotEqual(previousFrame, actual.dspBufferSize, "Device really changed block size");
                }
                Debug.Log("OWNED_FORMAT previousRate=" + previousRate + " actualRate=" + actual.sampleRate +
                          " previousFrame=" + previousFrame + " actualFrame=" + actual.dspBufferSize +
                          " generation=" + owner.Generation);
                Assert.AreEqual(actual.sampleRate, owner.Active.Rate);
                Assert.AreEqual(actual.dspBufferSize, owner.Active.Frame);
                Assert.Greater(owner.Generation, old);
                Assert.IsFalse(owner.Play(ids[0], old), "Reject late pre-reset command");
                Assert.AreEqual(0, owner.Active.Playing);
                CollectionAssert.AreEqual(ids, PreparedGraph.VoiceIds);
                yield return Measure("reset-silence");
                Silence();
                Assert.IsTrue(owner.Play(ids[0], owner.Generation));
                yield return Measure("explicit-restart");
                Tone(440);
            }
            Assert.GreaterOrEqual(sink.Owner.InterruptedVoices, 6);
        }
        [UnityTest]
        public IEnumerator RetiredGraphWaitsForOwnedReader()
        {
            var owner = sink.Owner;
            heldGraph = owner.Active;
            Assert.IsTrue(heldGraph.TryEnter());
            var format = AudioSettings.GetConfiguration();
            owner.Configure(format.sampleRate, format.dspBufferSize);
            for (int i = 0; i < 3; i++)
            {
                owner.Tick();
                yield return null;
            }
            Assert.IsFalse(heldGraph.Disposed);
            Assert.IsNull(owner.Active);
            Assert.IsFalse(heldGraph.TryEnter(), "No new consumer may enter a retired graph");
            var old = heldGraph;
            heldGraph.Exit();
            heldGraph = null;
            yield return Ready(owner);
            Assert.IsTrue(old.Disposed);
        }
        [UnityTest]
        public IEnumerator SupersededPreparationDrainsBeforeLatestGenerationPublishes()
        {
            var entered = new ManualResetEventSlim();
            preparationGate = new ManualResetEventSlim();
            standalone = new OwnedLifecycle();
            var format = AudioSettings.GetConfiguration();
            standalone.Configure(format.sampleRate, format.dspBufferSize,
                                 () =>
                                 {
                                     entered.Set();
                                     if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                         throw new TimeoutException("Test preparation gate");
                                 });
            standalone.Tick();
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
                yield return null;
            Assert.IsTrue(entered.IsSet, "Worker owns a real prepared native graph");
            int before = PreparedGraph.Closed;
            for (int i = 0; i < 8; i++)
                standalone.Configure(24000, 256);
            standalone.Tick();
            Assert.IsNull(standalone.Active);
            Assert.AreEqual(before, PreparedGraph.Closed);
            preparationGate.Set();
            yield return Ready(standalone);
            Assert.AreEqual(1, standalone.StalePreparations);
            Assert.AreEqual(24000, standalone.Active.Rate);
            Assert.AreEqual(256, standalone.Active.Frame);
            Assert.AreEqual(0, standalone.Active.Playing);
            Assert.AreEqual(before + 1, PreparedGraph.Closed);
            entered.Dispose();
        }
        [UnityTest]
        public IEnumerator PreparationFailureReleasesNativeOwnershipAndCanRetry()
        {
            standalone = new OwnedLifecycle();
            standalone.Configure(24000, 256, () => throw new InvalidOperationException("Injected failure"));
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (standalone.Failure == null && Time.realtimeSinceStartupAsDouble < end)
            {
                standalone.Tick();
                yield return null;
            }
            Assert.AreEqual("Injected failure", standalone.Failure?.Message);
            Assert.IsNull(standalone.Active);
            standalone.Configure(48000, 512);
            yield return Ready(standalone);
            Assert.AreEqual(48000, standalone.Active.Rate);
            Assert.AreEqual(512, standalone.Active.Frame);
        }
        [UnityTest]
        public IEnumerator SupersededFailureCannotPoisonNewConfiguration()
        {
            var entered = new ManualResetEventSlim();
            preparationGate = new ManualResetEventSlim();
            standalone = new OwnedLifecycle();
            standalone.Configure(48000, 512,
                                 () =>
                                 {
                                     entered.Set();
                                     if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                         throw new TimeoutException("Test preparation gate");
                                     throw new InvalidOperationException("Stale worker failure");
                                 });
            standalone.Tick();
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
                yield return null;
            Assert.IsTrue(entered.IsSet);
            standalone.Configure(24000, 256);
            preparationGate.Set();
            yield return Ready(standalone);
            Assert.IsNull(standalone.Failure);
            Assert.AreEqual(24000, standalone.Active.Rate);
            Assert.AreEqual(1, standalone.StalePreparations);
            Assert.AreEqual("Stale worker failure", standalone.RetiredFailure?.Message);
            entered.Dispose();
        }
        [UnityTest]
        public IEnumerator CloseDuringPreparationCannotPublishOrReplay()
        {
            var entered = new ManualResetEventSlim();
            preparationGate = new ManualResetEventSlim();
            standalone = new OwnedLifecycle();
            standalone.Configure(24000, 256,
                                 () =>
                                 {
                                     entered.Set();
                                     if (!preparationGate.Wait(TimeSpan.FromSeconds(15)))
                                         throw new TimeoutException("Test preparation gate");
                                 });
            standalone.Tick();
            double end = Time.realtimeSinceStartupAsDouble + 10;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end)
                yield return null;
            Assert.IsTrue(entered.IsSet);
            long old = standalone.Generation;
            int before = PreparedGraph.Closed;
            standalone.Close();
            standalone.Tick();
            Assert.IsFalse(standalone.Drained);
            Assert.AreEqual(before, PreparedGraph.Closed, "Worker still owns native resources");
            Assert.IsFalse(standalone.Play(PreparedGraph.VoiceIds[0], old));
            preparationGate.Set();
            while (!standalone.Drained && Time.realtimeSinceStartupAsDouble < end)
            {
                standalone.Tick();
                yield return null;
            }
            Assert.IsTrue(standalone.Drained);
            Assert.IsNull(standalone.Active);
            Assert.AreEqual(before + 1, PreparedGraph.Closed);
            entered.Dispose();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            preparationGate?.Set();
            if (heldGraph != null)
            {
                heldGraph.Exit();
                heldGraph = null;
            }
            var owner = sink?.Owner;
            if (emitter)
                UnityEngine.Object.Destroy(emitter);
            owner?.Close();
            standalone?.Close();
            double end = Time.realtimeSinceStartupAsDouble + 20;
            while (((owner != null && !owner.Drained) || (standalone != null && !standalone.Drained)) &&
                   Time.realtimeSinceStartupAsDouble < end)
            {
                owner?.Tick();
                standalone?.Tick();
                yield return null;
            }
            bool drained = (owner == null || owner.Drained) && (standalone == null || standalone.Drained);
            if (listener)
                UnityEngine.Object.Destroy(listener);
            AudioSettings.Reset(original);
            yield return null;
            if (others != null)
                for (int i = 0; i < others.Length; i++)
                    if (others[i])
                        others[i].enabled = enabled[i];
            AudioListener.volume = volume;
            AudioListener.pause = pause;
            preparationGate?.Dispose();
            preparationGate = null;
            standalone = null;
            Debug.Log("OWNED_LIFETIME opened=" + (PreparedGraph.Opened - opened) +
                      " closed=" + (PreparedGraph.Closed - closed) +
                      " nativeDiagnostics=" + (PreparedGraph.Diagnostics - diagnostics));
            Assert.IsTrue(drained, "Do not release a still-owned worker or callback on timeout");
            Assert.AreEqual(PreparedGraph.Opened - opened, PreparedGraph.Closed - closed,
                            "Every native graph released");
            Assert.AreEqual(diagnostics, PreparedGraph.Diagnostics, "Native validation must remain clean");
        }
    }
}
