// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Avatar;
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
        float previousCaptureDelta;
        [UnitySetUp] public IEnumerator Setup() { previousCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f/72; root = new GameObject("Imported model test"); directory = Path.Combine(Path.GetTempPath(), "MaestroImportTests-" + Guid.NewGuid().ToString("N")); yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() { UnityEngine.Object.Destroy(root); Time.captureDeltaTime = previousCaptureDelta; yield return null; yield return null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
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
        // An optional local export is deliberately never copied into the project.
        // This checks the actual runtime player and visible deformation, beyond JSON inspection.
        [UnityTest] public IEnumerator SelectedExternalModelLoadsAndPlaysEmbeddedSkeletalAnimationWhenPresent()
        {
            string path = Environment.GetEnvironmentVariable("MAESTRO_EXTERNAL_MODEL");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("No local animated model selected for verification");
            var model = root.AddComponent<ImportedModel>();
            var task = model.LoadAsync(ModelLibrary.Inspect(Path.GetFileName(path),ModelLibrary.ReadBounded(path)));
            yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception,Is.Null);
            Assert.That(model.Ready,Is.True); Assert.That(model.IsPlaying,Is.False);
            var skins = model.Instance.SkinnedMeshRenderers.Where(skin => skin.sharedMesh && skin.sharedMesh.vertexCount > 0).ToArray();
            if (model.ClipCount == 0 || skins.Length == 0) yield break;
            Vector3[] Vertices()
            {
                var result = new System.Collections.Generic.List<Vector3>(); var baked = new Mesh();
                foreach (var skin in skins) { skin.BakeMesh(baked,true); result.AddRange(baked.vertices.Select(skin.transform.TransformPoint)); }
                UnityEngine.Object.Destroy(baked); return result.ToArray();
            }
            var initial = Vertices(); model.Play(0,true); yield return new WaitForSeconds(.18f);
            Assert.That(model.IsPlaying,Is.True); var animated = Vertices();
            float displacement = initial.Zip(animated,(a,b) => Vector3.Distance(a,b)).Max();
            Assert.That(displacement,Is.GreaterThan(.001f),"Embedded playback must change the visible skinned mesh");
            Capture("external-model-playing.png",Vector3.zero,.23f);
            model.Stop(); yield return null;
            Assert.That(model.IsPlaying,Is.False);
            Assert.That(initial.Zip(Vertices(),(a,b) => Vector3.Distance(a,b)).Max(),Is.LessThan(.0001f),"Stop must restore the imported rest pose");
            Debug.Log("MAESTRO_EXTERNAL_MODEL_PLAYED file="+Path.GetFileName(path)+" clips="+model.ClipCount+" displacement="+displacement+
                " texturedMaterials="+model.Instance.Renderers.SelectMany(renderer => renderer.sharedMaterials).Count(material => material && material.mainTexture));
        }
        [UnityTest] public IEnumerator CustomMaestroRetargetsGesturesAndPosesAndSurvivesUndoAndReload() => CheckCustomMaestro(false);
        [UnityTest] public IEnumerator MixamoGlbMaestroRetargetsGesturesPosesAndSurvivesUndoAndReload() => CheckCustomMaestro(true);
        IEnumerator CheckCustomMaestro(bool mixamo)
        {
            root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform,false);
                var collider = go.AddComponent<BoxCollider>(); var item = go.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book"); var tutor = Included("maestro"); var avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory);
            var workshop = root.AddComponent<ImportWorkshop>(); workshop.Initialize(editor);
            var prepare = workshop.PrepareAsync(mixamo ? "custom.glb" : "custom.vrm",mixamo ? ModelFixture.Mixamo() : ModelFixture.Create(avatar:true)); yield return new WaitUntil(() => prepare.IsCompleted);
            var use = workshop.UseMaestroAsync(); yield return new WaitUntil(() => use.IsCompleted);
            Assert.That(use.Result,Is.True,workshop.Status);
            var hash = editor.Read("maestro").modelHash; Assert.That(avatar.ModelHash,Is.EqualTo(hash));
            Assert.That(editor.Snapshot().objects.Count(value => value.kind == RoomObjectKind.ImportedModel),Is.Zero,"Choosing a tutor must not add a room-object copy");
            Assert.That(avatar.PoseRig.Bone(PoseJoint.Head),Is.EqualTo(avatar.CustomModel.Humanoid.GetBoneTransform(HumanBodyBones.Head)));
            var forearm = avatar.PoseRig.Bone(PoseJoint.RightLowerArm); var hand = avatar.PoseRig.Bone(PoseJoint.RightHand);
            float length = Vector3.Distance(forearm.position,hand.position);
            // Loading can finish halfway through the startup greeting. Establish
            // an idle frame so the comparison cannot sample the same wave twice.
            avatar.SetEditing(true); avatar.Gesture("Idle"); yield return null;
            var before = hand.rotation; avatar.Gesture("Greeting");
            yield return new WaitForSeconds(.45f);
            Assert.That(Quaternion.Angle(before,hand.rotation),Is.GreaterThan(3),"Included gesture must reach the imported skeleton");
            Assert.That(Vector3.Distance(forearm.position,hand.position),Is.EqualTo(length).Within(.001f),"Retargeting must preserve proportions");
            avatar.PoseRig.SetManual(true); var head = avatar.PoseRig.Bone(PoseJoint.Head); var headBefore = head.rotation;
            var skin = avatar.CustomModel.Instance.SkinnedMeshRenderers.Single(); var baked = new Mesh(); skin.BakeMesh(baked,true);
            var vertexBefore = skin.transform.TransformPoint(baked.vertices[2]);
            var channelsBefore = avatar.PoseRig.Capture(); avatar.PoseRig.Rotate(PoseJoint.Head,Quaternion.AngleAxis(25,Vector3.right)*headBefore);
            skin.BakeMesh(baked,true);
            Assert.That(Vector3.Distance(vertexBefore,skin.transform.TransformPoint(baked.vertices[2])),Is.GreaterThan(.05f),"The visible skinned mesh must follow the posed joint");
            UnityEngine.Object.Destroy(baked);
            Assert.That(Quaternion.Angle(headBefore,head.rotation),Is.GreaterThan(10));
            var pose = avatar.PoseRig.Capture(); Assert.That(pose.Length,Is.EqualTo(17));
            Assert.That(Quaternion.Angle(channelsBefore.Single(x => x.joint == PoseJoint.Head).rotation,pose.Single(x => x.joint == PoseJoint.Head).rotation),Is.GreaterThan(10));
            editor.SaveAnimation("maestro",null,pose,true); avatar.SetEditing(false);
            workshop.DefaultMaestro(); Assert.That(avatar.CustomModel,Is.Null); Assert.That(editor.Read("maestro").modelHash,Is.Null);
            editor.Undo(); yield return new WaitUntil(() => !avatar.ModelBusy); Assert.That(avatar.ModelHash,Is.EqualTo(hash),avatar.ModelStatus);
            Assert.That(Quaternion.Angle(avatar.PoseRig.Capture().Single(x => x.joint == PoseJoint.Head).rotation,pose.Single(x => x.joint == PoseJoint.Head).rotation),Is.LessThan(.1f));
            editor.SaveNow(); yield return new WaitForSeconds(.2f);
            var saved = new RoomStorage(directory).Load(out var error); Assert.That(saved,Is.Not.Null,error); Assert.That(saved.objects.Single(x => x.id == "maestro").modelHash,Is.EqualTo(hash));
            UnityEngine.Object.Destroy(workshop); UnityEngine.Object.Destroy(editor); UnityEngine.Object.Destroy(avatar); yield return null;
            // Recreate the whole tutor root, as a process restart does.
            room.Unregister(tutor); UnityEngine.Object.Destroy(tutor.gameObject); yield return null;
            tutor = Included("maestro"); avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory);
            yield return new WaitUntil(() => !avatar.ModelBusy); Assert.That(avatar.ModelHash,Is.EqualTo(hash),avatar.ModelStatus);
            Assert.That(avatar.PoseRig.Capture().Length,Is.EqualTo(17));
            var missing = avatar.SetModel(new string('a',64),editor.Models); yield return new WaitUntil(() => missing.IsCompleted);
            Assert.That(missing.Result,Is.False); Assert.That(avatar.CustomModel,Is.Null); Assert.That(avatar.ModelStatus,Does.Contain("Using included Maestro"));
        }

        [UnityTest] public IEnumerator SelectedExternalHumanoidUsesTheRealTutorReplacementAndPosePath()
        {
            if (Environment.GetEnvironmentVariable("MAESTRO_REQUIRE_HUMANOID") != "1") Assert.Ignore("No external humanoid required for this run");
            string path = Environment.GetEnvironmentVariable("MAESTRO_EXTERNAL_MODEL"); Assert.That(path,Is.Not.Empty);
            var library = new ModelLibrary(directory); var asset = ModelLibrary.Inspect(Path.GetFileName(path),ModelLibrary.ReadBounded(path));
            var save = library.SaveAsync(asset); yield return new WaitUntil(() => save.IsCompleted); Assert.That(save.Exception,Is.Null);
            var avatar = root.AddComponent<MaestroAvatar>(); var load = avatar.SetModel(asset.Hash,library);
            yield return new WaitUntil(() => load.IsCompleted); Assert.That(load.Result,Is.True,avatar.ModelStatus);
            Assert.That(avatar.CustomModel.IsHumanoid,Is.True); Assert.That(avatar.CustomModel.IsPlaying,Is.False);
            var hand = avatar.PoseRig.Bone(PoseJoint.RightHand); var elbow = avatar.PoseRig.Bone(PoseJoint.RightLowerArm);
            float length = Vector3.Distance(hand.position,elbow.position);
            avatar.SetEditing(true); avatar.Gesture("Idle"); yield return null; var before = hand.rotation;
            avatar.Gesture("Greeting"); yield return new WaitForSeconds(.45f);
            Assert.That(Quaternion.Angle(before,hand.rotation),Is.GreaterThan(3));
            Assert.That(Vector3.Distance(hand.position,elbow.position),Is.EqualTo(length).Within(.002f));
            var skins = avatar.CustomModel.Instance.SkinnedMeshRenderers; var baked = new Mesh();
            Vector3[] Vertices()
            {
                var vertices = new System.Collections.Generic.List<Vector3>();
                foreach (var skin in skins) { skin.BakeMesh(baked,true); vertices.AddRange(baked.vertices.Select(skin.transform.TransformPoint)); }
                return vertices.ToArray();
            }
            avatar.PoseRig.SetManual(true); var beforePose = Vertices(); var head = avatar.PoseRig.Bone(PoseJoint.Head);
            var visibleBounds = new Bounds(beforePose[0],Vector3.zero); foreach (var point in beforePose) visibleBounds.Encapsulate(point);
            Assert.That(visibleBounds.size.y,Is.InRange(.8f,2.5f),"The fitted tutor mesh must have a usable height: "+visibleBounds);
            Assert.That(visibleBounds.center.magnitude,Is.LessThan(2.5f),"The fitted tutor mesh must remain at its room placement: "+visibleBounds);
            Assert.That(head.position.y,Is.InRange(.6f,2.5f),"Facing alignment must leave the avatar upright");
            Assert.That(Vector3.Dot(avatar.CustomModel.Instance.transform.up,Vector3.up),Is.GreaterThan(.999f));
            avatar.PoseRig.Rotate(PoseJoint.Head,Quaternion.AngleAxis(20,Vector3.right)*head.rotation);
            float displacement = beforePose.Zip(Vertices(),(a,b) => Vector3.Distance(a,b)).Max(); UnityEngine.Object.Destroy(baked);
            Assert.That(displacement,Is.GreaterThan(.01f),"Joint posing must deform the actual external model");
            Assert.That(avatar.PoseRig.Capture().Length,Is.EqualTo(17));
            yield return null; Capture("external-maestro-posed.png",Vector3.up*.9f,1.05f,true);
            Debug.Log("MAESTRO_EXTERNAL_TUTOR_VERIFIED file="+Path.GetFileName(path)+" posedVertexDisplacement="+displacement+" channels="+avatar.PoseRig.Capture().Length);
        }

        [UnityTest] public IEnumerator IncompleteAndAmbiguousNamedRigsRemainObjectsButCannotReplaceMaestro()
        {
            foreach (var name in new[] { "UnknownHand","mixamorig:Hips" })
            {
                var go = new GameObject("Unsupported named rig"); go.transform.SetParent(root.transform,false);
                var model = go.AddComponent<ImportedModel>();
                var task = model.LoadAsync(ModelLibrary.Inspect("unsupported.glb",ModelFixture.Mixamo(json => json["nodes"][8]["name"] = name)));
                yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception,Is.Null);
                Assert.That(model.Ready,Is.True); Assert.That(model.IsHumanoid,Is.False);
                Assert.Throws<ModelImportException>(() => model.FitAsMaestro());
                Assert.That(model.HumanoidIssue,Is.Not.Empty);
                UnityEngine.Object.Destroy(go); yield return null;
            }
        }

        [UnityTest] public IEnumerator AvatarReplacementCancellationCannotInstallAnOlderSelection()
        {
            var avatar = root.AddComponent<MaestroAvatar>(); var library = new ModelLibrary(directory);
            var asset = ModelLibrary.Inspect("avatar.vrm",ModelFixture.Create(avatar:true)); var save = library.SaveAsync(asset); yield return new WaitUntil(() => save.IsCompleted);
            var pending = avatar.SetModel(asset.Hash,library); avatar.SetModel(null,library);
            yield return new WaitUntil(() => pending.IsCompleted); yield return null;
            Assert.That(avatar.ModelHash,Is.Empty); Assert.That(avatar.CustomModel,Is.Null); Assert.That(avatar.ModelBusy,Is.False);
            var plain = ModelLibrary.Inspect("object.glb",ModelFixture.Create()); save = library.SaveAsync(plain); yield return new WaitUntil(() => save.IsCompleted);
            var rejected = avatar.SetModel(plain.Hash,library); yield return new WaitUntil(() => rejected.IsCompleted);
            Assert.That(rejected.Result,Is.False); Assert.That(avatar.ModelHash,Is.Empty);
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
            Capture("import-tools-unity.png", board.transform.position, .53f);
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
        static void Capture(string name, Vector3 center, float size, bool front = false)
        {
            string output = Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE"); if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output); var go = new GameObject("Import verification camera", typeof(Camera)); var camera = go.GetComponent<Camera>();
            var texture = new RenderTexture(1400, 1400, 24); var pixels = new Texture2D(1400, 1400, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.transform.position = center + (front ? Vector3.forward*3 : Vector3.back); camera.transform.LookAt(center); camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .01f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1); camera.targetTexture = texture; camera.Render();
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1400,1400),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(pixels); }
        }
    }
}
