// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceRuntimeTests
    {
        GameObject root;string directory,epoch,objectId,sequenceId;RoomEditor editor;RuleWorkshop workshop;RoomRules runtime;
        AnimationWorkshop animations;MovementControls controls;RoomPhysicsWorld physics;MaestroAvatar avatar;RecipeObject recipe;
        RoomRuntimeGate gate;IDisposable review;ControllerFrame frame;
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroRuntimeHold-"+Guid.NewGuid().ToString("N"));epoch=Path.Combine(directory,"action-epochs",Guid.NewGuid().ToString("N"));
            objectId=Guid.NewGuid().ToString("N");sequenceId=Guid.NewGuid().ToString("N");
            var document=new RoomDocument {version=2,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book,position=new Vector3(0,1,1)},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro,position=new Vector3(1,0,1)},new RoomObjectData {id=objectId,kind=RoomObjectKind.Assembly,position=new Vector3(2,1,1),recipe=RecipeTemplates.BoxRobot(true)}}};
            document.objects[2].recipe.playing=true;Assert.That(new RoomStorage(directory).Save(document,out var error),Is.True,error);
            var sequence=new RuleSequence {id=sequenceId,name="Wave robot",program=BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.RecipeAnimation,targetId=objectId,seconds=10,loop=true})};
            var rules=new RuleDocument {sequences=new[]{sequence},bindings=new[]{new RuleBinding {id=Guid.NewGuid().ToString("N"),sequenceId=sequenceId,trigger=RuleEventKind.Speaking,cooldown=.25f},new RuleBinding {id=Guid.NewGuid().ToString("N"),sequenceId=sequenceId,sourceId=objectId,trigger=RuleEventKind.ItemTapped,cooldown=.25f}}};
            Assert.That(new RuleStorage(directory).Save(rules,out error),Is.True,error);
            var preferences=new ControllerPreferences();preferences.buttons[0]=new ControllerBinding {command=ControllerCommand.Sequence,sequenceId=sequenceId};Assert.That(new ControllerPreferenceStorage(directory).Save(preferences,out error),Is.True,error);
            gate=new RoomRuntimeGate();review=gate.Hold("Review imported workspace before starting activity");
            root=new GameObject("Held workspace test");root.AddComponent<XRInteractionManager>();var room=root.AddComponent<RoomInteraction>();var viewer=new GameObject("Viewer");viewer.transform.SetParent(root.transform,false);room.Viewer=viewer.transform;
            RoomItem Item(string name){var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);var collider=obj.AddComponent<BoxCollider>();var item=obj.AddComponent<RoomItem>();item.Configure(new[]{collider});room.Register(item);return item;}
            var book=Item("book");var maestro=Item("maestro");avatar=maestro.gameObject.AddComponent<MaestroAvatar>();physics=root.AddComponent<RoomPhysicsWorld>();physics.SetSurfaces(true,"Aligned test room");
            editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,maestro,directory,physics,gate,epoch);recipe=editor.Find(objectId).GetComponent<RecipeObject>();
            animations=root.AddComponent<AnimationWorkshop>();animations.Initialize(editor);workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor);
            runtime=root.AddComponent<RoomRules>();runtime.Initialize(workshop,editor,animations,null,room,null);
            controls=root.AddComponent<MovementControls>();controls.Initialize(room,editor,animations,null,runtime,workshop,null,null,()=>true,()=>frame);
            frame=new ControllerFrame {leftTracked=true,rightTracked=true};
        }
        JObject Request()=>new() {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=objectId,["seconds"]=10,["loop"]=true,["source"]=new JObject {["kind"]="recipe"},["channel"]="wholeTarget"}}};
        void FocusRoundTrip(){foreach(var owner in new MonoBehaviour[]{editor,runtime,controls,physics,avatar}){owner.SendMessage("OnApplicationPause",true);owner.SendMessage("OnApplicationFocus",false);owner.SendMessage("OnApplicationPause",false);owner.SendMessage("OnApplicationFocus",true);}}
        [UnityTest] public IEnumerator LoadedContentStaysStillUntilHoldReleasedAndNewIntentArrives()
        {
            Assert.That(workshop.Snapshot().sequences.Single().id,Is.EqualTo(sequenceId));Assert.That(controls.Preferences.buttons[0].sequenceId,Is.EqualTo(sequenceId));
            string documents=JsonUtility.ToJson(editor.Snapshot());var part=recipe.Part(editor.Read(objectId).recipe.tracks[0].part);var rotation=part.localRotation;
            var animator=avatar.GetComponentInChildren<Animator>();var joint=avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperArm);var pose=joint.localRotation;
            for(int i=0;i<5;i++){runtime.ObserveSnapshot(new BookSnapshot {activity=i%2==0?"idle":"speaking"});editor.Tapped(editor.Find(objectId));recipe.Restart();avatar.Gesture("Greeting");avatar.SpatialWalk(.5f);yield return null;}
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(recipe.IsPlaying,Is.False);Assert.That(Quaternion.Angle(rotation,part.localRotation),Is.LessThan(.01f));Assert.That(animator.speed,Is.Zero);Assert.That(Quaternion.Angle(pose,joint.localRotation),Is.LessThan(.01f));
            Assert.That(physics.SetRunning(true,out var error),Is.False);StringAssert.Contains("Review",error);Assert.That(avatar.BeginUpperBody("test","Greeting"),Is.False);
            var executions=new RoomExecutions(editor);string id=runtime.Scheduler.Receipts.NextId;Assert.That(executions.Execute(Request(),out error),Is.False);StringAssert.Contains("Review",error);Assert.That(runtime.Scheduler.Receipts.NextId,Is.EqualTo(id));
            Assert.That(runtime.TryReadFact("object.position",1,new JObject {["target"]=objectId},out _),Is.True,"Inspection remains available during review");
            FocusRoundTrip();Assert.That(editor.Ownership.Suspended,Is.True);review.Dispose();review=null;yield return null;
            Assert.That(recipe.IsPlaying,Is.False);Assert.That(physics.Running,Is.False);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(documents));
            runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"First post-review snapshot is a baseline");
            Assert.That(executions.Execute(Request(),out error),Is.True,error);yield return null;Assert.That(recipe.IsPlaying,Is.True);
            Assert.That(File.Exists(Path.Combine(epoch,"action-receipts.v1.json")),Is.True);Assert.That(File.Exists(Path.Combine(directory,"action-receipts.v1.json")),Is.False);
        }
        [UnityTest] public IEnumerator NestedHoldStopsRunningEffectsAndLifecycleCannotReleaseAnotherOwner()
        {
            review.Dispose();review=null;var executions=new RoomExecutions(editor);var request=Request();Assert.That(executions.Execute(request,out var error),Is.True,error);physics.StartPhysics();yield return null;
            Assert.That(recipe.IsPlaying,Is.True);Assert.That(physics.Running,Is.True);using var switching=gate.Hold("Retaining current workspace");using var other=gate.Hold("Review new workspace");
            Assert.That(recipe.IsPlaying,Is.False);Assert.That(physics.Running,Is.False);Assert.That(editor.Ownership.Suspended,Is.True);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            FocusRoundTrip();switching.Dispose();Assert.That(editor.Ownership.Suspended,Is.True);other.Dispose();yield return null;
            Assert.That(editor.Ownership.Suspended,Is.False);Assert.That(recipe.IsPlaying,Is.False);Assert.That(physics.Running,Is.False);
            string id=(string)request["runId"];Assert.That((string)runtime.Scheduler.Invocation(id)["phase"],Is.Not.EqualTo("running"));Assert.That(executions.Execute(request,out _),Is.True);yield return null;Assert.That(recipe.IsPlaying,Is.False,"Duplicate old receipt must not restart effects");
            Assert.That(runtime.Trigger(sequenceId),Is.True);yield return null;Assert.That(recipe.IsPlaying,Is.True);
        }
        [UnityTest] public IEnumerator ControllerButtonsMustBeReleasedAfterHoldBeforeStartingSavedBindings()
        {
            controls.Tick(.02f);frame.x=true;controls.Tick(.02f);controls.ToggleAvatar();controls.ToggleUser();controls.ToggleView();Assert.That(controls.AvatarEnabled||controls.UserEnabled||controls.Virtual,Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);review.Dispose();review=null;controls.Tick(.02f);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            frame.x=false;controls.Tick(.02f);frame.x=true;controls.Tick(.02f);yield return null;Assert.That(recipe.IsPlaying,Is.True);
            using var hold=gate.Hold("Changing workspace");Assert.That(recipe.IsPlaying,Is.False);FocusRoundTrip();hold.Dispose();controls.Tick(.02f);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator NewlyCreatedRecipesAndAuthoringAlsoRespectTheExistingHold()
        {
            var fresh=new GameObject("Late recipe");fresh.transform.SetParent(root.transform,false);var data=editor.Read(objectId).Copy();data.id=Guid.NewGuid().ToString("N");var created=fresh.AddComponent<CreatedRoomObject>();created.Build(data,editor.Models,gate);
            var value=fresh.GetComponent<RecipeObject>();Assert.That(value.IsPlaying,Is.False);value.StartRule(true);yield return null;Assert.That(value.IsPlaying,Is.False);
            editor.Select(editor.Find(objectId));animations.Play();yield return null;Assert.That(animations.IsPlaying,Is.False);StringAssert.Contains("Review",animations.Status);
            review.Dispose();review=null;yield return null;Assert.That(value.IsPlaying,Is.False);value.Restart();Assert.That(value.IsPlaying,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root)UnityEngine.Object.Destroy(root);yield return null;review?.Dispose();review=null;
            if(gate!=null){using(var cleanup=gate.Hold("After owner destruction"))Assert.That(gate.Reason,Is.EqualTo("After owner destruction"));Assert.That(gate.Held,Is.False);}
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
