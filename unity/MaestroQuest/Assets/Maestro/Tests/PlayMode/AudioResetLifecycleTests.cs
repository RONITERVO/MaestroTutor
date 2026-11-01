// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    // Observation only. Never restarts an interrupted generation or retries PCM.
    internal sealed class AudioConfigurationTrace : IDisposable
    {
        readonly JArray events = new();
        internal int Count { get; private set; }
        internal AudioConfigurationTrace() => AudioSettings.OnAudioConfigurationChanged += Changed;
        void Changed(bool deviceChanged)
        {
            Count++;
            var config = AudioSettings.GetConfiguration();
            if (events.Count < 16) events.Add(new JObject {
                ["ordinal"] = Count, ["deviceChanged"] = deviceChanged,
                ["realtime"] = Time.realtimeSinceStartupAsDouble, ["dsp"] = AudioSettings.dspTime,
                ["sampleRate"] = config.sampleRate, ["buffer"] = config.dspBufferSize
            });
        }
        internal JObject Snapshot() => new() { ["count"] = Count, ["events"] = events.DeepClone() };
        internal string Describe(NativeSpeechOutput speech, string phase) =>
            phase + ": " + new JObject {
                ["audio"] = Snapshot(), ["voice"] = JObject.FromObject(speech.Read()),
                ["realtime"] = Time.realtimeSinceStartupAsDouble, ["dsp"] = AudioSettings.dspTime
            }.ToString(Newtonsoft.Json.Formatting.None);
        public void Dispose() => AudioSettings.OnAudioConfigurationChanged -= Changed;
    }

    public sealed partial class RoomAcousticsTests
    {
        [UnityTest] public IEnumerator ActualAudioResetCancelsBookSpeechAndFreshRequestRetainsAcousticObstruction()
        {
            var frame = new GameObject("Reset test world"); frame.transform.SetParent(root.transform, false);
            var wall = Cube(); wall.transform.SetParent(frame.transform, false);
            wall.transform.localPosition = Vector3.forward; wall.transform.localScale = new Vector3(4, 4, .1f);
            var mesh = UnityEngine.Object.Instantiate(wall.Mesh); wall.Bind(mesh);
            var listenerObject = new GameObject("Muted reset listener", typeof(AudioListener), typeof(SpeechListenerProbe));
            var probe = listenerObject.GetComponent<SpeechListenerProbe>();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled = listeners.Select(x => x.enabled).ToArray();
            foreach (var listener in listeners) listener.enabled = listener.gameObject == listenerObject;
            float volume = AudioListener.volume; bool paused = AudioListener.pause;
            AudioListener.volume = 1; AudioListener.pause = false;
            var emitter = new GameObject("Reset test voice"); emitter.transform.SetParent(root.transform, false);
            emitter.transform.position = Vector3.forward * 2;
            var speech = emitter.AddComponent<NativeSpeechOutput>();
            using var bridge = new BookSpeechSession(speech);
            using var trace = new AudioConfigurationTrace();
            RoomAcoustics.AcousticResidency retained = null;
            var evidence = new JObject();
            long poll = 0;
            double Now() => Time.realtimeSinceStartupAsDouble;
            JObject Chunk(int sequence)
            {
                var random = new System.Random(125 + sequence); var bytes = new byte[9600];
                for (int i = 0; i < bytes.Length; i += 2) {
                    short sample = (short)random.Next(-1000, 1001);
                    bytes[i] = (byte)sample; bytes[i + 1] = (byte)(sample >> 8);
                }
                return new JObject { ["sequence"] = sequence, ["pcm"] = Convert.ToBase64String(bytes) };
            }
            void Send(long revision, bool open, bool audio = false, bool complete = false)
            {
                bridge.Receive(new JObject {
                    ["document"] = new string('a', 32), ["poll"] = ++poll,
                    ["payload"] = new JObject {
                        ["version"] = 3, ["session"] = new string('b', 32), ["revision"] = revision,
                        ["open"] = open, ["chunks"] = audio ? new JArray(Enumerable.Range(1, 4).Select(Chunk)) : new JArray(),
                        ["completion"] = complete ? new JObject { ["sequence"] = 4, ["submittedSamples"] = 19200 } : null
                    }
                }.ToString(Newtonsoft.Json.Formatting.None), Now());
            }
            JObject Capture(string name)
            {
                var status = JObject.Parse(bridge.Status(Now()));
                evidence[name] = new JObject {
                    ["status"] = status, ["voice"] = JObject.FromObject(speech.Read()),
                    ["geometry"] = room.GeometryCount, ["ready"] = room.Ready, ["audio"] = trace.Snapshot()
                };
                return status;
            }
            IEnumerator Play(long revision, string label, Action<double> energy)
            {
                probe.Reset(); Send(revision, true, true); long generation = speech.Read().generation;
                Assert.That((string)Capture(label + "Accepted")["status"], Is.EqualTo("playing"));
                Send(revision, true, complete: true);
                double deadline = Now() + 8;
                while (!speech.Read().rendererDrained && speech.Read().generation == generation && Now() < deadline) {
                    Send(revision, true); yield return null;
                }
                Assert.That(speech.Read().generation, Is.EqualTo(generation), trace.Describe(speech, label));
                var status = Capture(label + "Drained");
                Assert.That((long)status["playedSamples"], Is.EqualTo(19200), trace.Describe(speech, label));
                Assert.That((bool)status["rendererDrained"], Is.True);
                var measured = probe.Read(); energy(measured.Left + measured.Right);
            }
            try
            {
                Assert.That(room.TryPrepareResidency(wall.gameObject, frame.transform, out retained, out var issue), Is.True, issue);
                Assert.That(retained.Commit(), Is.True);
                UnityEngine.Object.Destroy(wall.gameObject); UnityEngine.Object.Destroy(mesh); yield return null;
                room.Synchronize(); Assert.That(room.GeometryCount, Is.EqualTo(1)); Assert.That((bool)wall || (bool)mesh, Is.False);
                Send(0, false); Assert.That((string)Capture("initial")["status"], Is.EqualTo("ready"));
                Send(1, true, true); long interrupted = speech.Read().generation;
                double deadline = Now() + 5;
                while (speech.Read().playedSamples < 2400 && speech.Read().generation == interrupted && Now() < deadline) {
                    Send(1, true); yield return null;
                }
                Assert.That(speech.Read().playedSamples, Is.GreaterThanOrEqualTo(2400).And.LessThan(19200), trace.Describe(speech, "before reset"));
                Capture("beforeReset"); int changes = trace.Count;
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True, "Exercise the actual engine event");
                deadline = Now() + 5;
                while (trace.Count == changes && Now() < deadline) yield return null;
                Assert.That(trace.Count, Is.GreaterThan(changes));
                var stopped = Capture("afterReset");
                Assert.That(speech.Read().generation, Is.Not.EqualTo(interrupted));
                Assert.That((string)stopped["status"], Is.EqualTo("failed"));
                Assert.That((long)stopped["playedSamples"], Is.Zero); Assert.That((bool)stopped["rendererDrained"], Is.False);
                Assert.That((bool)stopped["microphoneSuppressed"], Is.True);
                Assert.That(speech.TryWrite(interrupted, 5, new short[240], out _), Is.False);
                Assert.That(speech.TryComplete(interrupted, 4, 19200, out _), Is.False);
                long retired = speech.Read().generation;
                Send(1, true, true); Capture("lateRequest");
                Assert.That(speech.Read().generation, Is.EqualTo(retired));
                Assert.That(speech.Read().submittedSamples, Is.Zero, "Old book revision must not reopen the cancelled voice");
                deadline = Now() + 5;
                while ((!room.Ready || room.GeometryCount != 1) && Now() < deadline) yield return null;
                Assert.That(room.Ready, Is.True, room.Issue);
                Assert.That(room.GeometryCount, Is.EqualTo(1), "Reset preserves retired acoustic geometry");
                // Browser failure disposal advances reset and release separately.
                Send(3, false); Assert.That((string)Capture("closed")["status"], Is.EqualTo("ready"));
                double obstructed = 0, clear = 0;
                yield return Play(4, "recovered", value => obstructed = value);
                retained.Dispose(); retained = null;
                deadline = Now() + 5;
                while (room.GeometryCount != 0 && Now() < deadline) yield return null;
                Assert.That(room.GeometryCount, Is.Zero);
                yield return Play(7, "clear", value => clear = value);
                Assert.That(clear, Is.GreaterThan(.0001));
                Assert.That(obstructed, Is.LessThan(clear * .7), "Fresh speech must still hear the retained wall after reset");
                evidence["energy"] = new JObject { ["retained"] = obstructed, ["clear"] = clear };
            }
            finally
            {
                string folder = Environment.GetEnvironmentVariable("MAESTRO_AUDIO_RESET_EVIDENCE");
                if (!string.IsNullOrEmpty(folder)) {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "book-reset.json"), evidence.ToString());
                }
                retained?.Dispose(); bridge.Dispose(); room.enabled = false;
                UnityEngine.Object.Destroy(listenerObject);
                if (mesh) UnityEngine.Object.Destroy(mesh);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
        }
    }
}
