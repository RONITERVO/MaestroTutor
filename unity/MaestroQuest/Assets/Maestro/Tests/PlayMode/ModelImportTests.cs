// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    public sealed class ModelImportTests
    {
        GameObject root;
        string directory;
        [UnitySetUp] public IEnumerator Setup() { root = new GameObject("Imported model test"); directory = Path.Combine(Path.GetTempPath(), "MaestroImportTests-" + Guid.NewGuid().ToString("N")); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { UnityEngine.Object.Destroy(root); yield return null; yield return null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        [UnityTest] public IEnumerator LoadsActualGeometryStylesItAndPlaysOnlyWhenRequested()
        {
            var model = root.AddComponent<ImportedModel>(); var task = model.LoadAsync(ModelLibrary.Inspect("triangle.glb", ModelFixture.Create()));
            yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception, Is.Null);
            Assert.That(model.Ready, Is.True); Assert.That(model.IsPlaying, Is.False); Assert.That(model.ClipCount, Is.EqualTo(1));
            Assert.That(model.LocalBounds.size.y, Is.EqualTo(.35f).Within(.001f));
            Assert.That(model.Instance.Renderers[0].sharedMaterial.shader.name, Is.EqualTo("Maestro/Watercolor"));
            Capture("imported-model-unity.png", Vector3.zero, .26f);
            var node = model.Instance.Nodes[0]; var rotation = node.localRotation;
            model.Play(0, true); yield return new WaitForSeconds(.2f);
            Assert.That(Quaternion.Angle(rotation, node.localRotation), Is.GreaterThan(5));
            model.SendMessage("OnApplicationPause", true); yield return null;
            Assert.That(model.IsPlaying, Is.False); Assert.That(Quaternion.Angle(rotation, node.localRotation), Is.LessThan(.1f));
        }
        [UnityTest] public IEnumerator ImportsVrmHumanoidAsOwnedRoomObject()
        {
            var model = root.AddComponent<ImportedModel>(); var task = model.LoadAsync(ModelLibrary.Inspect("skeleton.vrm", ModelFixture.Create(avatar: true)));
            yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception, Is.Null);
            Assert.That(model.Ready, Is.True); var animator = model.Instance.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null); Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Head), Is.Not.Null); Assert.That(model.IsPlaying, Is.False);
        }
        [UnityTest] public IEnumerator PreviewAcceptEraseUndoAndReloadKeepLocalModelAndNeverAutoplay()
        {
            root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name) { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root.transform, false); var item = go.AddComponent<RoomItem>(); item.Configure(new[] { go.GetComponent<Collider>() }); room.Register(item); return item; }
            var book = Included("book"); var maestro = Included("maestro");
            var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room, book, maestro, directory);
            var workshop = root.AddComponent<ImportWorkshop>(); workshop.Initialize(editor);
            var prepare = workshop.PrepareAsync("triangle.glb", ModelFixture.Create()); yield return new WaitUntil(() => prepare.IsCompleted);
            Assert.That(workshop.HasPreview, Is.True, workshop.Status); Assert.That(editor.Snapshot().objects.Any(x => x.kind == RoomObjectKind.ImportedModel), Is.False);
            var board = new GameObject("Solid import tools"); board.transform.SetParent(root.transform, false); board.transform.localPosition = new Vector3(4,0,0); board.AddComponent<ImportTools>().Build(workshop, room);
            Capture("import-tools-unity.png", board.transform.position, .4f);
            var accept = workshop.AcceptAsync(); yield return new WaitUntil(() => accept.IsCompleted); Assert.That(accept.Result, Is.True, workshop.Status);
            var data = editor.Read(editor.SelectedId); Assert.That(data.kind, Is.EqualTo(RoomObjectKind.ImportedModel)); Assert.That(ModelLibrary.ValidHash(data.modelHash), Is.True);
            var created = editor.Find(data.id).GetComponent<CreatedRoomObject>();
            yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus);
            editor.Erase(); yield return null; Assert.That(editor.Read(data.id), Is.Null);
            editor.Undo(); yield return null;
            created = editor.Find(data.id).GetComponent<CreatedRoomObject>(); yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus); Assert.That(created.Model.IsPlaying, Is.False);
            editor.SaveNow(); yield return new WaitForSeconds(.2f); UnityEngine.Object.Destroy(workshop); UnityEngine.Object.Destroy(editor); yield return null;
            var document = new RoomStorage(directory).Load(out var error); Assert.That(document, Is.Not.Null, error);
            Assert.That(document.objects.Single(x => x.id == data.id).modelHash, Is.EqualTo(data.modelHash));
            foreach (var value in root.GetComponentsInChildren<CreatedRoomObject>()) UnityEngine.Object.Destroy(value.gameObject); yield return null;
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room, book, maestro, directory);
            created = editor.Find(data.id).GetComponent<CreatedRoomObject>(); yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus); Assert.That(created.Model.IsPlaying, Is.False);
        }
        static void Capture(string name, Vector3 center, float size)
        {
            string output = Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE"); if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output); var go = new GameObject("Import verification camera", typeof(Camera)); var camera = go.GetComponent<Camera>();
            var texture = new RenderTexture(1400, 1400, 24); var pixels = new Texture2D(1400, 1400, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.transform.position = center + Vector3.back; camera.transform.LookAt(center); camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .01f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1); camera.targetTexture = texture; camera.Render();
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1400,1400),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(pixels); }
        }
    }
}
