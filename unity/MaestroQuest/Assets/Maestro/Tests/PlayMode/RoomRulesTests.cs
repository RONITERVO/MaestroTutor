// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class RoomRulesTests
    {
        GameObject root, leftAnchor, rightAnchor;
        XRInteractionManager manager;
        RoomEditor editor;
        AnimationWorkshop animations;
        RuleWorkshop workshop;
        RoomRules runtime;
        RoomItem block;
        string directory, sequenceId;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),"MaestroRoomRules-"+Guid.NewGuid().ToString("N"));
            root = new GameObject("Room rules test"); manager = root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var value = new GameObject(name); value.transform.SetParent(root.transform,false); var collider = value.AddComponent<BoxCollider>(); collider.size = Vector3.one*.1f;
                var item = value.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            var book = Included("book"); var avatar = Included("maestro"); editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,avatar,directory);
            var blockData = editor.Snapshot().objects.First(x => x.kind == RoomObjectKind.Block); block = editor.Find(blockData.id); editor.Select(block);
            editor.SaveAnimation(blockData.id,new RoomMotion { frames = new[] { new MotionFrame { position = blockData.position },new MotionFrame { time = 2,position = blockData.position + Vector3.right*.4f } } },null,false);
            animations = root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor);
            workshop = root.AddComponent<RuleWorkshop>(); workshop.Initialize(editor,directory);
            leftAnchor = new GameObject("Left controller pose"); leftAnchor.transform.SetParent(root.transform,false); leftAnchor.transform.position = new Vector3(2,1,1);
            rightAnchor = new GameObject("Right controller pose"); rightAnchor.transform.SetParent(root.transform,false); rightAnchor.transform.position = new Vector3(3,1,1);
            runtime = root.AddComponent<RoomRules>(); runtime.Initialize(workshop,editor,animations,null,room,null,index => { var anchor = index == 0 ? leftAnchor : rightAnchor; return anchor && anchor.activeSelf ? anchor.transform : null; });
            workshop.NewSequence();
            for (int i = 0; i < System.Enum.GetValues(typeof(RuleActionKind)).Length && workshop.Selected.steps[0].action != RuleActionKind.RecordedAnimation; i++) workshop.CycleAction();
            Assert.That(workshop.Selected.steps[0].action,Is.EqualTo(RuleActionKind.RecordedAnimation));
            workshop.UseTarget(); sequenceId = workshop.Selected.id;
            yield return null;
        }
        XRRayInteractor Hand(int index, Vector3 position)
        {
            var hand = new GameObject("Rule test hand"); hand.SetActive(false); hand.transform.SetParent(root.transform,false); hand.transform.position = position;
            hand.AddComponent<ControllerIdentity>().PointerId = index;
            var ray = hand.AddComponent<XRRayInteractor>(); ray.enableUIInteraction = false; ray.interactionManager = manager; ray.keepSelectedTargetValid = true; ray.manipulateAttachTransform = false;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed = true,manualValue = 1 };
            hand.SetActive(true); return ray;
        }
        [UnityTest] public IEnumerator RealButtonAndWebActivityTriggerTheSameRecordedActionAndPauseStopsIt()
        {
            workshop.AddBinding(); workshop.AddButton(ButtonMount.Room);
            yield return null; Physics.SyncTransforms();
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking",audioPaused = true });
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Paused browser snapshots cannot start rules");
            var before = block.transform.localPosition;
            runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking" }); Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Resuming establishes a fresh baseline");
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking" }); Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            yield return new WaitForSeconds(.25f); Assert.That(block.transform.localPosition.x,Is.GreaterThan(before.x+.02f));
            runtime.StopAll(); Assert.That(Vector3.Distance(block.transform.localPosition,before),Is.LessThan(.001f));
            var button = root.GetComponentInChildren<RuleButton>(); var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor;
            var ray = new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(1,ray),Is.True); router.End(1,ray); Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.SendMessage("OnApplicationPause",true); Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            runtime.SendMessage("OnApplicationPause",false); yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator MountedButtonFollowsControllerRejectsItsOwnerAndSavesAdjustedOffset()
        {
            workshop.AddButton(ButtonMount.LeftController); yield return null;
            var button = root.GetComponentInChildren<RuleButton>(); var data = workshop.Snapshot().buttons.Single();
            Assert.That(Vector3.Distance(button.transform.position,leftAnchor.transform.TransformPoint(data.position)),Is.LessThan(.001f));
            leftAnchor.transform.position += Vector3.up*.2f; yield return null;
            Assert.That(Vector3.Distance(button.transform.position,leftAnchor.transform.TransformPoint(data.position)),Is.LessThan(.001f));
            var own = Hand(0,button.transform.position-Vector3.forward*.25f); var other = Hand(1,button.transform.position-Vector3.forward*.25f);
            var item = button.GetComponent<RoomItem>(); Assert.That(button.Process(own,item.Grab),Is.False); Assert.That(button.Process(other,item.Grab),Is.True);
            var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor; Physics.SyncTransforms();
            var ray = new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(0,ray),Is.False); Assert.That(router.Begin(1,ray),Is.True); router.End(1,ray);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1)); runtime.StopAll();
            own.gameObject.SetActive(false); manager.SelectEnter((IXRSelectInteractor)other,item.Grab); yield return new WaitForSeconds(.15f);
            other.transform.position += Vector3.up*.06f; yield return null; yield return null;
            other.gameObject.SetActive(false); yield return null;
            var adjusted = workshop.Snapshot().buttons.Single(); Assert.That(adjusted.position.y,Is.EqualTo(data.position.y+.06f).Within(.01f));
            leftAnchor.SetActive(false); yield return null; Assert.That(button.GetComponent<Collider>().enabled,Is.False);
            leftAnchor.SetActive(true); yield return null; Assert.That(button.GetComponent<Collider>().enabled,Is.True);
            workshop.SendMessage("OnApplicationPause",true);
            var restored = new RuleStorage(directory).Load(out var error); Assert.That(error,Is.Null); Assert.That(restored.buttons.Single().position,Is.EqualTo(adjusted.position));
        }
        [UnityTest] public IEnumerator AuthoringAndUserGripTakePriorityAndRuleEditsUndoAsOneDocument()
        {
            var originalMotion = editor.Read(editor.SelectedId).motion;
            animations.ToggleRecord(); Assert.That(runtime.Trigger(sequenceId),Is.False); animations.Stop();
            editor.SaveAnimation(editor.SelectedId,originalMotion,null,false);
            Assert.That(runtime.Trigger(sequenceId),Is.True); yield return new WaitForSeconds(.2f);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Grip must interrupt a still-running take");
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(originalMotion.frames[0].position.x+.01f));
            var current = block.transform.position; var hand = Hand(1,current-Vector3.forward*.2f);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(Vector3.Distance(current,block.transform.position),Is.LessThan(.01f),"Grabbing does not snap an animated object back");
            hand.gameObject.SetActive(false); yield return null;
            workshop.AddBinding(); workshop.AddButton(ButtonMount.Room); int count = workshop.Snapshot().buttons.Length;
            workshop.DeleteSequence(); Assert.That(workshop.Snapshot().sequences,Is.Empty); workshop.Undo();
            Assert.That(workshop.Snapshot().buttons.Length,Is.EqualTo(count)); Assert.That(workshop.Snapshot().bindings.Length,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SharedRuleEditsPreserveStepIdentityRejectStaleAndUndoWholeBatch()
        {
            int revision=workshop.Revision;var sequence=workshop.Selected;string originalStep=sequence.steps[0].id;
            sequence.steps=sequence.steps.Append(new RuleStep {id="",action=RuleActionKind.Wait,seconds=1}).ToArray();
            var edit=new RuleRequest {action="edit",revision=revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence},new RuleEdit {kind="button",target=sequence.id,mount=ButtonMount.LeftController}}};
            Assert.That(workshop.Execute(edit,out var error,out _),Is.True,error);Assert.That(workshop.Revision,Is.EqualTo(revision+1));
            var saved=workshop.Selected;Assert.That(saved.steps[0].id,Is.EqualTo(originalStep));Assert.That(RuleDocument.IsId(saved.steps[1].id),Is.True);
            Assert.That(workshop.Execute(edit,out _,out _),Is.False,"An old draft cannot replace a newer edit");
            string before=JsonUtility.ToJson(workshop.Snapshot());saved.steps[0].seconds=-1;
            edit.revision=workshop.Revision;edit.edits[0].sequence=saved;Assert.That(workshop.Execute(edit,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(before));
            Assert.That(workshop.Execute(new RuleRequest {action="undo",revision=workshop.Revision},out _,out _),Is.True);
            Assert.That(workshop.Selected.steps.Length,Is.EqualTo(1));Assert.That(workshop.Snapshot().buttons.Length,Is.Zero);
            Assert.That(workshop.Execute(new RuleRequest {action="redo",revision=workshop.Revision},out _,out _),Is.True);
            var restored=workshop.Selected;string added=restored.steps[1].id;restored.steps=restored.steps.Reverse().ToArray();
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=restored}}},out _,out _),Is.True);
            Assert.That(workshop.Selected.steps[0].id,Is.EqualTo(added));workshop.AddStep();Assert.That(workshop.Observe().revision,Is.GreaterThan(revision));
            workshop.SendMessage("OnApplicationPause",true);var loaded=new RuleStorage(directory).Load(out _);Assert.That(loaded.sequences.First(x=>x.id==sequence.id).steps[0].id,Is.EqualTo(added));
            yield return null;
        }
        [UnityTest] public IEnumerator AgentRuleRecipePlaybackSharesTutorEventsAndActualButtonInput()
        {
            var agent=new RoomAgentExecutor(editor);var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;
            Assert.That(agent.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[]{new RoomAgentCommand {action="create",kind="recipe",reference="robot",name="Robot",recipe=recipe}}},out var error,out var created),Is.True,error);
            string target=created.Single();var geometry=editor.Find(target).GetComponent<RecipeObject>();Assert.That(geometry.IsPlaying,Is.False);
            var sequence=new RuleSequence {id="",name="Wave with speech",steps=new[]{new RuleStep {id="",action=RuleActionKind.RecipeAnimation,targetId=target,seconds=.3f,loop=true}}};
            var request=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{
                new RuleEdit {kind="save",reference="wave",sequence=sequence},
                new RuleEdit {kind="bind",binding=new RuleBinding {sequenceId="wave",trigger=RuleEventKind.Speaking,stopOnExit=true}},
                new RuleEdit {kind="button",target="wave",mount=ButtonMount.Room}}};
            Assert.That(agent.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=request}}},out error,out created),Is.True,error);
            string id=created.Single();yield return null;runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);Assert.That(workshop.Observe().running.Single().stepId,Is.EqualTo(workshop.Selected.steps[0].id));
            var arm=geometry.Part("RightUpperArm");var rotation=arm.localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(rotation,arm.localRotation),Is.GreaterThan(.1f));
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});Assert.That(geometry.IsPlaying,Is.False);
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);Assert.That(geometry.IsPlaying,Is.True);
            yield return new WaitForSeconds(.4f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(geometry.IsPlaying,Is.False);
            Assert.That(editor.Read(target).recipe.playing,Is.False,"Rule playback does not rewrite the saved recipe flags");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
