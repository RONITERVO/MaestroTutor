// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        RoomAcoustics AreaAcoustics()
        {
            var audio = root.AddComponent<RoomAcoustics>(); Assert.That(audio.Ready, Is.True, audio.Issue);
            // The usual fixture was built before installing this optional owner.
            foreach (var surface in root.GetComponentsInChildren<AcousticSurface>()) surface.Bind(surface.Mesh, surface.Scanned);
            for (int i = 0; i < 16; i++) audio.Synchronize();
            Assert.That(audio.OmittedCount, Is.Zero); return audio;
        }
        [UnityTest] public IEnumerator AcousticDormantPlacementEditsPreserveRealListenerObstruction()
        {
            string id = editor.Identity(block); var data = editor.Read(id); data.position = new Vector3(3, 0, 1); data.scale = 4; data.physics = ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision, new[] { data }, Array.Empty<string>(), out var error), Is.True, error);
            string area = NativeAreaFor(id); var audio = AreaAcoustics();
            using var output = new AcousticOutputSample(root, new Vector3(3, 0, 0)); var energy = new double[5];
            for (int stage = 0; stage < energy.Length; stage++)
            {
                if (stage == 1)
                {
                    Assert.That(editor.RetireNativeArea(area, out error), Is.True, error); yield return null;
                    var rename = editor.Read(id); rename.name = "Wall after render retirement";
                    Assert.That(editor.ApplyAgentEdit(editor.Revision, new[] { rename }, Array.Empty<string>(), out error), Is.True, error);
                }
                if (stage == 2)
                {
                    var moved = editor.Read(id); moved.position += Vector3.right * 6;
                    Assert.That(editor.ApplyAgentEdit(editor.Revision, new[] { moved }, Array.Empty<string>(), out error), Is.True, error);
                }
                if (stage == 3) editor.Undo();
                if (stage == 4) Assert.That(editor.ApplyAgentEdit(editor.Revision, Array.Empty<RoomObjectData>(), new[] { id }, out error), Is.True, error);
                audio.Synchronize(); yield return null; yield return output.Sample(value => energy[stage] = value);
            }
            Assert.That(energy[4], Is.GreaterThan(.0001));
            foreach (int stage in new[] { 0, 1, 3 })
            { Assert.That(energy[stage], Is.LessThan(energy[4] * .7)); Assert.That(energy[stage], Is.EqualTo(energy[0]).Within(energy[0] * .05)); }
            Assert.That(energy[2], Is.EqualTo(energy[4]).Within(energy[4] * .05));
        }
        [UnityTest] public IEnumerator AcousticAreaRetirementKeepsGeometryThroughCancellationAndNativeReload()
        {
            string id = editor.Identity(block), area = NativeAreaFor(id); var audio = AreaAcoustics();
            int count = audio.GeometryCount; var old = block.gameObject; string saved = JsonUtility.ToJson(editor.Snapshot());
            Assert.That(editor.RetireNativeArea(area, out var error), Is.True, error);
            Assert.That(audio.RetainedGeometryCount, Is.GreaterThan(0)); Assert.That(audio.GeometryCount, Is.EqualTo(count));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area, cancel.Token), false);
            yield return null; Assert.That((bool)old, Is.False); audio.Synchronize();
            Assert.That(audio.GeometryCount, Is.EqualTo(count)); Assert.That(editor.Find(id), Is.Null);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()), Is.EqualTo(saved));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area)); audio.Synchronize();
            Assert.That(audio.RetainedGeometryCount, Is.Zero); Assert.That(audio.GeometryCount, Is.EqualTo(count));
            Assert.That(audio.OmittedCount, Is.Zero); Assert.That((bool)editor.Find(id), Is.True);
        }
        [UnityTest] public IEnumerator AcousticDormantDefinitionsInvalidateRestoreAndDeleteWithoutGhostGeometry()
        {
            string id = editor.Identity(block), area = NativeAreaFor(id); var audio = AreaAcoustics(); int count = audio.GeometryCount;
            Assert.That(editor.RetireNativeArea(area, out var error), Is.True, error); yield return null;
            var rename = editor.Read(id); rename.name = "Retained wall";
            Assert.That(editor.ApplyAgentEdit(editor.Revision, new[] { rename }, Array.Empty<string>(), out error), Is.True, error);
            audio.Synchronize(); Assert.That(audio.GeometryCount, Is.EqualTo(count)); Assert.That(audio.OmittedCount, Is.Zero);
            var changed = editor.Read(id); changed.physics = changed.physics == ItemPhysics.Fixed ? ItemPhysics.Solid : ItemPhysics.Fixed;
            Assert.That(editor.ApplyAgentEdit(editor.Revision, new[] { changed }, Array.Empty<string>(), out error), Is.True, error);
            audio.Synchronize(); Assert.That(editor.Find(id), Is.Null); Assert.That(audio.GeometryCount, Is.LessThan(count));
            Assert.That(audio.OmittedCount, Is.GreaterThan(0), "Changed dormant input must be unknown, not a ghost wall or a known-clear room");
            editor.Undo(); audio.Synchronize(); Assert.That(audio.GeometryCount, Is.EqualTo(count)); Assert.That(audio.OmittedCount, Is.Zero);
            Assert.That(editor.ApplyAgentEdit(editor.Revision, Array.Empty<RoomObjectData>(), new[] { id }, out error), Is.True, error);
            audio.Synchronize(); Assert.That(audio.RetainedGeometryCount, Is.Zero); Assert.That(audio.GeometryCount, Is.LessThan(count)); Assert.That(audio.OmittedCount, Is.Zero);
        }
        [UnityTest] public IEnumerator AcousticAreaTemporaryDiscardReleasesTheOldCacheAndWorkspaceTeardownDrainsIt()
        {
            yield return WaitForModuleLibrary(); string id = editor.Identity(block), area = NativeAreaFor(id); var audio = AreaAcoustics(); int count = audio.GeometryCount;
            Assert.That(editor.BeginTemporaryRoom(out var error), Is.True, error); while (editor.TemporarySavePending) yield return null;
            Assert.That(editor.RetireNativeArea(area, out error), Is.True, error); yield return null;
            Assert.That(editor.DiscardTemporaryRoom(out error), Is.True, error);
            for (int i = 0; i < 16; i++) audio.Synchronize();
            Assert.That(audio.RetainedGeometryCount, Is.Zero); Assert.That(editor.NativeEntityDormant(id), Is.True); Assert.That(editor.Find(id), Is.Null);
            Assert.That(audio.GeometryCount, Is.LessThan(count), "Restoration must release the temporary acoustic cache");
            yield return ColdAcousticSettled(audio);
            Assert.That(editor.Find(id), Is.Null); Assert.That(audio.RetainedGeometryCount, Is.GreaterThan(0)); Assert.That(audio.GeometryCount, Is.EqualTo(count));
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            for (int i = 0; i < 16; i++) audio.Synchronize();
            Assert.That(audio.GeometryCount, Is.EqualTo(count)); Assert.That(audio.OmittedCount, Is.Zero);
            Assert.That(editor.RetireNativeArea(area, out error), Is.True, error); yield return null;
            Assert.That(audio.RetainedGeometryCount, Is.GreaterThan(0));
            UnityEngine.Object.Destroy(editor); yield return null; Assert.That(audio.RetainedGeometryCount, Is.Zero);
        }
        [UnityTest] public IEnumerator AcousticAreaKeepsImportedGeometryWithoutKeepingTheNativeModelOrMeshes()
        {
            var before = ImportedModel.LiveBudget; var asset = ModelLibrary.Inspect("retained-acoustics.glb", ModelFixture.Create());
            var save = editor.Models.SaveAsync(asset); yield return new WaitUntil(() => save.IsCompleted); Assert.That(save.Exception, Is.Null);
            var create = editor.CreateImportedModelAsync(asset.Hash, CancellationToken.None); yield return new WaitUntil(() => create.IsCompleted);
            Assert.That(create.Exception, Is.Null); string id = create.Result, area = NativeAreaFor(id);
            var view = editor.Find(id).GetComponent<CreatedRoomObject>(); var model = view.Model;
            // The creation also has a shared built-in grab/proxy mesh. Only
            // importer-owned renderer meshes must be destroyed with this model.
            var meshes = model.Instance.Renderers.Select(r => r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh).Where(m => m).Distinct().ToArray();
            Assert.That(meshes, Is.Not.Empty);
            var audio = AreaAcoustics(); int geometry = audio.GeometryCount;
            Assert.That(ImportedModel.LiveBudget, Is.Not.EqualTo(before));
            Assert.That(editor.RetireNativeArea(area, out var error), Is.True, error);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((!ImportedModel.LiveBudget.Equals(before) || model) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That((bool)model, Is.False); Assert.That(ImportedModel.LiveBudget, Is.EqualTo(before));
            Assert.That(meshes.All(mesh => !mesh), Is.True, "Audio cache must not retain importer-owned Unity meshes");
            audio.Synchronize(); Assert.That(audio.RetainedGeometryCount, Is.GreaterThan(0)); Assert.That(audio.GeometryCount, Is.EqualTo(geometry));
            audio.enabled = false; audio.enabled = true; audio.Synchronize();
            Assert.That(audio.GeometryCount, Is.EqualTo(geometry)); Assert.That(audio.OmittedCount, Is.Zero);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area)); audio.Synchronize();
            Assert.That(audio.RetainedGeometryCount, Is.Zero); Assert.That(audio.GeometryCount, Is.EqualTo(geometry));
            Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().ModelGeometryReady, Is.True);
        }
        [UnityTest] public IEnumerator AcousticAreaRefusesUnpreparedInputWithoutRetiringTheNativeObject()
        {
            string id = editor.Identity(block), area = NativeAreaFor(id); var audio = AreaAcoustics();
            var surface = block.GetComponentInChildren<AcousticSurface>(); var mesh = surface.Mesh;
            var bad = new Mesh(); bad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.right * 2 }; bad.triangles = new[] { 0, 1, 2 };
            try
            {
                surface.Bind(bad); audio.Synchronize(); int count = audio.GeometryCount;
                Assert.That(editor.RetireNativeArea(area, out _), Is.False); Assert.That(editor.Find(id), Is.EqualTo(block));
                Assert.That(audio.RetainedGeometryCount, Is.Zero); Assert.That(audio.GeometryCount, Is.EqualTo(count));
                surface.Bind(mesh); audio.Synchronize(); Assert.That(editor.RetireNativeArea(area, out var error), Is.True, error);
            }
            finally { UnityEngine.Object.Destroy(bad); }
            yield return null;
        }
    }
    // Measures the native listener mix; the probe mutes it before device output.
    sealed class AcousticOutputSample : IDisposable
    {
        readonly GameObject listenerObject;
        readonly AudioListener[] listeners;
        readonly bool[] enabled;
        readonly float volume;
        readonly bool paused;
        readonly SpeechListenerProbe probe;
        readonly NativeSpeechOutput speech;
        internal AcousticOutputSample(GameObject root, Vector3 listenerPosition)
        {
            listenerObject = new GameObject("Muted acoustic sample", typeof(AudioListener), typeof(SpeechListenerProbe));
            listenerObject.transform.position = listenerPosition; probe = listenerObject.GetComponent<SpeechListenerProbe>();
            listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None); enabled = listeners.Select(x => x.enabled).ToArray();
            foreach (var listener in listeners) listener.enabled = listener.gameObject == listenerObject;
            volume = AudioListener.volume; paused = AudioListener.pause; AudioListener.volume = 1; AudioListener.pause = false;
            var emitter = new GameObject("Acoustic test voice"); emitter.transform.SetParent(root.transform, false); emitter.transform.position = listenerPosition + Vector3.forward * 2;
            speech = emitter.AddComponent<NativeSpeechOutput>();
        }
        internal IEnumerator Sample(Action<double> receive)
        {
            probe.Reset(); long generation = speech.Begin(24000); var random = new System.Random(125);
            try
            {
                for (int packet = 0; packet < 4; packet++)
                {
                    var pcm = new short[4800]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)random.Next(-1000, 1001);
                    Assert.That(speech.TryWrite(generation, packet + 1, pcm, out var issue), Is.True, issue);
                }
                double end = Time.realtimeSinceStartupAsDouble + 8;
                while (speech.Read().playedSamples < 19200 && Time.realtimeSinceStartupAsDouble < end) yield return null;
                Assert.That(speech.Read().playedSamples, Is.EqualTo(19200)); var result = probe.Read(); receive(result.Left + result.Right);
            }
            finally { speech.Stop(); }
            yield return null;
        }
        public void Dispose()
        {
            if (speech) { speech.Stop(); UnityEngine.Object.Destroy(speech.gameObject); }
            UnityEngine.Object.Destroy(listenerObject);
            for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
            AudioListener.volume = volume; AudioListener.pause = paused;
        }
    }
    public sealed partial class RoomAcousticsTests
    {
        [UnityTest] public IEnumerator RetainedAcousticReservationsAreBoundedAndAbandonedPreparationReleasesThem()
        {
            var wall = Cube(); room.Synchronize();
            var held = new System.Collections.Generic.List<RoomAcoustics.AcousticResidency>();
            try
            {
                for (int i = 0; i < RoomAcoustics.MaximumVirtualSurfaces; i++)
                { Assert.That(room.TryPrepareResidency(wall.gameObject, root.transform, out var lease, out var error), Is.True, error); held.Add(lease); }
                Assert.That(room.TryPrepareResidency(wall.gameObject, root.transform, out var refused, out var issue), Is.False);
                Assert.That(refused, Is.Null); Assert.That(issue, Does.Contain("budget")); Assert.That(room.GeometryCount, Is.EqualTo(1));
                foreach (var lease in held) lease.Dispose(); held.Clear();
                Assert.That(room.RetainedGeometryCount, Is.Zero);
                Assert.That(room.TryPrepareResidency(wall.gameObject, root.transform, out var retry, out var retryError), Is.True, retryError);
                using (retry) { Assert.That(retry.Commit(), Is.True); retry.Restore(wall.gameObject); }
                room.Synchronize(); Assert.That(room.GeometryCount, Is.EqualTo(1)); Assert.That(room.RetainedGeometryCount, Is.Zero);
            }
            finally { foreach (var lease in held) lease.Dispose(); }
            yield return null;
        }
        [UnityTest] public IEnumerator RetainedAcousticWallPreservesActualDspAfterViewAndMeshDestructionAndAudioReset()
        {
            var frame = new GameObject("Authored world frame"); frame.transform.SetParent(root.transform, false);
            var wall = Cube(); wall.transform.SetParent(frame.transform, false); wall.transform.localPosition = Vector3.forward;
            wall.transform.localScale = new Vector3(4, 4, .1f);
            var mesh = UnityEngine.Object.Instantiate(wall.Mesh); wall.Bind(mesh);
            var listenerObject = new GameObject("Muted retained listener", typeof(AudioListener), typeof(SpeechListenerProbe));
            var probe = listenerObject.GetComponent<SpeechListenerProbe>();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None); var enabled = listeners.Select(x => x.enabled).ToArray();
            foreach (var listener in listeners) listener.enabled = listener.gameObject == listenerObject;
            float volume = AudioListener.volume; bool paused = AudioListener.pause; AudioListener.volume = 1; AudioListener.pause = false;
            var emitter = new GameObject("Retained obstruction speech"); emitter.transform.SetParent(root.transform, false); emitter.transform.position = new Vector3(0, 0, 2);
            var speech = emitter.AddComponent<NativeSpeechOutput>(); RoomAcoustics.AcousticResidency retained = null;
            double[] energy = new double[6];
            using var audioTrace = new AudioConfigurationTrace();
            try
            {
                for (int stage = 0; stage < energy.Length; stage++)
                {
                    if (stage == 1)
                    {
                        Assert.That(room.TryPrepareResidency(wall.gameObject, frame.transform, out retained, out var error), Is.True, error);
                        Assert.That(retained.Commit(), Is.True); UnityEngine.Object.Destroy(wall.gameObject); UnityEngine.Object.Destroy(mesh); yield return null;
                        Assert.That((bool)wall, Is.False); Assert.That((bool)mesh, Is.False);
                    }
                    if (stage == 2)
                    {
                        Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True); yield return null;
                        double deadline = Time.realtimeSinceStartupAsDouble + 5; while (!room.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    }
                    if (stage == 3) frame.transform.position = Vector3.right * 6;
                    if (stage == 4) frame.transform.position = Vector3.zero;
                    if (stage == 5) { retained.Dispose(); retained = null; }
                    room.Synchronize(); yield return null; Assert.That(room.Ready, Is.True, room.Issue);
                    Assert.That(room.GeometryCount, Is.EqualTo(stage == 5 ? 0 : 1));
                    probe.Reset(); long generation = speech.Begin(24000); var random = new System.Random(125);
                    for (int packet = 0; packet < 4; packet++)
                    {
                        var pcm = new short[4800]; for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)random.Next(-1000, 1001);
                        Assert.That(speech.TryWrite(generation, packet + 1, pcm, out var issue), Is.True, issue);
                    }
                    double end = Time.realtimeSinceStartupAsDouble + 8;
                    while (speech.Read().playedSamples < 19200 && speech.Read().generation == generation && Time.realtimeSinceStartupAsDouble < end) yield return null;
                    Assert.That(speech.Read().generation, Is.EqualTo(generation), audioTrace.Describe(speech, "retained wall stage " + stage + " interrupted"));
                    Assert.That(speech.Read().playedSamples, Is.EqualTo(19200), audioTrace.Describe(speech, "retained wall stage " + stage)); var result = probe.Read(); energy[stage] = result.Left + result.Right;
                    speech.Stop(); yield return null;
                }
                Assert.That(energy[5], Is.GreaterThan(.0001));
                foreach (int stage in new[] { 0, 1, 2, 4 })
                { Assert.That(energy[stage], Is.LessThan(energy[5] * .7)); Assert.That(energy[stage], Is.EqualTo(energy[0]).Within(energy[0] * .05)); }
                Assert.That(energy[3], Is.EqualTo(energy[5]).Within(energy[5] * .05));
            }
            finally
            {
                retained?.Dispose(); speech.Stop(); room.enabled = false; UnityEngine.Object.Destroy(listenerObject);
                if (mesh) UnityEngine.Object.Destroy(mesh);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i]) listeners[i].enabled = enabled[i];
                AudioListener.volume = volume; AudioListener.pause = paused;
            }
        }
    }
    public sealed partial class RoomAcousticMapTests
    {
        [UnityTest] public IEnumerator RetainedAcousticDisposalDrainsTheActualMapWorkerBeforeNativeRelease()
        {
            Box(); HoldCompute(); Assert.That(room.RequestMap(new[] { Vector3.zero }), Is.True); yield return NativeComputationEntered();
            var wall = root.GetComponentInChildren<AcousticSurface>();
            Assert.That(room.TryPrepareResidency(wall.gameObject, root.transform, out var retained, out var error), Is.True, error);
            using (retained)
            {
                Assert.That(retained.Commit(), Is.True); UnityEngine.Object.Destroy(wall.gameObject); yield return null;
                retained.Dispose(); room.Synchronize(); Assert.That(room.GeometryCount, Is.EqualTo(6), "Native worker still owns its input");
                Assert.That(room.RetainedGeometryCount, Is.Zero); gate.Set(); yield return Finish();
                Assert.That(room.MapReady, Is.False); Assert.That(room.GeometryCount, Is.EqualTo(5));
            }
        }
        [UnityTest] public IEnumerator RetainedAcousticInputSurvivesRenderRetirementDuringNativeMapWork()
        {
            Box(); HoldCompute(); Assert.That(room.RequestMap(new[] { Vector3.zero }), Is.True); yield return NativeComputationEntered();
            var wall = root.GetComponentInChildren<AcousticSurface>();
            Assert.That(room.TryPrepareResidency(wall.gameObject, root.transform, out var retained, out var error), Is.True, error);
            using (retained)
            {
                Assert.That(retained.Commit(), Is.True); UnityEngine.Object.Destroy(wall.gameObject); yield return null;
                room.Synchronize(); Assert.That(room.GeometryCount, Is.EqualTo(6)); Assert.That(room.RetainedGeometryCount, Is.EqualTo(1));
                gate.Set(); yield return Finish(); Assert.That(room.MapReady, Is.True, room.MapIssue);
                retained.Dispose(); room.Synchronize(); Assert.That(room.MapReady, Is.False); Assert.That(room.GeometryCount, Is.EqualTo(5));
            }
        }
    }
}
