// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class RoomAcousticsTests
    {
        GameObject root;
        RoomAcoustics room;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Owned acoustic scene"); room = root.AddComponent<RoomAcoustics>();
            Assert.IsTrue(room.Ready, room.Issue);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (root) UnityEngine.Object.Destroy(root); yield return null; }
        AcousticSurface Cube(string name = "Wall", bool scanned = false)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(root.transform, false);
            return AcousticSurface.Attach(cube, cube.GetComponent<MeshFilter>().sharedMesh, scanned);
        }
        sealed class ScanSource : IRoomSceneSource, IScannedAcousticSource
        {
            public bool Supported => true;
            public bool Tracked { get; set; } = true;
            public ScannedAcousticMesh[] Meshes = Array.Empty<ScannedAcousticMesh>();
            public System.Threading.Tasks.Task<bool> Permission() => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<bool> Scan() => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<bool> Load() => System.Threading.Tasks.Task.FromResult(true);
            public bool TryRead(out ScannedAcousticMesh[] meshes) { meshes = Meshes; return true; }
        }
        [UnityTest] public IEnumerator ScannedGeometryNeedsAcceptedTrackedDataButNotPhysicsOrVisibleOverlays()
        {
            var world = root.AddComponent<RoomPhysicsWorld>(); var scan = root.AddComponent<ScannedRoom>(); scan.Initialize(world);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
            wall.GetComponent<Collider>().enabled = false; wall.GetComponent<Renderer>().enabled = false;
            var source = new ScanSource { Meshes = new[] { new ScannedAcousticMesh(wall, wall.GetComponent<MeshFilter>().sharedMesh) } };
            scan.SetSourceForTests(source); scan.SetAcousticSourceForTests(source);
            scan.SynchronizeAcousticScan(); room.Synchronize(); Assert.AreEqual(0, room.GeometryCount, "Unaccepted scan data must not obstruct audio");
            scan.Load(); double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (scan.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(scan.Busy); scan.SynchronizeAcousticScan(); room.Synchronize();
            Assert.IsFalse(world.Running); Assert.IsFalse(world.SurfacesReady); Assert.AreEqual(1, room.GeometryCount);
            scan.ToggleSurfaces(); scan.ToggleSurfaces(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            scan.SetVirtualView(true); scan.SynchronizeAcousticScan(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            source.Tracked = false; scan.SynchronizeAcousticScan(); Assert.AreEqual(0, room.GeometryCount);
            source.Tracked = true; scan.SynchronizeAcousticScan(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            source.Meshes = Array.Empty<ScannedAcousticMesh>(); scan.SynchronizeAcousticScan(); Assert.AreEqual(0, room.GeometryCount);
            source.Meshes = new[] { new ScannedAcousticMesh(wall, wall.GetComponent<MeshFilter>().sharedMesh) };
            scan.SynchronizeAcousticScan(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            scan.SendMessage("OnApplicationFocus", false); Assert.AreEqual(0, room.GeometryCount);
            scan.SendMessage("OnApplicationFocus", true); scan.SynchronizeAcousticScan(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            scan.enabled = false; Assert.AreEqual(0, room.GeometryCount);
        }
        [UnityTest] public IEnumerator PoseInvalidationAvailabilityAndOwnerLifetimeReleaseNativeGeometry()
        {
            var surface = Cube(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            surface.GetComponent<Collider>().enabled = false; surface.GetComponent<Renderer>().enabled = false;
            room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            surface.transform.localScale = Vector3.zero; room.Synchronize(); Assert.AreEqual(0, room.GeometryCount);
            Assert.That(surface.Issue, Does.Contain("transform"));
            surface.transform.localScale = Vector3.one; room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            surface.SetAvailable(false); Assert.AreEqual(0, room.GeometryCount);
            surface.SetAvailable(true); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            room.enabled = false; Assert.IsFalse(room.Ready); Assert.AreEqual(0, room.GeometryCount);
            room.enabled = true; room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            root.SendMessage("OnApplicationFocus", false); Assert.AreEqual(0, room.GeometryCount);
            root.SendMessage("OnApplicationFocus", true); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            surface.gameObject.SetActive(false); Assert.AreEqual(0, room.GeometryCount);
            surface.gameObject.SetActive(true); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            surface.Bind(null); Assert.AreEqual(0, room.GeometryCount);
            yield return null;
        }
        [UnityTest] public IEnumerator NativePrimitiveShapesUseTheirReadableGeometry()
        {
            foreach (var primitive in new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder })
            {
                var shape = GameObject.CreatePrimitive(primitive); shape.transform.SetParent(root.transform, false);
                var surface = AcousticSurface.Attach(shape, shape.GetComponent<MeshFilter>().sharedMesh);
                room.Synchronize(); Assert.IsNull(surface.Issue, primitive + ": " + surface.Issue);
            }
            Assert.AreEqual(3, room.GeometryCount); yield return null;
        }
        [UnityTest] public IEnumerator SourceValidationRejectsUnreadableOversizedAndDegenerateMeshesBeforeNativeUpload()
        {
            var surface = Cube(); room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.right * 2 }; mesh.triangles = new[] { 0, 1, 2 };
                surface.Bind(mesh); Assert.AreEqual(0, room.GeometryCount); room.Synchronize();
                Assert.AreEqual(0, room.GeometryCount); Assert.That(surface.Issue, Does.Contain("degenerate"));
                mesh.vertices = new Vector3[RoomAcoustics.MaximumMeshVertices + 1]; surface.Bind(mesh); room.Synchronize();
                Assert.AreEqual(0, room.GeometryCount); Assert.That(surface.Issue, Does.Contain("budget"));
                mesh.Clear(); mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
                mesh.UploadMeshData(true); surface.Bind(mesh); room.Synchronize(); Assert.AreEqual(0, room.GeometryCount);
                Assert.That(surface.Issue, Does.Contain("unreadable")); Assert.AreEqual(1, room.OmittedCount);
            }
            finally { UnityEngine.Object.Destroy(mesh); }
            yield return null;
        }
        [UnityTest] public IEnumerator SurfaceBudgetRetriesAfterDeletionAndScanHasAnIndependentReservation()
        {
            var items = new AcousticSurface[RoomAcoustics.MaximumScannedSurfaces + 1];
            for (int i = 0; i < items.Length; i++) items[i] = Cube("Scanned wall " + i, true);
            // Four uploads per frame; the fixed bound also applies to manual sync.
            room.Synchronize(); Assert.AreEqual(4, room.GeometryCount);
            for (int i = 0; i < 40; i++) room.Synchronize();
            Assert.AreEqual(RoomAcoustics.MaximumScannedSurfaces, room.GeometryCount);
            Assert.That(items[^1].Issue, Does.Contain("budget"));
            var virtualWall = Cube("Virtual wall"); room.Synchronize();
            Assert.AreEqual(RoomAcoustics.MaximumScannedSurfaces + 1, room.GeometryCount);
            items[0].gameObject.SetActive(false); room.Synchronize();
            Assert.IsNull(items[^1].Issue); Assert.AreEqual(RoomAcoustics.MaximumScannedSurfaces + 1, room.GeometryCount);
            yield return null;
        }
        [UnityTest] public IEnumerator RecipeRebuildAndAnimatedPartsUseActualMeshesWithoutProxyBoxes()
        {
            var go = new GameObject("Window frame"); go.transform.SetParent(root.transform, false);
            var recipe = go.AddComponent<RecipeObject>();
            var data = new RoomRecipe { parts = new[] { new RecipePart { id = "left", shape = "box", size = new Vector3(.1f, 2, .1f), position = Vector3.left },
                new RecipePart { id = "right", shape = "box", size = new Vector3(.1f, 2, .1f), position = Vector3.right } } };
            Assert.IsTrue(recipe.Apply(data)); room.Synchronize(); Assert.AreEqual(2, room.GeometryCount);
            Assert.AreEqual(24, room.TriangleCount);
            recipe.Part("left").localRotation = Quaternion.Euler(0, 25, 0); room.Synchronize(); Assert.AreEqual(2, room.GeometryCount);
            data.parts = new[] { data.parts[0] }; Assert.IsTrue(recipe.Apply(data));
            Assert.AreEqual(0, room.GeometryCount, "Old topology must leave immediately, before deferred GameObject destruction");
            room.Synchronize(); Assert.AreEqual(1, room.GeometryCount);
            go.SetActive(false); Assert.AreEqual(0, room.GeometryCount); yield return null;
        }
        [UnityTest] public IEnumerator RealListenerMixTracksHiddenWallMovementDeletionAndAcousticOwnerDisabling()
        {
            var listenerObject = new GameObject("Muted acoustic listener", typeof(AudioListener), typeof(SpeechListenerProbe));
            var probe = listenerObject.GetComponent<SpeechListenerProbe>();
            var existing = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled = new bool[existing.Length];
            for (int i = 0; i < existing.Length; i++) { enabled[i] = existing[i].enabled; existing[i].enabled = existing[i].gameObject == listenerObject; }
            float volume = AudioListener.volume; bool paused = AudioListener.pause;
            AudioListener.volume = 1; AudioListener.pause = false;
            var emitter = new GameObject("Acoustic speech", typeof(NativeSpeechOutput)); emitter.transform.SetParent(root.transform, false);
            emitter.transform.position = new Vector3(0, 0, 2); var output = emitter.GetComponent<NativeSpeechOutput>();
            var wall = Cube(); wall.transform.localPosition = new Vector3(0, 0, 1); wall.transform.localScale = new Vector3(4, 4, .1f);
            wall.GetComponent<Renderer>().enabled = false; wall.GetComponent<Collider>().enabled = false;
            double[] energies = new double[7];
            try
            {
                for (int stage = 0; stage < energies.Length; stage++)
                {
                    if (stage == 0) wall.SetAvailable(false);
                    if (stage == 1) wall.SetAvailable(true);
                    if (stage == 2) wall.transform.position = new Vector3(6, 0, 1);
                    if (stage == 3) wall.transform.position = new Vector3(0, 0, 1);
                    if (stage == 4) room.enabled = false;
                    if (stage == 5) room.enabled = true;
                    if (stage == 6) { wall.gameObject.SetActive(false); UnityEngine.Object.Destroy(wall.gameObject); }
                    room.Synchronize(); yield return null;
                    probe.Reset(); long generation = output.Begin(24000);
                    var source = emitter.GetComponentInChildren<MetaXRAudioSource>();
                    Assert.AreEqual(room.Ready, source.EnableAcoustics);
                    Assert.AreEqual(-60, source.ReverbSendDb);
                    var random = new System.Random(125);
                    for (int packet = 0; packet < 4; packet++)
                    {
                        var pcm = new short[4800]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)random.Next(-1000, 1001);
                        Assert.IsTrue(output.TryWrite(generation, packet + 1, pcm, out var error), error);
                    }
                    double deadline = Time.realtimeSinceStartupAsDouble + 8;
                    while (output.Read().playedSamples < 19200 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.AreEqual(19200, output.Read().playedSamples);
                    var result = probe.Read(); energies[stage] = result.Left + result.Right;
                    Debug.Log($"MAESTRO_OWNED_ACOUSTICS: stage={stage}; energy={energies[stage]}; geometry={room.GeometryCount}");
                    output.Stop(); yield return null;
                }
                Assert.Greater(energies[0], .0001);
                foreach (int stage in new[] { 1, 3, 5 })
                {
                    Assert.Less(energies[stage], energies[0] * .7, "The hidden/non-colliding wall must still obstruct sound");
                    Assert.Greater(energies[stage], energies[0] * .01, "Conversational occlusion should preserve speech audibility");
                }
                foreach (int stage in new[] { 2, 4, 6 }) Assert.That(energies[stage], Is.EqualTo(energies[0]).Within(energies[0] * .05));
            }
            finally
            {
                output.Stop(); room.enabled = false; UnityEngine.Object.Destroy(listenerObject);
                for (int i = 0; i < existing.Length; i++) if (existing[i]) existing[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
        }
    }
}
