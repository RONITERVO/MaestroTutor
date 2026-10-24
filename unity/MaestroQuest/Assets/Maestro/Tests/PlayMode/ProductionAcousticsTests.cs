// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Book;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class ProductionAcousticsTests
    {
        GameObject root;
        WorkspaceHost workspace;
        string directory;
        AudioListener[] listeners;
        bool[] listenerEnabled;

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!ReferenceEquals(workspace, null))
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!workspace.Retirement.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(workspace.Retirement.IsCompleted, Is.True, "Production workspace owners must finish before test storage is removed");
            }
            if (listeners != null)
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = listenerEnabled[i];
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest] public IEnumerator ProductionRoomKeepsDirectAcousticsWithoutStartingUnusedReflectionWork()
        {
            // Use the actual application composition with private disposable storage.
            // An isolated RoomAcoustics fixture would miss the startup regression.
            directory = Path.Combine(Application.temporaryCachePath, "maestro-production-acoustics-" + Guid.NewGuid().ToString("N"));
            listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            listenerEnabled = new bool[listeners.Length];
            for (int i = 0; i < listeners.Length; i++) { listenerEnabled[i] = listeners[i].enabled; listeners[i].enabled = false; }
            root = new GameObject("Production acoustic regression"); root.SetActive(false);
            var app = root.AddComponent<MaestroRoom>(); app.ProbeWorkspaceDirectory = directory; root.SetActive(true);
            workspace = root.GetComponent<WorkspaceHost>();
            Assert.That(workspace.Ready, Is.True, workspace.Status);
            var acoustics = root.GetComponent<RoomAcoustics>();
            Assert.That(acoustics.Ready, Is.True, acoustics.Issue);
            Assert.That(root.GetComponentsInChildren<RoomAudioOutput>(true), Is.Empty, "Reflection output awaits mixed-audio Live acceptance");
            Assert.That(root.GetComponentsInChildren<RoomAcousticMapScheduler>(true), Is.Empty, "An unrouted reflection map must not start background native work");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
            wall.transform.localScale = new Vector3(4, 3, .1f);
            wall.GetComponent<Renderer>().enabled = false; wall.GetComponent<Collider>().enabled = false;
            AcousticSurface.Attach(wall, wall.GetComponent<MeshFilter>().sharedMesh, environment: true);
            var emitter = new GameObject("Direct-only speech"); emitter.transform.SetParent(root.transform, false);
            var speech = emitter.AddComponent<NativeSpeechOutput>(); speech.Begin(24000);
            var source = emitter.GetComponentInChildren<MetaXRAudioSource>();
            Assert.That(source.EnableAcoustics, Is.True);
            Assert.That(source.ReverbSendDb, Is.EqualTo(-60));

            // Cross the scheduler's structure/listener settling period with an
            // eligible boundary, checking every frame rather than only at the end.
            double end = Time.realtimeSinceStartupAsDouble + 3.2;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                acoustics.Synchronize();
                Assert.That(acoustics.Ready, Is.True, acoustics.Issue);
                Assert.That(acoustics.BoundaryCount, Is.GreaterThan(0));
                Assert.That(acoustics.MapComputing, Is.False);
                Assert.That(acoustics.MapReady, Is.False);
                Assert.That(source.EnableAcoustics, Is.True);
                yield return null;
            }
            speech.Stop();
        }
    }
}
#endif
