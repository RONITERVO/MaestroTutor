// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    public sealed class LibraryRuleAndWalkTests
    {
        GameObject root,anchor;
        RoomEditor editor;
        AnimationWorkshop authoring;
        MaestroAvatar avatar;
        RuleWorkshop rules;
        RoomRules runtime;
        MotionEntry greeting,gait;
        string directory;
        float captureDelta;
        static byte[] Clip(int node,string name) => ModelFixture.Mixamo(json => { json["animations"][0]["name"] = name; json["animations"][0]["channels"][0]["target"]["node"] = node; });
        static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup+10;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(),Is.True,"The requested library operation did not finish");
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f/72;
            directory = Path.Combine(Path.GetTempPath(),"MaestroLibraryRules-"+Guid.NewGuid().ToString("N"));
            root = new GameObject("Saved motion rules test"); root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform,false); var collider = go.AddComponent<BoxCollider>(); collider.size = Vector3.one*.1f;
                var item = go.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book"); var tutor = Included("maestro"); avatar = tutor.gameObject.AddComponent<MaestroAvatar>();
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,tutor,directory); editor.Select(tutor);
            authoring = root.AddComponent<AnimationWorkshop>(); authoring.Initialize(editor);
            rules = root.AddComponent<RuleWorkshop>(); rules.Initialize(editor,directory);
            anchor = new GameObject("Left controller anchor"); anchor.transform.SetParent(root.transform,false); anchor.transform.position = new Vector3(2,1,1);
            runtime = root.AddComponent<RoomRules>(); runtime.Initialize(rules,editor,authoring,null,room,null,_ => anchor.transform);
            var bytes = Clip(5,"Greeting"); var asset = ModelLibrary.Inspect("avatar.glb",bytes);
            var save = editor.Models.SaveAsync(asset); yield return Until(() => save.IsCompleted); Assert.That(save.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True); yield return Until(() => !avatar.ModelBusy); Assert.That(avatar.CustomModel,Is.Not.Null,avatar.ModelStatus);
            var add = editor.Motions.ImportAsync("greeting.glb",bytes); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); greeting = add.Result.Single();
            add = editor.Motions.ImportAsync("walk.glb",Clip(12,"Walking")); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); gait = add.Result.Single();
            rules.NewSequence(); rules.AssignLibraryMotion(greeting.id);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root); Time.captureDeltaTime = captureDelta; yield return null; yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        [UnityTest] public IEnumerator SavedMotionUsesTheSameControllerButtonAndTutorStateRulesAndKeepsItsIdentityAfterRename()
        {
            rules.ToggleWhileState(); rules.AddBinding(); rules.AddButton(ButtonMount.LeftController); yield return null;
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking" });
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1)); yield return Until(() => avatar.LibraryMotionId == greeting.id || runtime.Scheduler.LastError != null);
            Assert.That(runtime.Scheduler.LastError,Is.Null); var head = avatar.PoseRig.Bone(PoseJoint.Head); var initial = head.rotation;
            yield return new WaitForSeconds(.35f); Assert.That(Quaternion.Angle(initial,head.rotation),Is.GreaterThan(10));
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var button = root.GetComponentInChildren<RuleButton>(); var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor; Physics.SyncTransforms();
            var ray = new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(0,ray),Is.False); Assert.That(router.Begin(1,ray),Is.True); router.End(1,ray);
            yield return Until(() => avatar.LibraryMotionId == greeting.id); runtime.StopAll(); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var rename = editor.Motions.UpdateAsync(greeting.id,"My greeting",new[] { "greeting" },true); yield return Until(() => rename.IsCompleted); Assert.That(rename.Exception,Is.Null);
            Assert.That(rules.Selected.steps[0].motionId,Is.EqualTo(greeting.id)); Assert.That(rules.Summary,Does.Contain("My greeting"));
            rules.AssignLibraryMotion(gait.id); rules.Undo(); Assert.That(rules.Selected.steps[0].motionId,Is.EqualTo(greeting.id));
            rules.SendMessage("OnApplicationPause",true); var restored = new RuleStorage(directory).Load(out var error); Assert.That(error,Is.Null);
            Assert.That(restored.sequences.Single().steps.Single().motionId,Is.EqualTo(greeting.id)); Assert.That(restored.buttons.Single().mount,Is.EqualTo(ButtonMount.LeftController));
        }
        [UnityTest] public IEnumerator SavedObjectMotionRunsAndStopsWithoutMovingItsRoomPlacement()
        {
            var imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var prepare = imports.PrepareAsync("animated-object.glb",Clip(5,"Greeting")); yield return Until(() => prepare.IsCompleted); Assert.That(prepare.Exception,Is.Null);
            var accept = imports.AcceptAsync(); yield return Until(() => accept.IsCompleted); Assert.That(accept.Result,Is.True,imports.Status);
            var item = editor.Find(editor.SelectedId); var created = item.GetComponent<CreatedRoomObject>();
            yield return Until(() => created.Model && created.Model.Ready); var node = created.Model.Instance.Nodes[5];
            var rotation = node.localRotation; var position = item.transform.position;
            rules.UseTarget(); rules.AssignLibraryMotion(greeting.id);
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True,runtime.Scheduler.LastError);
            yield return Until(() => runtime.Scheduler.PreparingCount == 0); Assert.That(runtime.Scheduler.LastError,Is.Null);
            // Object rule sampling and its scheduler use the unscaled clock.
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(Quaternion.Angle(rotation,node.localRotation),Is.GreaterThan(10));
            Assert.That(Vector3.Distance(position,item.transform.position),Is.LessThan(.0001f));
            runtime.StopAll(); Assert.That(Quaternion.Angle(rotation,node.localRotation),Is.LessThan(.01f));
        }
        [UnityTest] public IEnumerator LoadingRuleCancelsOnFocusLossAndMissingOrIncompatibleMotionsCannotRun()
        {
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True); runtime.SendMessage("OnApplicationFocus",false);
            var probe = editor.Motions.AcquireAsync(greeting.id,greeting.rigHash); yield return Until(() => probe.IsCompleted); Assert.That(probe.Exception,Is.Null); probe.Result.Dispose(); yield return null;
            Assert.That(avatar.IsImportedClipPlaying,Is.False); Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            runtime.SendMessage("OnApplicationFocus",true); yield return null; Assert.That(avatar.IsImportedClipPlaying,Is.False);
            File.Delete(Path.Combine(directory,"motions",gait.hash+".motion.glb")); rules.AssignLibraryMotion(gait.id);
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True); yield return Until(() => runtime.Scheduler.RunningCount == 0);
            Assert.That(runtime.Scheduler.LastError,Does.Contain("missing")); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var add = editor.Motions.ImportAsync("different-rig.glb",ModelFixture.Create()); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null);
            rules.AssignLibraryMotion(add.Result.Single().id); Assert.That(runtime.Trigger(rules.Selected.id),Is.False); Assert.That(runtime.Scheduler.LastError,Does.Contain("compatible"));
        }
        [UnityTest] public IEnumerator SavedWalkAnimatesTheRigWithoutTravelAndSurvivesCompatibleModelChangesUndoAndReload()
        {
            Assert.That(editor.SetAvatarWalkMotion(gait.id),Is.True); var position = avatar.transform.position;
            authoring.PreviewWalk(); yield return Until(() => avatar.LibraryMotionId == gait.id || avatar.WalkMotionStatus?.Contains("unavailable") == true);
            Assert.That(avatar.LibraryMotionId,Is.EqualTo(gait.id),avatar.WalkMotionStatus);
            var leg = avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg); var rotation = leg.rotation;
            yield return new WaitForSeconds(.35f); Assert.That(Quaternion.Angle(rotation,leg.rotation),Is.GreaterThan(10));
            Assert.That(Vector3.Distance(position,avatar.transform.position),Is.LessThan(.0001f)); authoring.Stop(); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var replacement = ModelLibrary.Inspect("compatible-export.glb",Clip(8,"Other export")); var save = editor.Models.SaveAsync(replacement); yield return Until(() => save.IsCompleted);
            Assert.That(editor.SetMaestroModel(replacement.Hash),Is.True); yield return Until(() => !avatar.ModelBusy); Assert.That(avatar.WalkMotionId,Is.EqualTo(gait.id));
            authoring.PreviewWalk(); yield return Until(() => avatar.LibraryMotionId == gait.id); authoring.Stop();
            editor.Undo(); yield return Until(() => !avatar.ModelBusy); Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(gait.id));
            editor.Undo(); Assert.That(editor.Read("maestro").walkMotionId,Is.Null.Or.Empty); editor.Redo();
            editor.SaveNow(); editor.SendMessage("OnApplicationPause",true);
            var saved = new RoomStorage(directory).Load(out var error); Assert.That(error,Is.Null); Assert.That(saved.version,Is.EqualTo(2)); Assert.That(saved.objects.Single(x => x.id == "maestro").walkMotionId,Is.EqualTo(gait.id));
            var add = editor.Motions.ImportAsync("unavailable.glb",Clip(13,"Lower leg")); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); var missing = add.Result.Single();
            Assert.That(editor.SetAvatarWalkMotion(missing.id),Is.True); File.Delete(Path.Combine(directory,"motions",missing.hash+".motion.glb"));
            authoring.PreviewWalk(); yield return Until(() => avatar.WalkMotionStatus?.Contains("missing") == true);
            Assert.That(avatar.LibraryMotionId,Is.Null); Assert.That(authoring.ControlsTarget("maestro"),Is.True,"The included gait remains available");
            authoring.Stop(); yield return null; Assert.That(avatar.IsImportedClipPlaying,Is.False);
        }
    }
}
