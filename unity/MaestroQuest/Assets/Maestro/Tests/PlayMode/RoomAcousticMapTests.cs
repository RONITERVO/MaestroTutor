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
    public sealed class RoomAcousticMapTests
    {
        GameObject root;
        RoomAcoustics room;
        ManualResetEventSlim gate, entered;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Acoustic map ownership"); room = root.AddComponent<RoomAcoustics>();
            Assert.IsTrue(room.Ready, room.Issue);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            gate?.Set();
            if (root) UnityEngine.Object.Destroy(root);
            // A cancelled native job retains ownership until it returns. Poll
            // with the actual public owner instead of assuming one frame suffices.
            var next = new GameObject("Cleanup lease check").AddComponent<RoomAcoustics>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!next.Ready && Time.realtimeSinceStartupAsDouble < end) yield return null;
            bool released = next.Ready; UnityEngine.Object.Destroy(next.gameObject); yield return null;
            Assert.IsTrue(released, "Native acoustic ownership was not released");
            gate?.Dispose(); gate = null; entered?.Dispose(); entered = null;
        }
        AcousticSurface Wall(Vector3 position, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = position; wall.transform.localScale = size;
            return AcousticSurface.Attach(wall, wall.GetComponent<MeshFilter>().sharedMesh);
        }
        void Box()
        {
            Wall(new(-2, 0, 0), new(.1f, 3, 4)); Wall(new(2, 0, 0), new(.1f, 3, 4));
            Wall(new(0, -1.5f, 0), new(4, .1f, 4)); Wall(new(0, 1.5f, 0), new(4, .1f, 4));
            Wall(new(0, 0, -2), new(4, 3, .1f)); Wall(new(0, 0, 2), new(4, 3, .1f));
            room.Synchronize(); room.Synchronize();
        }
        IEnumerator Finish()
        {
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (room.MapComputing && Time.realtimeSinceStartupAsDouble < end) { room.Synchronize(); yield return null; }
            Assert.IsFalse(room.MapComputing, "Acoustic computation did not terminate");
        }
        void HoldCompute()
        {
            gate = new ManualResetEventSlim(false); entered = new ManualResetEventSlim(false);
            room.FirstMapProgress = () => { entered.Set(); gate.Wait(TimeSpan.FromSeconds(10)); };
        }
        IEnumerator NativeComputationEntered()
        {
            double end = Time.realtimeSinceStartupAsDouble + 3;
            while (!entered.IsSet && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.IsTrue(entered.IsSet, "The actual native computation must be running before the lifecycle change");
        }
        [UnityTest] public IEnumerator NativeMapBecomesReadyThenMovementAndTrackingLossInvalidateIt()
        {
            root.transform.position = new Vector3(2, 0, 3); Box();
            Assert.IsTrue(room.RequestMap(new[] { root.transform.position }));
            Assert.IsFalse(room.Ready, "Do not use geometry concurrently while the SDK computes its map");
            yield return Finish();
            Assert.IsTrue(room.Ready); Assert.IsTrue(room.MapReady, room.MapIssue);
            root.transform.position += Vector3.forward; room.Synchronize(); Assert.IsFalse(room.MapReady);
            Assert.IsTrue(room.RequestMap(new[] { root.transform.position })); yield return Finish(); Assert.IsTrue(room.MapReady, room.MapIssue);
            root.GetComponentInChildren<AcousticSurface>().SetAvailable(false);
            Assert.IsFalse(room.MapReady); Assert.AreEqual(5, room.GeometryCount);
        }
        [UnityTest] public IEnumerator EditsDuringComputeRetainInputsAndDiscardLateResults()
        {
            Box(); HoldCompute();
            Assert.IsTrue(room.RequestMap(new[] { Vector3.zero }));
            yield return NativeComputationEntered();
            var wall = root.GetComponentInChildren<AcousticSurface>();
            wall.gameObject.SetActive(false);
            Wall(new Vector3(1, 0, 1), Vector3.one);
            room.Synchronize();
            Assert.IsFalse(room.Ready); Assert.AreEqual(6, room.GeometryCount, "Worker inputs stay alive; newly registered geometry is not uploaded yet");
            gate.Set(); yield return Finish();
            Assert.IsTrue(room.Ready); Assert.IsFalse(room.MapReady, "A cancelled scene must never install its late map");
            Assert.AreEqual(6, room.GeometryCount);
            room.FirstMapProgress = null;
            Assert.IsTrue(room.RequestMap(new[] { Vector3.zero })); yield return Finish(); Assert.IsTrue(room.MapReady, room.MapIssue);
        }
        [UnityTest] public IEnumerator DestroyDuringComputeKeepsLeaseUntilInputCleanupCompletes()
        {
            Box(); HoldCompute();
            Assert.IsTrue(room.RequestMap(new[] { Vector3.zero }));
            yield return NativeComputationEntered();
            UnityEngine.Object.Destroy(root); yield return null; root = null;
            var replacement = new GameObject("Next acoustic room").AddComponent<RoomAcoustics>();
            try
            {
                Assert.IsFalse(replacement.Ready, "Destroyed Unity owner must not release native inputs being read by its worker");
                gate.Set(); double end = Time.realtimeSinceStartupAsDouble + 5;
                while (!replacement.Ready && Time.realtimeSinceStartupAsDouble < end) yield return null;
                Assert.IsTrue(replacement.Ready, replacement.Issue);
                Assert.AreEqual(0, replacement.GeometryCount); Assert.IsFalse(replacement.MapReady);
            }
            finally { UnityEngine.Object.Destroy(replacement.gameObject); }
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidAndOverBudgetPointsDoNotStartNativeWork()
        {
            Box();
            foreach (var points in new[] { Array.Empty<Vector3>(), new Vector3[RoomAcousticMapJob.MaximumPoints + 1], new[] { new Vector3(float.NaN, 0, 0) }, new[] { new Vector3(0, 0, 10001) } })
            { Assert.IsFalse(room.RequestMap(points)); Assert.IsTrue(room.Ready); Assert.IsFalse(room.MapComputing); }
            yield return null;
        }
        [UnityTest] public IEnumerator PauseFocusAndAudioResetCancelHeldWorkBeforeReopening()
        {
            Box();
            foreach (string change in new[] { "OnApplicationPause", "OnApplicationFocus", "AudioConfigurationChanged" })
            {
                HoldCompute(); Assert.IsTrue(room.RequestMap(new[] { Vector3.zero }));
                yield return NativeComputationEntered();
                room.SendMessage(change, change == "OnApplicationPause");
                Assert.IsFalse(room.Ready); Assert.AreEqual(0, room.GeometryCount);
                if (change != "AudioConfigurationChanged") room.SendMessage(change, change == "OnApplicationFocus");
                Assert.IsFalse(room.Ready, "Reopening must wait for cancelled native input cleanup");
                gate.Set(); double end = Time.realtimeSinceStartupAsDouble + 5;
                while ((!room.Ready || room.GeometryCount != 6) && Time.realtimeSinceStartupAsDouble < end) yield return null;
                Assert.IsTrue(room.Ready, room.Issue); Assert.AreEqual(6, room.GeometryCount);
                Assert.IsFalse(room.MapComputing); Assert.IsFalse(room.MapReady, "Reopening never adopts the cancelled map");
                room.FirstMapProgress = null;
                gate.Dispose(); gate = null; entered.Dispose(); entered = null;
            }
        }
        [UnityTest] public IEnumerator CooperativeComputeBudgetDiscardsMapAndAllowsAnotherRequest()
        {
            Box(); HoldCompute(); Assert.IsTrue(room.RequestMap(new[] { Vector3.zero }));
            yield return NativeComputationEntered();
            yield return new WaitForSecondsRealtime(1.1f);
            gate.Set(); yield return Finish();
            Assert.IsTrue(room.Ready); Assert.IsFalse(room.MapReady);
            Assert.That(room.MapIssue, Does.Contain("compute budget"));
            room.FirstMapProgress = null;
            Assert.IsTrue(room.RequestMap(new[] { Vector3.zero })); yield return Finish();
            Assert.IsTrue(room.MapReady, room.MapIssue);
        }
        [UnityTest] public IEnumerator NativeSpeechDspContinuesWhileMapCalculationRetainsGeometry()
        {
            Box(); HoldCompute();
            var listener = new GameObject("Muted map-compute listener", typeof(AudioListener), typeof(SpeechListenerProbe));
            var probe = listener.GetComponent<SpeechListenerProbe>();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled = new bool[listeners.Length];
            for (int i = 0; i < listeners.Length; i++) { enabled[i] = listeners[i].enabled; listeners[i].enabled = listeners[i].gameObject == listener; }
            float volume = AudioListener.volume; bool paused = AudioListener.pause;
            AudioListener.volume = 1; AudioListener.pause = false;
            var voice = new GameObject("Continuing speech", typeof(NativeSpeechOutput)); voice.transform.SetParent(root.transform, false);
            voice.transform.localPosition = new Vector3(.7f, 0, .8f); var output = voice.GetComponent<NativeSpeechOutput>();
            try
            {
                long generation = output.Begin(24000); var random = new System.Random(291);
                for (int packet = 0; packet < 8; packet++)
                {
                    var pcm = new short[4800]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)random.Next(-1000, 1001);
                    Assert.IsTrue(output.TryWrite(generation, packet + 1, pcm, out var error), error);
                }
                yield return new WaitForSecondsRealtime(.25f);
                Assert.IsTrue(room.RequestMap(new[] { Vector3.zero })); yield return NativeComputationEntered();
                probe.Reset(); long before = output.Read().playedSamples;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.IsFalse(room.Ready); Assert.IsFalse(voice.GetComponentInChildren<MetaXRAudioSource>().EnableAcoustics);
                Assert.Greater(output.Read().playedSamples, before, "Native audio rendering must advance while room calculations run");
                var signal = probe.Read(); Assert.Greater(signal.Left + signal.Right, .0001, "Measure listener sound, not only submitted PCM");
                gate.Set(); yield return Finish(); Assert.IsTrue(room.Ready);
            }
            finally
            {
                gate.Set(); output.Stop(); UnityEngine.Object.Destroy(listener);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
        }
    }
}
