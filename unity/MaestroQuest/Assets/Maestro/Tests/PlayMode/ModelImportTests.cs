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
using Maestro.Quest.Rules;
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
            yield return Capture("imported-model-unity.png", Vector3.zero, .26f);
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
            yield return Capture("external-model-playing.png",Vector3.zero,.23f);
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
            editor.SaveNow(); editor.SendMessage("OnApplicationPause",true); editor.SendMessage("OnApplicationPause",false); yield return null;
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
            yield return null; yield return Capture("external-maestro-posed.png",Vector3.up*.9f,1.05f,true);
            if (avatar.CustomModel.ClipCount > 0 && avatar.CustomModel.ClipDuration(0) >= .1f)
            {
                var position = avatar.transform.position; var container = avatar.CustomModel.Instance.transform;
                var localPosition = container.localPosition; var localScale = container.localScale;
                avatar.SetEditing(true); avatar.SetWalkClip(0); avatar.SpatialWalk(.65f);
                var first = avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation;
                yield return new WaitForSeconds(.3f);
                Assert.That(avatar.IsImportedClipPlaying,Is.True);
                Assert.That(Quaternion.Angle(first,avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation),Is.GreaterThan(.01f),"Selected walking export must move its leg");
                yield return new WaitForSeconds(1.8f);
                Assert.That(Vector3.Distance(position,avatar.transform.position),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(localPosition,container.localPosition),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(localScale,container.localScale),Is.LessThan(.0001f));
                var hips = avatar.transform.InverseTransformPoint(avatar.PoseRig.Bone(PoseJoint.Hips).position);
                Assert.That(new Vector2(hips.x,hips.z).magnitude,Is.LessThan(.25f),"Clip travel must not bypass room navigation");
                yield return Capture("external-maestro-clip.png",Vector3.up*.9f,1.05f,true);
                avatar.SpatialWalk(0); Assert.That(avatar.IsImportedClipPlaying,Is.False);
                // Exercise the reusable payload through the actual walking owner,
                // including visible deformation rather than only synthetic bones.
                using var motions = new MotionLibrary(directory);
                var extract = motions.ImportAsync(Path.GetFileName(path),asset.Bytes);
                yield return new WaitUntil(() => extract.IsCompleted); Assert.That(extract.Exception,Is.Null);
                var motion = extract.Result.First(x => !x.Short);
                avatar.SetWalkReference(-1,motion.id,motions);
                float deadline = Time.realtimeSinceStartup+15;
                while (avatar.LibraryMotionId != motion.id && Time.realtimeSinceStartup < deadline) { avatar.SpatialWalk(.65f); yield return null; }
                Assert.That(avatar.LibraryMotionId,Is.EqualTo(motion.id),avatar.WalkMotionStatus);
                baked = new Mesh(); var initialMotion = Vertices(); first = avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation;
                for (int frame = 0; frame < 24; frame++) { avatar.SpatialWalk(.65f); yield return null; }
                float motionDisplacement = initialMotion.Zip(Vertices(),(a,b) => Vector3.Distance(a,b)).Max();
                Assert.That(motionDisplacement,Is.GreaterThan(.01f),"Saved walking motion must deform the real Meshy model");
                Assert.That(Quaternion.Angle(first,avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation),Is.GreaterThan(.01f));
                Assert.That(Vector3.Distance(position,avatar.transform.position),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(localPosition,container.localPosition),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(localScale,container.localScale),Is.LessThan(.0001f));
                hips = avatar.transform.InverseTransformPoint(avatar.PoseRig.Bone(PoseJoint.Hips).position);
                Assert.That(new Vector2(hips.x,hips.z).magnitude,Is.LessThan(.25f));
                yield return Capture("external-maestro-library-walk.png",Vector3.up*.9f,1.05f,true);
                UnityEngine.Object.Destroy(baked);
                Debug.Log("MAESTRO_EXTERNAL_LIBRARY_WALK_VERIFIED file="+Path.GetFileName(path)+" vertexDisplacement="+motionDisplacement);
                avatar.SpatialWalk(0); avatar.SetEditing(false); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            }
            Debug.Log("MAESTRO_EXTERNAL_TUTOR_VERIFIED file="+Path.GetFileName(path)+" posedVertexDisplacement="+displacement+" channels="+avatar.PoseRig.Capture().Length);
        }

        [UnityTest] public IEnumerator EmbeddedMaestroClipsPreviewPersistAndRunFromVisualRules()
        {
            root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform,false);
                var collider = go.AddComponent<BoxCollider>(); var item = go.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book"); var tutor = Included("maestro"); var avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory);
            var animations = root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor);
            var imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,animations);
            var rules = root.AddComponent<RuleWorkshop>(); rules.Initialize(editor,directory);
            var runtime = root.AddComponent<RoomRules>(); runtime.Initialize(rules,editor,animations,null,room,null);
            var prepare = imports.PrepareAsync("head-turn.glb",ModelFixture.Mixamo(json => json["animations"][0]["channels"][0]["target"]["node"] = 5));
            yield return new WaitUntil(() => prepare.IsCompleted);
            var use = imports.UseMaestroAsync(); yield return new WaitUntil(() => use.IsCompleted);
            Assert.That(use.Result,Is.True,imports.Status); Assert.That(editor.SelectedId,Is.EqualTo("maestro"));
            Assert.That(editor.SetAvatarWalkClip(0),Is.True); Assert.That(avatar.WalkClip,Is.Zero);
            Assert.That(editor.SetAvatarSize(.35f),Is.True); Assert.That(avatar.transform.localScale.x,Is.EqualTo(.35f));
            editor.Undo(); Assert.That(avatar.transform.localScale.x,Is.EqualTo(1));
            var position = tutor.transform.position; var head = avatar.PoseRig.Bone(PoseJoint.Head);
            imports.Play(); var before = head.rotation;
            yield return new WaitForSeconds(.35f);
            Assert.That(avatar.IsImportedClipPlaying,Is.True); Assert.That(animations.IsImportedPreview,Is.True);
            Assert.That(Quaternion.Angle(before,head.rotation),Is.GreaterThan(10));
            Assert.That(Vector3.Distance(position,tutor.transform.position),Is.LessThan(.001f));
            imports.Stop(); Assert.That(avatar.IsImportedClipPlaying,Is.False); Assert.That(animations.ControlsTarget("maestro"),Is.False);
            animations.PreviewWalk(); Assert.That(avatar.IsImportedClipPlaying,Is.True);
            animations.SendMessage("OnApplicationPause",true); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            animations.SendMessage("OnApplicationPause",false);
            rules.NewSequence();
            for (int i=0;i<Enum.GetValues(typeof(RuleActionKind)).Length && rules.Selected.SimpleSteps()[0].action != RuleActionKind.ImportedClip;i++) rules.CycleAction();
            var step = rules.Selected.SimpleSteps()[0]; Assert.That(step.action,Is.EqualTo(RuleActionKind.ImportedClip));
            Assert.That(step.clipModelHash,Is.EqualTo(avatar.ModelHash)); Assert.That(step.seconds,Is.Zero);
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,runtime.Scheduler.LastError);
            before = head.rotation; yield return new WaitForSeconds(.35f);
            Assert.That(Quaternion.Angle(before,head.rotation),Is.GreaterThan(10));
            imports.Stop(); Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True); yield return new WaitForSecondsRealtime(1.15f);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            editor.SaveNow(); rules.SendMessage("OnApplicationPause",true); rules.SendMessage("OnApplicationPause",false); yield return new WaitForSeconds(.2f);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x => x.id == "maestro").walkClip,Is.EqualTo(1));
            Assert.That(new RuleStorage(directory).Load(out _).sequences.Single().SimpleSteps()[0].clipModelHash,Is.EqualTo(step.clipModelHash));
            imports.DefaultMaestro(); Assert.That(editor.Read("maestro").walkClip,Is.Zero);
            Assert.That(runtime.Trigger(rules.Selected.id),Is.False,"A clip must not silently resolve against another avatar");
            editor.Undo(); yield return new WaitUntil(() => !avatar.ModelBusy);
            Assert.That(avatar.WalkClip,Is.Zero); Assert.That(runtime.Trigger(rules.Selected.id),Is.True,runtime.Scheduler.LastError); runtime.StopAll();
            var replacement = ModelLibrary.Inspect("different.glb",ModelFixture.Mixamo(json => json["animations"][0]["name"] = "Different motion"));
            var saveReplacement = editor.Models.SaveAsync(replacement); yield return new WaitUntil(() => saveReplacement.IsCompleted);
            Assert.That(saveReplacement.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(replacement.Hash),Is.True); Assert.That(avatar.ModelBusy,Is.True);
            rules.CycleGesture(); Assert.That(rules.Selected.SimpleSteps()[0].clipModelHash,Is.Null.Or.Empty,"Loading must not bind the previous rig's clip index to the next model");
            yield return new WaitUntil(() => !avatar.ModelBusy);
            rules.CycleGesture(); Assert.That(rules.Selected.SimpleSteps()[0].clipModelHash,Is.EqualTo(replacement.Hash));
        }

        [UnityTest] public IEnumerator LibraryControlsSaveAndPreviewWithoutAnotherModelOrAutoplay()
        {
            root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform,false);
                var collider = go.AddComponent<BoxCollider>(); var item = go.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book"); var tutor = Included("maestro"); var avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            var editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory);
            var animations = root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor);
            var imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,animations);
            var original = ModelFixture.Mixamo(json => json["animations"][0]["channels"][0]["target"]["node"] = 5);
            var prepare = imports.PrepareAsync("avatar.glb",original); yield return new WaitUntil(() => prepare.IsCompleted);
            var use = imports.UseMaestroAsync(); yield return new WaitUntil(() => use.IsCompleted); Assert.That(use.Result,Is.True,imports.Status);
            var add = imports.SaveMotionsAsync(); yield return new WaitUntil(() => add.IsCompleted); Assert.That(add.Result,Is.True,imports.Status);
            Assert.That(imports.LibraryMode,Is.True); Assert.That(avatar.IsImportedClipPlaying,Is.False,"Saving must not start playback");
            var entry = editor.Motions.List().Single();
            var second = ModelFixture.Mixamo(json => json["animations"][0]["channels"][0]["target"]["node"] = 8);
            prepare = imports.PrepareAsync("another-export.glb",second); yield return new WaitUntil(() => prepare.IsCompleted);
            add = imports.SaveMotionsAsync(); yield return new WaitUntil(() => add.IsCompleted); Assert.That(add.Result,Is.True,imports.Status);
            Assert.That(Directory.GetFiles(Path.Combine(directory,"models"),"*.glb").Length,Is.EqualTo(1),"Motions must not persist another textured model");
            Assert.That(editor.Motions.List().Length,Is.EqualTo(2)); Assert.That(imports.HasPreview,Is.False);
            Assert.That(editor.Snapshot().objects.Count(x => x.kind == RoomObjectKind.ImportedModel),Is.Zero);
            imports.NextClip(); // Return to the first same-named motion's stable identity.
            var play = imports.PlayLibraryAsync(); bool wasPending = !play.IsCompleted; imports.SendMessage("OnApplicationPause",true);
            yield return new WaitUntil(() => play.IsCompleted); if (wasPending) Assert.That(play.Result,Is.False,"Pause during loading must cancel playback");
            Assert.That(avatar.IsImportedClipPlaying,Is.False);
            // Force another cold load so focus loss cannot hide behind an already
            // completed cache hit. No action resumes until the user presses Play.
            editor.Motions.Dispose(); // Recreate the actual editor/library as on restart.
            UnityEngine.Object.Destroy(imports); UnityEngine.Object.Destroy(animations); UnityEngine.Object.Destroy(editor); yield return null;
            foreach (var item in root.GetComponentsInChildren<CreatedRoomObject>()) UnityEngine.Object.Destroy(item.gameObject); yield return null;
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory); editor.Select(tutor);
            animations = root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor);
            imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,animations); imports.ToggleLibrary();
            yield return new WaitUntil(() => !avatar.ModelBusy);
            play = imports.PlayLibraryAsync(); wasPending = !play.IsCompleted; imports.SendMessage("OnApplicationFocus",false);
            yield return new WaitUntil(() => play.IsCompleted); if (wasPending) Assert.That(play.Result,Is.False,"Focus loss during loading must cancel playback");
            Assert.That(avatar.IsImportedClipPlaying,Is.False);
            play = imports.PlayLibraryAsync(); yield return new WaitUntil(() => play.IsCompleted); Assert.That(play.Result,Is.True,imports.Status);
            Assert.That(avatar.IsImportedClipPlaying,Is.True); Assert.That(animations.IsImportedPreview,Is.True);
            var head = avatar.PoseRig.Bone(PoseJoint.Head); var hand = avatar.PoseRig.Bone(PoseJoint.LeftHand);
            var beforeHead = head.rotation; var beforeHand = hand.rotation; yield return new WaitForSeconds(.35f);
            Assert.That(Mathf.Max(Quaternion.Angle(beforeHead,head.rotation),Quaternion.Angle(beforeHand,hand.rotation)),Is.GreaterThan(10));
            imports.StopPreview(); Assert.That(avatar.IsImportedClipPlaying,Is.False); Assert.That(animations.ControlsTarget("maestro"),Is.False); Assert.That(imports.Status,Does.Contain("stopped"));
            string storage = Path.Combine(directory,"motions");
            using var reopened = new MotionLibrary(storage); Assert.That(reopened.Find(entry.id).hash,Is.EqualTo(entry.hash)); Assert.That(reopened.ResidentClipCount,Is.Zero);
            var board = new GameObject("Library controls"); board.transform.SetParent(root.transform,false); board.transform.localPosition = new Vector3(-3,1,0); board.AddComponent<ImportTools>().Build(imports,room);
            yield return null; yield return Capture("motion-library-tools-unity.png",board.transform.position,.53f);
            imports.DefaultMaestro(); Assert.That(imports.Details,Does.Contain("Load a compatible"));
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
            yield return Capture("import-tools-unity.png", board.transform.position, .53f);
            var accept = workshop.AcceptAsync(); yield return new WaitUntil(() => accept.IsCompleted); Assert.That(accept.Result, Is.True, workshop.Status);
            var data = editor.Read(editor.SelectedId); Assert.That(data.kind, Is.EqualTo(RoomObjectKind.ImportedModel)); Assert.That(ModelLibrary.ValidHash(data.modelHash), Is.True);
            var created = editor.Find(data.id).GetComponent<CreatedRoomObject>();
            yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus);
            editor.Erase(); yield return null; Assert.That(editor.Read(data.id), Is.Null);
            editor.Undo(); yield return null;
            created = editor.Find(data.id).GetComponent<CreatedRoomObject>(); yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus); Assert.That(created.Model.IsPlaying, Is.False);
            editor.SaveNow(); editor.SendMessage("OnApplicationPause",true); editor.SendMessage("OnApplicationPause",false); yield return null; UnityEngine.Object.Destroy(workshop); UnityEngine.Object.Destroy(editor); yield return null;
            var document = new RoomStorage(directory).Load(out var error); Assert.That(document, Is.Not.Null, error);
            Assert.That(document.objects.Single(x => x.id == data.id).modelHash, Is.EqualTo(data.modelHash));
            foreach (var value in root.GetComponentsInChildren<CreatedRoomObject>()) UnityEngine.Object.Destroy(value.gameObject); yield return null;
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room, book, maestro, directory);
            created = editor.Find(data.id).GetComponent<CreatedRoomObject>(); yield return new WaitUntil(() => created.Model && created.Model.Ready || created.ModelStatus != "Loading local model…");
            Assert.That(created.Model.Ready, Is.True, created.ModelStatus); Assert.That(created.Model.IsPlaying, Is.False);
        }
        sealed class BatchReadGate : IMotionBatchSource
        {
            public int Count => 2;
            public string Name(int index) => index+".glb";
            public bool Waiting,Released,Disposed;
            public async System.Threading.Tasks.Task<MotionBatchInput> ReadAsync(int index,System.Threading.CancellationToken cancellation)
            {
                if (index == 1 && !Released) { Waiting=true; await System.Threading.Tasks.Task.Delay(-1,cancellation); }
                return new MotionBatchInput(index+".glb",ModelFixture.TranslationMotion("LINEAR",index+1));
            }
            public void Dispose() => Disposed=true;
        }
        (ImportWorkshop,RoomEditor,RoomInteraction) BatchRoom()
        {
            root.AddComponent<XRInteractionManager>(); var room=root.AddComponent<RoomInteraction>();
            RoomItem Included(string name) { var go=new GameObject(name); go.transform.SetParent(root.transform,false); var collider=go.AddComponent<BoxCollider>(); var item=go.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item; }
            var book=Included("book"); var maestro=Included("maestro");
            var editor=root.AddComponent<RoomEditor>(); editor.Initialize(room,book,maestro,directory);
            var workshop=root.AddComponent<ImportWorkshop>(); workshop.Initialize(editor); return (workshop,editor,room);
        }
        [UnityTest] public IEnumerator BatchTraySavesMotionsWithoutCreatingModelsAndRetainsReviewableFailures()
        {
            var (workshop,editor,room)=BatchRoom(); Directory.CreateDirectory(directory);
            string good=Path.Combine(directory,"Animation with a long user chosen name.glb"),bad=Path.Combine(directory,"Incomplete export.glb");
            File.WriteAllBytes(good,ModelFixture.Mixamo()); File.WriteAllText(bad,"incomplete download");
            var batch=workshop.Batches; Assert.That(batch.Prepare(new LocalMotionBatchSource(new[] {good,bad,good})),Is.True);
            int count=editor.Snapshot().objects.Length;
            var board=new GameObject("Batch tools"); board.transform.SetParent(root.transform,false); board.transform.position=new Vector3(4,0,0);
            var tray=board.AddComponent<ImportTools>(); tray.Build(workshop,room);
            var controls=board.GetComponentsInChildren<RuleToolAction>();
            controls.Single(x => x.AccessibleName == "Animation batches").Command();
            Assert.That(controls.Any(x => x.AccessibleName == "Save batch"),Is.True);
            Assert.That(editor.Motions.List().Length,Is.Zero,"Selecting files does not save them");
            batch.NextCategory(); Assert.That(batch.Batch.Category,Is.EqualTo("idle"));
            controls.Single(x => x.AccessibleName == "Save batch").Command();
            yield return new WaitUntil(() => !batch.Busy);
            Assert.That(batch.Batch.Saved,Is.EqualTo(2)); Assert.That(batch.Batch.Failed,Is.EqualTo(1)); Assert.That(batch.Batch.Results[1].Name,Is.EqualTo("Incomplete export.glb"));
            Assert.That(editor.Motions.List().Length,Is.EqualTo(1)); Assert.That(editor.Motions.ResidentClipCount,Is.Zero);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count)); Assert.That(root.GetComponentsInChildren<ImportedModel>().Length,Is.Zero);
            batch.PreviousResult(); yield return Capture("batch-import-failure-unity.png",board.transform.position,.53f);
            File.WriteAllBytes(bad,ModelFixture.TranslationMotion("STEP"));
            controls.Single(x => x.AccessibleName == "Retry failed").Command(); yield return new WaitUntil(() => !batch.Busy);
            Assert.That(batch.Batch.Saved,Is.EqualTo(3)); Assert.That(batch.Batch.Failed,Is.Zero); Assert.That(editor.Motions.List().Length,Is.EqualTo(2));
            yield return Capture("batch-import-results-unity.png",board.transform.position,.53f);
            batch.Clear(); Assert.That(batch.Batch,Is.Null); Assert.That(editor.Motions.List().Length,Is.EqualTo(2)); Assert.That(File.Exists(good),Is.True);
            controls.Single(x => x.AccessibleName == "Models").Command(); Assert.That(controls.Any(x => x.AccessibleName == "Use Maestro"),Is.True);
        }
        [UnityTest] public IEnumerator PausingBatchCancelsProviderReadAndRequiresExplicitResume()
        {
            var (workshop,editor,_)=BatchRoom(); var source=new BatchReadGate(); var batch=workshop.Batches;
            Assert.That(batch.Prepare(source),Is.True); var task=batch.SaveAsync(); yield return new WaitUntil(() => source.Waiting);
            Assert.That(workshop.Busy,Is.True); batch.SendMessage("OnApplicationPause",true);
            yield return new WaitUntil(() => task.IsCompleted); Assert.That(task.Exception,Is.Null); Assert.That(batch.Batch.Saved,Is.EqualTo(1)); Assert.That(batch.Batch.Pending,Is.EqualTo(1));
            Assert.That(workshop.Busy,Is.False); batch.SendMessage("OnApplicationPause",false); yield return null;
            Assert.That(editor.Motions.List().Length,Is.EqualTo(1),"Returning focus must not resume importing by itself");
            source.Released=true; task=batch.SaveAsync(); yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(batch.Batch.Saved,Is.EqualTo(2)); Assert.That(editor.Motions.List().Length,Is.EqualTo(2));
            batch.Clear(); Assert.That(source.Disposed,Is.True);
        }
        static IEnumerator Capture(string name, Vector3 center, float size, bool front = false)
        {
            string output = Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE"); if (string.IsNullOrEmpty(output)) yield break;
            Directory.CreateDirectory(output); var go = new GameObject("Import verification camera", typeof(Camera)); var camera = go.GetComponent<Camera>();
            var texture = new RenderTexture(1400, 1400, 24); var pixels = new Texture2D(1400, 1400, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.transform.position = center + (front ? Vector3.forward*3 : Vector3.back); camera.transform.LookAt(center); camera.orthographic = true; camera.orthographicSize = size; camera.nearClipPlane = .01f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f,.91f,.87f,1); camera.targetTexture = texture; camera.enabled = false;
                // Offscreen text first becomes visible here. Let dynamic-font atlas
                // and TextMesh UV updates settle before retaining visual evidence.
                camera.Render(); yield return null; camera.Render();
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0,0,1400,1400),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(pixels); }
        }
    }
}
