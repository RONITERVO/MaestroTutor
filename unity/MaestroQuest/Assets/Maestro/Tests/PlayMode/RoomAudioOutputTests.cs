// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Threading;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class RoomAudioOutputTests
    {
        ManualResetEventSlim computeGate, computeEntered;
        [UnityTearDown] public IEnumerator CleanupComputation()
        {
            // Initial map work also owns native inputs, even when an assertion
            // prevents the later replacement gate from ever being created.
            computeGate?.Set();
            var next = new GameObject("Mixer computation cleanup").AddComponent<RoomAcoustics>();
            bool released = false;
            try
            {
                double end = Time.realtimeSinceStartupAsDouble + 5;
                while (!next.Ready && Time.realtimeSinceStartupAsDouble < end) yield return null;
                released = next.Ready;
            }
            finally { UnityEngine.Object.Destroy(next.gameObject); }
            yield return null;
            if (released) { computeGate?.Dispose(); computeEntered?.Dispose(); computeGate = computeEntered = null; }
            Assert.IsTrue(released, "Retired mixer computation must release the room before the next fixture");
        }
        [UnityTest] public IEnumerator RepeatedRoomMixReplacementPreservesNativeMapAndVoiceLifetimes()
        {
            // Exercise native DSP, geometry, maps and mixer reload together. A
            // source-only mock cannot detect a cross-thread native lifetime fault.
            for (int cycle = 0; cycle < 8; cycle++)
            {
                Trace("cycle-begin " + cycle);
                yield return RenderMix(replaceMapDuringSpeech: cycle % 2 == 1);
                yield return Resources.UnloadUnusedAssets();
                GC.Collect(); GC.WaitForPendingFinalizers();
                yield return null;
                Trace("cycle-end " + cycle);
            }
        }
        [UnityTest] public IEnumerator OwnedReflectionMixRendersAndStoppingOneVoicePreservesAnother() => RenderMix(false);
        [UnityTest] public IEnumerator MapReplacementDuringMultipleVoicesKeepsDspAlive() => RenderMix(true);
        [UnityTest] public IEnumerator InitialMapRetirementWaitsWithoutReplacementGate()
        {
            // The initial calculation can still own the native context when an
            // assertion fails, before RenderMix creates its replacement gate.
            var initialGate = new ManualResetEventSlim(false);
            var initialEntered = new ManualResetEventSlim(false);
            var root = new GameObject("Initial map cleanup regression");
            var room = root.AddComponent<RoomAcoustics>();
            IEnumerator drain = null;
            try
            {
                Assert.IsTrue(room.Ready, room.Issue);
                AddWalls(root);
                room.Synchronize(); room.Synchronize();
                room.FirstMapProgress = () => { initialEntered.Set(); initialGate.Wait(TimeSpan.FromSeconds(10)); };
                Assert.IsTrue(room.RequestMap(new[] { Vector3.zero }));
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (!initialEntered.IsSet && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsTrue(initialEntered.IsSet, "Hold the real initial map calculation");
                root.SetActive(false);
                Assert.IsNull(computeGate, "No replacement calculation has been requested");
                drain = CleanupComputation();
                for (int frame = 0; frame < 4; frame++)
                {
                    Assert.IsTrue(drain.MoveNext(), "Cleanup returned while the initial native map still owned its inputs");
                    yield return drain.Current;
                }
                initialGate.Set();
                while (drain.MoveNext()) yield return drain.Current;
            }
            finally
            {
                initialGate.Set();
                (drain as IDisposable)?.Dispose();
                room.FirstMapProgress = null; UnityEngine.Object.Destroy(root);
                // TearDown owns disposal after the worker drains, including when
                // the regression fails against the former early-return cleanup.
                computeGate = initialGate; computeEntered = initialEntered;
            }
        }
        // NUnit progress may be lost when the native map worker crashes before
        // it writes XML. Keep stage/format evidence in Unity's immediate log.
        static void Trace(string stage, RoomAcoustics room = null)
        {
            AudioSettings.GetDSPBufferSize(out var length, out var buffers);
            Debug.Log("MAESTRO_ROOM_AUDIO " + stage + " rate=" + AudioSettings.outputSampleRate
                + " block=" + length + " buffers=" + buffers + (room
                    ? " ready=" + room.Ready + " computing=" + room.MapComputing + " geometry=" + room.GeometryCount
                    : ""));
        }
        static void AddWalls(GameObject root)
        {
            var positions = new[] { new Vector3(-2,0,0), new Vector3(2,0,0), new Vector3(0,-1.5f,0), new Vector3(0,1.5f,0), new Vector3(0,0,-2), new Vector3(0,0,2) };
            var scales = new[] { new Vector3(.1f,3,4), new Vector3(.1f,3,4), new Vector3(4,.1f,4), new Vector3(4,.1f,4), new Vector3(4,3,.1f), new Vector3(4,3,.1f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = positions[i]; wall.transform.localScale = scales[i];
                AcousticSurface.Attach(wall, wall.GetComponent<MeshFilter>().sharedMesh, environment: true);
            }
        }
        IEnumerator RenderMix(bool replaceMapDuringSpeech)
        {
            var root = new GameObject("Owned room audio test");
            var listenerObject = new GameObject("Muted room listener", typeof(AudioListener));
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled = new bool[listeners.Length];
            for (int i = 0; i < listeners.Length; i++) { enabled[i] = listeners[i].enabled; listeners[i].enabled = listeners[i].gameObject == listenerObject; }
            float volume = AudioListener.volume; bool paused = AudioListener.pause;
            AudioListener.volume = 1; AudioListener.pause = false;
            var room = root.AddComponent<RoomAcoustics>();
            var audio = root.AddComponent<RoomAudioOutput>();
            audio.Configure(room, listenerObject.GetComponent<AudioListener>());
            // The production tap runs before this test-only silence filter.
            var probe = listenerObject.AddComponent<SpeechListenerProbe>();
            var first = new GameObject("First voice").AddComponent<NativeSpeechOutput>();
            first.transform.SetParent(root.transform, false); first.transform.localPosition = new Vector3(.5f, 0, 1);
            var second = new GameObject("Second voice").AddComponent<NativeSpeechOutput>();
            second.transform.SetParent(root.transform, false); second.transform.localPosition = new Vector3(-.5f, 0, 1);
            try
            {
                Assert.IsTrue(room.Ready, room.Issue);
                AddWalls(root);
                room.Synchronize(); yield return null; room.Synchronize();
                Trace("initial-map-request", room);
                Assert.IsTrue(room.RequestMap(new[] { listenerObject.transform.position }));
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (room.MapComputing && Time.realtimeSinceStartupAsDouble < deadline) { room.Synchronize(); yield return null; }
                Trace("initial-map-result", room);
                Assert.IsFalse(room.MapComputing, "Initial acoustic map did not finish before the test deadline");
                Assert.IsTrue(room.MapReady, room.MapIssue);
                audio.Refresh(); Assert.IsTrue(audio.ReflectionsActive, audio.Issue);
                Trace("voice-start", room);
                long a = first.Begin(24000), b = second.Begin(24000);
                var sourceA = first.GetComponentInChildren<AudioSource>();
                var sourceB = second.GetComponentInChildren<AudioSource>();
                Assert.IsNotNull(sourceA.outputAudioMixerGroup);
                Assert.AreSame(sourceA.outputAudioMixerGroup, sourceB.outputAudioMixerGroup);
                var random = new System.Random(941);
                var pcm = new short[4800]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)random.Next(-1200,1201);
                Assert.IsTrue(first.TryWrite(a, 1, pcm, out var error), error);
                for (int i = 1; i <= 10; i++) Assert.IsTrue(second.TryWrite(b, i, pcm, out error), error);
                Assert.IsTrue(first.MicrophoneSuppressed);
                yield return new WaitForSecondsRealtime(.1f);
                if (replaceMapDuringSpeech)
                {
                    computeGate = new ManualResetEventSlim(false); computeEntered = new ManualResetEventSlim(false);
                    room.FirstMapProgress = () => { computeEntered.Set(); computeGate.Wait(TimeSpan.FromSeconds(10)); };
                    Trace("replacement-map-request", room);
                    Assert.IsTrue(room.RequestMap(new[] { listenerObject.transform.position + Vector3.right * .1f }));
                    deadline = Time.realtimeSinceStartupAsDouble + 3;
                    while (!computeEntered.IsSet && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.IsTrue(computeEntered.IsSet, "Hold the real native computation while both routed sources exist");
                    long before = second.Read().playedSamples;
                    audio.Refresh(); Assert.IsFalse(audio.ReflectionsActive);
                    probe.Reset(); yield return new WaitForSecondsRealtime(.15f);
                    Assert.Greater(second.Read().playedSamples, before);
                    var direct = probe.Read(); Assert.Greater(direct.Left + direct.Right, .00001,
                        "The listener must continue hearing direct sound while reflections are rebuilt");
                    computeGate.Set(); deadline = Time.realtimeSinceStartupAsDouble + 8;
                    while (room.MapComputing && Time.realtimeSinceStartupAsDouble < deadline) { room.Synchronize(); yield return null; }
                    Trace("replacement-map-result", room);
                    Assert.IsFalse(room.MapComputing); Assert.IsTrue(room.MapReady, room.MapIssue);
                    room.FirstMapProgress = null; audio.Refresh(); Assert.IsTrue(audio.ReflectionsActive, audio.Issue);
                }
                else yield return new WaitForSecondsRealtime(.25f);
                Trace("stop-first-voice", room);
                first.Stop();
                Assert.IsTrue(sourceB.isPlaying); Assert.AreEqual(b, second.Read().generation);
                Assert.IsTrue(first.MicrophoneSuppressed, "Stopping one voice must not discard the shared output tail");
                probe.Reset(); yield return new WaitForSecondsRealtime(.25f);
                var mixed = probe.Read();
                Assert.Greater(mixed.Left + mixed.Right, .00001, "The second voice was lost or the native mix did not render");
                Assert.Greater(second.Read().playedSamples, 4800);
                // This deliberately demonstrates the current monitor's full-mix
                // scope. A future ambient loop cannot use it as a Live gate.
                Assert.IsTrue(audio.MicrophoneSuppressed);
                deadline = Time.realtimeSinceStartupAsDouble + 7;
                while (second.Read().playedSamples < 48000 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(48000, second.Read().playedSamples);
                while (audio.MicrophoneSuppressed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsFalse(audio.MonitorFailed);
                Assert.IsFalse(audio.MicrophoneSuppressed, "A drained finite mix never reached quiet");
                second.Stop(); audio.enabled = false;
                Assert.IsFalse(audio.ReflectionsActive); Assert.IsFalse(audio.Routed);
            }
            finally
            {
                Trace("teardown", room);
                computeGate?.Set(); room.FirstMapProgress = null;
                first.Stop(); second.Stop(); audio.enabled = false;
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(listenerObject);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
            yield return null;
            yield return CleanupComputation();
        }
    }
}
