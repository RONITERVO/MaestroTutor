// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class RoomAudioOutputTests
    {
        [UnityTest] public IEnumerator OwnedReflectionMixRendersAndStoppingOneVoicePreservesAnother()
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
                var positions = new[] { new Vector3(-2,0,0), new Vector3(2,0,0), new Vector3(0,-1.5f,0), new Vector3(0,1.5f,0), new Vector3(0,0,-2), new Vector3(0,0,2) };
                var scales = new[] { new Vector3(.1f,3,4), new Vector3(.1f,3,4), new Vector3(4,.1f,4), new Vector3(4,.1f,4), new Vector3(4,3,.1f), new Vector3(4,3,.1f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
                    wall.transform.localPosition = positions[i]; wall.transform.localScale = scales[i];
                    AcousticSurface.Attach(wall, wall.GetComponent<MeshFilter>().sharedMesh, environment: true);
                }
                room.Synchronize(); yield return null; room.Synchronize();
                Assert.IsTrue(room.RequestMap(new[] { listenerObject.transform.position }));
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (room.MapComputing && Time.realtimeSinceStartupAsDouble < deadline) { room.Synchronize(); yield return null; }
                Assert.IsTrue(room.MapReady, room.MapIssue);
                audio.Refresh(); Assert.IsTrue(audio.ReflectionsActive, audio.Issue);
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
                yield return new WaitForSecondsRealtime(.35f);
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
                first.Stop(); second.Stop(); audio.enabled = false;
                UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(listenerObject);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
            yield return null;
        }
    }
}
