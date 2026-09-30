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
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
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
            var library=root.GetComponent<LibraryBookController>(); if (library) { library.SetVisible(false); yield return Until(() => !library.UsagePending); }
            UnityEngine.Object.Destroy(root); Time.captureDeltaTime = captureDelta; yield return null; yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        [UnityTest] public IEnumerator ImportedWalkAndUpperBodyLayerRetargetTogetherWithoutLosingTheMotionLease()
        {
            avatar.SetEditing(true);avatar.SetWalkReference(-1,gait.id,editor.Motions);
            for(int i=0;i<45;i++) {avatar.SpatialWalk(.65f);yield return null;}
            Assert.That(avatar.LibraryMotionId,Is.EqualTo(gait.id));
            var hand=avatar.PoseRig.Bone(PoseJoint.RightUpperArm);var before=hand.rotation;
            var clipTime=avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation;
            var call=Newtonsoft.Json.Linq.JObject.Parse(@"{'id':'animation.play','version':1,'arguments':{'target':'maestro','source':{'kind':'gesture','gesture':'greeting'},'channel':'upperBody','seconds':10}}");
            Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            for(int i=0;i<30;i++) {avatar.SpatialWalk(.65f);yield return null;}
            Assert.That(avatar.LibraryMotionId,Is.EqualTo(gait.id),"Upper-body sampling must not stop or replace the imported gait");
            Assert.That(Quaternion.Angle(before,hand.rotation),Is.GreaterThan(70),"The imported visible arm follows the composed canonical pose");
            Assert.That(Quaternion.Angle(clipTime,avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg).rotation),Is.GreaterThan(1));
            Assert.That(runtime.Scheduler.CancelInvocation(run,out _),Is.True);Assert.That(avatar.LibraryMotionId,Is.EqualTo(gait.id));
            Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);
            avatar.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",true);
            Assert.That(avatar.UpperBodyActive,Is.False);Assert.That(avatar.LibraryMotionId,Is.Null);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            avatar.SendMessage("OnApplicationPause",false);runtime.SendMessage("OnApplicationPause",false);
            yield return null;Assert.That(avatar.UpperBodyActive,Is.False);Assert.That(avatar.LibraryMotionId,Is.Null,"Resume cannot replay an interrupted layer");
        }
        [UnityTest] public IEnumerator AgentActivityAssignmentsShareBookRevisionAtomicityUndoAndRealPlayback()
        {
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>();book.Initialize(editor,imports,rules);book.SetVisible(true);
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            var profiles=editor.ActivityProfiles;int sceneRevision=editor.Revision,ruleRevision=rules.Revision;
            AvatarActivityEdit Assign(int role,string id) => new() {operation="assign",role=role,choice=new TutorMotionChoice {motionId=id,loop=true,weight=2}};
            RoomAgentRequest Request(string operation,params AvatarActivityEdit[] edits) => new() {version=2,commands=new[] {new RoomAgentCommand {action="avatarActivities",activities=new AvatarActivityRequest {operation=operation,modelHash=avatar.ModelHash,revision=profiles.Revision,edits=operation=="edit" ? edits : null}}}};
            Assert.That(executor.Execute(Request("edit",Assign(3,greeting.id),Assign(1,gait.id)),out var error,out _),Is.True,error);
            Assert.That(profiles.Revision,Is.EqualTo(2));Assert.That(editor.Revision,Is.EqualTo(sceneRevision));Assert.That(rules.Revision,Is.EqualTo(ruleRevision));
            var view=observer.Observe().activityProfile;
            Assert.That(JsonUtility.ToJson(view.roles[3]),Is.EqualTo(JsonUtility.ToJson(book.State.activityProfile.roles[3])));
            Assert.That(view.roles[1].choices.Single().motionId,Is.EqualTo(gait.id));Assert.That(avatar.ActivityMotionId,Is.Null,"Saving with the library open must not start automatic playback");
            string output=Environment.GetEnvironmentVariable("MAESTRO_ACTIVITY_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"activity-state.json"),RoomAgentWire.Serialize(observer.Observe()));}
            var staleAgent=Request("edit",new AvatarActivityEdit {operation="clear",role=3});
            var staleBook=new LibraryBookRequest {version=1,session=book.State.session,sequence=2,action="roleClear",modelHash=avatar.ModelHash,profileRevision=profiles.Revision,role=3};
            var work=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=1,action="roleAssign",modelHash=avatar.ModelHash,profileRevision=profiles.Revision,role=3,motionId=greeting.id,speed=.75f,weight=4,loop=true});yield return Until(()=>work.IsCompleted);
            Assert.That(profiles.Revision,Is.EqualTo(3));Assert.That(observer.Observe().activityProfile.roles[3].choices.Single().weight,Is.EqualTo(4));
            Assert.That(executor.Execute(staleAgent,out error,out _),Is.False);Assert.That(error,Does.Contain("changed"));
            work=book.HandleAsync(staleBook);yield return Until(()=>work.IsCompleted);Assert.That(book.State.status,Does.Contain("changed"));Assert.That(profiles.Revision,Is.EqualTo(3));
            Assert.That(executor.Execute(Request("edit",Assign(0,gait.id),Assign(2,new string('a',32))),out error,out _),Is.False);Assert.That(profiles.Find(avatar.ModelHash).roles.Any(r=>r.role==TutorMotionRole.Idle),Is.False);Assert.That(profiles.Revision,Is.EqualTo(3));
            Assert.That(executor.Execute(Request("undo"),out error,out _),Is.True,error);Assert.That(book.State.activityProfile.roles[3].choices.Single().weight,Is.EqualTo(2));
            Assert.That(executor.Execute(Request("undo"),out error,out _),Is.True,error);Assert.That(profiles.Find(avatar.ModelHash),Is.Null,"One Undo removes both roles in the original batch");
            Assert.That(executor.Execute(Request("redo"),out error,out _),Is.True,error);Assert.That(editor.Revision,Is.EqualTo(sceneRevision));
            book.SetVisible(false);avatar.ObserveTutorState(new BookSnapshot {version=1,activity="speaking"});yield return Until(()=>avatar.ActivityMotionId==greeting.id);
            var head=avatar.PoseRig.CanonicalBone(PoseJoint.Head);var rotation=head.localRotation;yield return new WaitForSeconds(.3f);Assert.That(Quaternion.Angle(rotation,head.localRotation),Is.GreaterThan(1));
            avatar.ObserveTutorState(new BookSnapshot {version=1,activity="listening"});yield return Until(()=>avatar.ActivityMotionId==gait.id);
            avatar.SetEditing(true);yield return null;Assert.That(avatar.ActivityMotionId,Is.Null);
            Assert.That(executor.Execute(Request("edit",Assign(2,greeting.id)),out error,out _),Is.True,error);yield return null;Assert.That(avatar.ActivityMotionId,Is.Null,"Saving does not steal manual authoring ownership");avatar.SetEditing(false);
            File.Delete(Path.Combine(directory,"motions",greeting.hash+".motion.glb"));
            Assert.That(executor.Execute(Request("edit",Assign(0,greeting.id)),out error,out _),Is.False);Assert.That(error,Does.Contain("download"));
            view=observer.Observe().activityProfile;Assert.That(view.roles[3].choices.Single().available,Is.False);Assert.That(view.roles[3].choices.Single().motionId,Is.EqualTo(greeting.id));
        }
        [UnityTest] public IEnumerator AgentAndManualWalkSelectionShareValidationPlaybackUndoAndAvailability()
        {
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            RoomAgentRequest Request(params RoomAgentCommand[] commands) => new() {version=2,conditions=new[] {new RoomObjectCondition {id="maestro",revision=editor.ObjectRevision("maestro")}},commands=commands};
            RoomAgentCommand Choose(string id) => new() {action="avatarWalk",target="maestro",motionId=id};
            Assert.That(observer.Observe().walk.source,Is.EqualTo("included"));
            var original=editor.Read("maestro");int before=editor.Revision;
            Assert.That(executor.Execute(Request(Choose(gait.id),new RoomAgentCommand {action="avatarSettings",target="maestro",movement=new AvatarMovementSettings {distance=1.1f,speed=.8f}}),out var error,out _),Is.True,error);
            Assert.That(editor.Revision,Is.EqualTo(before+1));Assert.That(avatar.IsImportedClipPlaying,Is.False,"Saving a gait does not begin movement or preview");
            var view=observer.Observe().walk;Assert.That(view.motionId,Is.EqualTo(gait.id));Assert.That(view.name,Is.EqualTo("Walking"));Assert.That(view.available,Is.True);Assert.That(view.source,Is.EqualTo("library"));
            string output=Environment.GetEnvironmentVariable("MAESTRO_WALK_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"walk-state.json"),RoomAgentWire.Serialize(observer.Observe()));}
            authoring.PreviewWalk();yield return Until(()=>avatar.LibraryMotionId==gait.id);
            var leg=avatar.PoseRig.Bone(PoseJoint.LeftUpperLeg);var rotation=leg.rotation;yield return new WaitForSeconds(.3f);Assert.That(Quaternion.Angle(rotation,leg.rotation),Is.GreaterThan(1));authoring.Stop();
            editor.Undo();Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(original.walkMotionId));Assert.That(editor.Read("maestro").walkSpeed,Is.EqualTo(original.walkSpeed));
            editor.Redo();Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(gait.id));Assert.That(editor.Read("maestro").walkSpeed,Is.EqualTo(.8f));
            var stale=Request(Choose(gait.id));Assert.That(editor.SetAvatarWalkMotion(greeting.id),Is.True);
            Assert.That(observer.Observe().walk.motionId,Is.EqualTo(greeting.id));Assert.That(executor.Execute(stale,out error,out _),Is.False);Assert.That(error,Does.Contain("changed"));Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(greeting.id));
            var add=editor.Motions.ImportAsync("different-rig.glb",ModelFixture.Create());yield return Until(()=>add.IsCompleted);Assert.That(add.Exception,Is.Null);
            before=editor.Revision;float size=editor.Read("maestro").scale;
            Assert.That(executor.Execute(Request(new RoomAgentCommand {action="resize",target="maestro",scale=1.2f},Choose(add.Result.Single().id)),out error,out _),Is.False);
            Assert.That(editor.Revision,Is.EqualTo(before));Assert.That(editor.Read("maestro").scale,Is.EqualTo(size),"A failed assignment cannot partially apply the saved batch");
            File.Delete(Path.Combine(directory,"motions",gait.hash+".motion.glb"));
            Assert.That(editor.SetAvatarWalkMotion(gait.id),Is.False);Assert.That(executor.Execute(Request(Choose(gait.id)),out error,out _),Is.False);Assert.That(error,Does.Contain("download"));
            File.Delete(Path.Combine(directory,"motions",greeting.hash+".motion.glb"));view=observer.Observe().walk;Assert.That(view.available,Is.False);Assert.That(view.motionId,Is.EqualTo(greeting.id),"Availability never silently rewrites a saved choice");
            Assert.That(executor.Execute(Request(Choose("")),out error,out _),Is.True,error);view=observer.Observe().walk;Assert.That(view.source,Is.EqualTo("included"));Assert.That(view.available,Is.True);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            editor.Undo();Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(greeting.id));Assert.That(editor.SetAvatarWalkClip(-1),Is.True);Assert.That(observer.Observe().walk.source,Is.EqualTo("included"));
        }
        [UnityTest] public IEnumerator AgentSearchSharesBookPagesAndFeedsRealRulePlaybackWithoutSideEffects()
        {
            foreach(int i in Enumerable.Range(1,15).Where(x=>x!=5 && x!=12)) {
                var add=editor.Motions.ImportAsync("extra-"+i+".glb",Clip(i,"Motion "+i));yield return Until(()=>add.IsCompleted);Assert.That(add.Exception,Is.Null);
            }
            var different=ModelFixture.Mixamo(json=>json["nodes"][5]["translation"][1]=.15);
            var incompatible=editor.Motions.ImportAsync("other-rig.glb",different);yield return Until(()=>incompatible.IsCompleted);Assert.That(incompatible.Exception,Is.Null);
            var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>();book.Initialize(editor,imports,rules);book.SetVisible(true);
            var executor=new RoomAgentExecutor(editor);int originalRevision=editor.Revision,ruleRevision=rules.Revision;string selected=editor.SelectedId;
            var query=new RoomMotionQuery {query="",offset=12};
            var command=new RoomAgentCommand {action="motions",target="maestro",motionQuery=query};
            var request=new RoomAgentRequest {version=2,commands=new[] {command}};
            Assert.That(executor.Execute(request,out var error,out var created),Is.True,error);Assert.That(created,Is.Empty);
            var page=executor.Motions.Observe();Assert.That(page.total,Is.EqualTo(15));Assert.That(page.entries.Length,Is.EqualTo(3));Assert.That(page.offset,Is.EqualTo(12));
            var work=book.HandleAsync(new LibraryBookRequest {version=1,session=book.State.session,sequence=1,action="query",query="",offset=12,compatibleOnly=true});yield return Until(()=>work.IsCompleted);
            Assert.That(page.entries.Select(x=>x.id),Is.EqualTo(book.State.entries.Select(x=>x.id)),"Human and agent browse the same page");
            Assert.That(page.entries.Any(x=>x.id==incompatible.Result.Single().id),Is.False);
            query.query="Walking";query.offset=0;Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            page=executor.Motions.Observe();Assert.That(page.entries.Single().id,Is.EqualTo(gait.id));Assert.That(page.entries.Single().downloaded,Is.True);
            Assert.That(editor.Revision,Is.EqualTo(originalRevision));Assert.That(rules.Revision,Is.EqualTo(ruleRevision));Assert.That(editor.SelectedId,Is.EqualTo(selected));Assert.That(avatar.IsImportedClipPlaying,Is.False);
            string output=Environment.GetEnvironmentVariable("MAESTRO_MOTION_SEARCH_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {
                Directory.CreateDirectory(output);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var observation=observer.Observe();observation.motions=page;
                File.WriteAllText(Path.Combine(output,"motion-search-state.json"),RoomAgentWire.Serialize(observation));
            }
            var sequence=rules.Selected;var selectedSteps=sequence.SimpleSteps();selectedSteps[0].motionId=page.entries.Single().id;sequence.SetSimpleSteps(selectedSteps);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=rules.Revision,edits=new[] {new RuleEdit {kind="save",sequence=sequence}}}}}},out error,out _),Is.True,error);
            Assert.That(runtime.Trigger(sequence.id),Is.True);yield return Until(()=>avatar.LibraryMotionId==gait.id);runtime.StopAll();
            var update=editor.Motions.UpdateAsync(gait.id,"Everyday walk",new[] {"calm"},true);yield return Until(()=>update.IsCompleted);Assert.That(update.Exception,Is.Null);
            query.query="calm";query.favouritesOnly=true;Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(executor.Motions.Observe().entries.Single().name,Is.EqualTo("Everyday walk"));
            var archive=editor.Motions.ArchiveAsync(gait.id,true);yield return Until(()=>archive.IsCompleted);Assert.That(executor.Motions.Observe().total,Is.Zero);
            query.archivedOnly=true;Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(executor.Motions.Observe().entries.Single().archived,Is.True);
            Assert.That(editor.SetMaestroModel(null),Is.True);yield return Until(()=>!avatar.ModelBusy);
            page=executor.Motions.Observe();Assert.That(page.ready,Is.False);Assert.That(page.entries,Is.Empty);Assert.That(page.modelHash,Is.Empty);
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("loaded imported model"));
        }
        [UnityTest] public IEnumerator ProgramEditsKeepLibraryUsableAndProtectReferencedMotions()
        {
            var imports=root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); book.SetVisible(true);
            int requestNumber=0;
            LibraryBookRequest Request(string action) => new() { version=1,session=book.State.session,sequence=++requestNumber,action=action,motionId=gait.id,ruleId=rules.Selected.id,stepIndex=rules.SelectedStepIndex };
            var work=book.HandleAsync(Request("select")); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.canAssign,Is.True);
            var staleAssignment=Request("rule");
            var program=rules.Selected; program.name="Programmed walk";
            program.program=Newtonsoft.Json.JsonConvert.SerializeObject(new {
                version=2,entry="main",resources=new[] {"maestro"},functions=new[] {new {
                    name="main",returns="void",parameters=Array.Empty<object>(),locals=new[] {new {name="reserved",initial=false}},
                    body=new[] {new {id="walk",op="invoke",capability="animation.play",version=1,arguments=new {target="maestro",source=new {kind="library",motionId=gait.id},channel="wholeTarget",seconds=.5f,loop=true},bindings=new {}}}
                }}
            });
            Assert.That(program.Compile(out var programError),Is.Not.Null,programError);
            var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=new RuleRequest {
                action="edit",revision=rules.Revision,edits=new[] {new RuleEdit {kind="save",sequence=program}}
            }}}},out var error,out _),Is.True,error);
            Assert.That(rules.SelectedStep,Is.Null); Assert.That(book.State.canAssign,Is.False);
            Assert.That(book.State.usage.uses,Is.EqualTo(new[] {"Program: Programmed walk"}));
            var usage=MotionUsage.Read(editor,rules,gait.id,retained:new MotionRetention());
            Assert.That(usage.total,Is.EqualTo(1)); Assert.That(usage.history || usage.saved || usage.uncertain || usage.playing,Is.False);
            Assert.That(usage.protection,Does.Contain("assignments"),"The current program protects its motion before save/history retention can do so");
            work=book.HandleAsync(staleAssignment); yield return Until(() => work.IsCompleted);
            Assert.That(work.Exception,Is.Null); Assert.That(book.State.ack,Is.EqualTo(staleAssignment.sequence));
            Assert.That(book.State.status,Does.Contain("program")); Assert.That(rules.Selected.program,Is.EqualTo(program.program));
            Assert.That(runtime.Trigger(program.id),Is.True); yield return Until(() => avatar.LibraryMotionId == gait.id);
            book.Refresh(); Assert.That(book.State.canPreview,Is.True); Assert.That(book.State.canAssign,Is.False);
            runtime.StopAll();
            work=book.HandleAsync(Request("archive")); yield return Until(() => work.IsCompleted);
            work=book.HandleAsync(Request("removeDownload")); yield return Until(() => work.IsCompleted);
            Assert.That(editor.Motions.Downloaded(gait.id),Is.True);
            rules.Undo(); Assert.That(rules.Selected.SimpleSteps(),Is.Not.Null); Assert.That(book.State.canAssign,Is.True);
            work=book.HandleAsync(Request("rule")); yield return Until(() => work.IsCompleted);
            Assert.That(rules.SelectedStep.motionId,Is.EqualTo(gait.id),book.State.status);
            book.SetVisible(false); rules.Undo(); rules.Redo();
            Assert.That(book.State.visible,Is.False); Assert.That(rules.SelectedStep.motionId,Is.EqualTo(gait.id));
        }
        [UnityTest] public IEnumerator BookArchiveKeepsPlaybackAndShowsCurrentHistoryAndSavedProtection()
        {
            var imports=root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); book.SetVisible(true);
            int sequence=0;
            LibraryBookRequest Request(string action) => new() { version=1,session=book.State.session,sequence=++sequence,action=action,motionId=greeting.id };
            var work=book.HandleAsync(Request("select")); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.usage.total,Is.EqualTo(1)); Assert.That(book.State.usage.uses.Single(),Does.Contain("Action"));
            Assert.That(runtime.Trigger(rules.Selected.id),Is.True); yield return Until(() => avatar.LibraryMotionId == greeting.id);
            work=book.HandleAsync(Request("archive")); yield return Until(() => work.IsCompleted);
            Assert.That(editor.Motions.Inspect(greeting.id).archived,Is.True); Assert.That(avatar.LibraryMotionId,Is.EqualTo(greeting.id),"Archive must not interrupt an existing rule");
            work=book.HandleAsync(Request("removeDownload")); yield return Until(() => work.IsCompleted);
            Assert.That(editor.Motions.Downloaded(greeting.id),Is.True); Assert.That(book.State.canRemoveDownload,Is.False);
            runtime.StopAll(); rules.AssignLibraryMotion(gait.id); book.Refresh();
            Assert.That(book.State.usage.total,Is.Zero); Assert.That(book.State.usage.history,Is.True); Assert.That(book.State.canRemoveDownload,Is.False);
            rules.Undo(); Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id));
            rules.SendMessage("OnApplicationPause",true); rules.SendMessage("OnApplicationPause",false);
            yield return Until(() => book.State.usage.saved);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE");
            if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence,"protected-book-state.json"),Newtonsoft.Json.JsonConvert.SerializeObject(book.State)); }
            work=book.HandleAsync(Request("restore")); yield return Until(() => work.IsCompleted); Assert.That(editor.Motions.Find(greeting.id).archived,Is.False);
        }
        [UnityTest] public IEnumerator BookRemovesUnusedDownloadAndExactReimportRestoresTheSameSelection()
        {
            var add=editor.Motions.ImportAsync("unused.glb",Clip(8,"Unassigned gesture")); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); var entry=add.Result.Single();
            var imports=root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); book.SetVisible(true);
            int sequence=0;
            LibraryBookRequest Request(string action) => new() { version=1,session=book.State.session,sequence=++sequence,action=action,motionId=entry.id };
            var work=book.HandleAsync(Request("select")); yield return Until(() => work.IsCompleted);
            work=book.HandleAsync(Request("archive")); yield return Until(() => work.IsCompleted); yield return Until(() => book.State.canRemoveDownload);
            var query=Request("query"); query.query=""; query.archivedOnly=true; query.compatibleOnly=true; work=book.HandleAsync(query); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.entries.Single().id,Is.EqualTo(entry.id));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE");
            if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence,"maintenance-book-state.json"),Newtonsoft.Json.JsonConvert.SerializeObject(book.State)); }
            work=book.HandleAsync(Request("removeDownload")); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.selected.id,Is.EqualTo(entry.id)); Assert.That(book.State.selected.removed,Is.True,book.State.status); Assert.That(book.State.canPreview || book.State.canWalk || book.State.canAssign,Is.False);
            Assert.That(editor.Motions.Find(entry.id),Is.Null); Assert.That(editor.Motions.PayloadPresent(entry.id),Is.False); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            if (!string.IsNullOrEmpty(evidence)) File.WriteAllText(Path.Combine(evidence,"removed-book-state.json"),Newtonsoft.Json.JsonConvert.SerializeObject(book.State));
            work=book.HandleAsync(Request("walk")); yield return Until(() => work.IsCompleted); Assert.That(editor.Read("maestro").walkMotionId,Is.Not.EqualTo(entry.id));
            add=editor.Motions.ImportAsync("original-again.glb",Clip(8,"Unassigned gesture")); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); Assert.That(add.Result.Single().id,Is.EqualTo(entry.id));
            book.Refresh(); Assert.That(book.State.selected.removed,Is.False); Assert.That(book.State.canPreview,Is.True); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            // A lost payload must still be removable from the catalogue without
            // forcing the user to download the original solely to forget it.
            File.Delete(Path.Combine(directory,"motions",entry.hash+".motion.glb"));
            work=book.HandleAsync(Request("archive")); yield return Until(() => work.IsCompleted); Assert.That(book.State.canRemoveDownload,Is.True);
            work=book.HandleAsync(Request("removeDownload")); yield return Until(() => work.IsCompleted); Assert.That(book.State.canForgetMotion,Is.True);
            work=book.HandleAsync(Request("forgetMotion")); yield return Until(() => work.IsCompleted); Assert.That(editor.Motions.Inspect(entry.id),Is.Null); Assert.That(book.State.selected,Is.Null);

        }
        [UnityTest] public IEnumerator TutorStateProfilesSwitchMotionsBlendAndYieldToManualOwners()
        {
            var profiles=editor.ActivityProfiles; var model=avatar.ModelHash; var rig=avatar.CustomModel.MotionRigHash;
            Assert.That(profiles.Assign(model,rig,TutorMotionRole.Speaking,new TutorMotionChoice { motionId=greeting.id,loop=true },editor.Motions,out var error),Is.True,error);
            Assert.That(profiles.Assign(model,rig,TutorMotionRole.Listening,new TutorMotionChoice { motionId=gait.id,loop=true },editor.Motions,out error),Is.True,error);
            var speaking=new BookSnapshot { version=1,activity="speaking" }; avatar.ObserveTutorState(speaking);
            yield return Until(() => avatar.ActivityMotionId == greeting.id);
            var head=avatar.PoseRig.CanonicalBone(PoseJoint.Head); var before=head.localRotation;
            yield return new WaitForSeconds(.3f); Assert.That(Quaternion.Angle(before,head.localRotation),Is.GreaterThan(1),"The assigned motion must deform the actual rig");
            var rootPosition=avatar.transform.position; var beforeSwitch=head.localRotation;
            avatar.ObserveTutorState(new BookSnapshot { version=1,activity="listening" }); yield return Until(() => avatar.ActivityMotionId == gait.id);
            Assert.That(Quaternion.Angle(beforeSwitch,head.localRotation),Is.LessThan(30),"Transitions must begin from the presented pose");
            Assert.That(avatar.transform.position,Is.EqualTo(rootPosition),"Activity clips do not own room translation");
            avatar.SetEditing(true); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
            avatar.SetEditing(false); avatar.SetActivityLibraryOpen(true); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
            avatar.SetActivityLibraryOpen(false); yield return Until(() => avatar.ActivityMotionId == gait.id);
            var play=editor.Motions.AcquireAsync(greeting.id,rig); yield return Until(() => play.IsCompleted); Assert.That(avatar.PlayLibraryMotion(play.Result,true),Is.True);
            yield return new WaitForSeconds(.15f); Assert.That(avatar.LibraryMotionId,Is.EqualTo(greeting.id)); Assert.That(avatar.ActivityMotionId,Is.Null,"An explicit preview retains priority");
            avatar.StopImportedClip(); avatar.ReducedMotion=true; yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
            avatar.ReducedMotion=false; avatar.ObserveTutorState(new BookSnapshot { version=1,activity="speaking",audioPaused=true }); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
        }
        [UnityTest] public IEnumerator StateLoadCannotStartAfterPauseAndAvatarProfilesReturnAfterSwitching()
        {
            string model=avatar.ModelHash,rig=avatar.CustomModel.MotionRigHash;
            editor.ActivityProfiles.Assign(model,rig,TutorMotionRole.Speaking,new TutorMotionChoice { motionId=greeting.id,loop=true },editor.Motions,out _);
            var observed=new BookSnapshot { version=1,activity="speaking" }; avatar.ObserveTutorState(observed);
            yield return null; avatar.SendMessage("OnApplicationPause",true); yield return new WaitForSeconds(.3f); Assert.That(avatar.ActivityMotionId,Is.Null);
            avatar.SendMessage("OnApplicationPause",false); avatar.ObserveTutorState(observed); yield return new WaitForSeconds(.15f); Assert.That(avatar.ActivityMotionId,Is.Null,"Resume rejects the old snapshot object");
            avatar.ObserveTutorState(new BookSnapshot { version=1,activity="speaking" }); yield return Until(() => avatar.ActivityMotionId == greeting.id);
            Assert.That(editor.SetMaestroModel(""),Is.True); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
            Assert.That(editor.SetMaestroModel(model),Is.True); yield return Until(() => !avatar.ModelBusy); yield return Until(() => avatar.ActivityMotionId == greeting.id);
            Assert.That(editor.ActivityProfiles.Find(model).roles[0].choices[0].motionId,Is.EqualTo(greeting.id));
            avatar.SendMessage("OnApplicationFocus",false); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
            avatar.SendMessage("OnApplicationFocus",true); yield return null; Assert.That(avatar.ActivityMotionId,Is.Null);
        }
        [UnityTest] public IEnumerator NonloopingStateChoicesAvoidImmediateRepeatAndRespectReuseGap()
        {
            string model=avatar.ModelHash,rig=avatar.CustomModel.MotionRigHash;
            editor.ActivityProfiles.Assign(model,rig,TutorMotionRole.Speaking,new TutorMotionChoice { motionId=greeting.id,speed=2,cooldown=3,weight=10 },editor.Motions,out _);
            editor.ActivityProfiles.Assign(model,rig,TutorMotionRole.Speaking,new TutorMotionChoice { motionId=gait.id,speed=2,cooldown=3 },editor.Motions,out _);
            avatar.ObserveTutorState(new BookSnapshot { version=1,activity="speaking" }); yield return Until(() => avatar.ActivityMotionId != null);
            string first=avatar.ActivityMotionId; yield return Until(() => avatar.ActivityMotionId != null && avatar.ActivityMotionId != first);
            yield return Until(() => avatar.ActivityMotionId == null); yield return new WaitForSeconds(.15f);
            Assert.That(avatar.ActivityMotionId,Is.Null,"Completed choices wait for their cooldown instead of repeating immediately");
            Assert.That(avatar.ActivityMotionStatus,Does.Contain("included"));
        }
        [UnityTest] public IEnumerator BookRoleAssignmentsRejectStaleAvatarAndPersistWithoutPreviewing()
        {
            var imports=root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book=root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); book.SetVisible(true);
            int sequence=0;
            LibraryBookRequest Request(string action,string model=null) => new() { version=1,session=book.State.session,sequence=++sequence,action=action,profileRevision=editor.ActivityProfiles.Revision,modelHash=model ?? avatar.ModelHash,role=3,motionId=greeting.id,weight=2,speed=1,cooldown=2 };
            avatar.ObserveTutorState(new BookSnapshot { version=1,activity="speaking" });
            var work=book.HandleAsync(Request("roleAssign")); yield return Until(() => work.IsCompleted); yield return null;
            Assert.That(book.State.activityProfile.roles[3].choices.Single().motionId,Is.EqualTo(greeting.id)); Assert.That(avatar.ActivityMotionId,Is.Null,"Library browsing suppresses automatic state playback");
            work=book.HandleAsync(Request("roleClear",new string('a',64))); yield return Until(() => work.IsCompleted); Assert.That(book.State.status,Does.Contain("changed")); Assert.That(book.State.activityProfile.roles[3].choices.Length,Is.EqualTo(1));
            work=book.HandleAsync(Request("roleRemove")); yield return Until(() => work.IsCompleted); Assert.That(book.State.activityProfile.roles[3].choices,Is.Empty);
            work=book.HandleAsync(Request("roleUndo")); yield return Until(() => work.IsCompleted); Assert.That(book.State.activityProfile.roles[3].choices.Length,Is.EqualTo(1));
            work=book.HandleAsync(Request("roleRedo")); yield return Until(() => work.IsCompleted); Assert.That(book.State.activityProfile.roles[3].choices,Is.Empty);
            work=book.HandleAsync(Request("roleAssign")); yield return Until(() => work.IsCompleted);
            work=book.HandleAsync(Request("select")); yield return Until(() => work.IsCompleted);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE");
            if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence,"activity-book-state.json"),Newtonsoft.Json.JsonConvert.SerializeObject(book.State)); }
            work=book.HandleAsync(Request("close")); yield return Until(() => work.IsCompleted); yield return Until(() => avatar.ActivityMotionId == greeting.id);
        }
        [UnityTest] public IEnumerator BookLibrarySearchesPagesEditsAndAssignsStableIdsWithoutAutoplay()
        {
            var imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book = root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); imports.BrowseLibrary();
            Assert.That(book.State.visible,Is.True); Assert.That(imports.LibraryMode,Is.True);
            foreach (int i in Enumerable.Range(1,15).Where(x => x != 5 && x != 12)) { var add = editor.Motions.ImportAsync("motion.glb",Clip(i,"Motion "+i)); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); }
            int sequence = 0;
            LibraryBookRequest Request(string action) => new() { version = 1,sequence = ++sequence,session = book.State.session,action = action };
            var query = Request("query"); query.query = ""; query.compatibleOnly = true;
            var work = book.HandleAsync(query); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.total,Is.EqualTo(15)); Assert.That(book.State.entries.Length,Is.EqualTo(12));
            query = Request("query"); query.query = ""; query.offset = 12; query.compatibleOnly = true;
            work = book.HandleAsync(query); yield return Until(() => work.IsCompleted); Assert.That(book.State.offset,Is.EqualTo(12)); Assert.That(book.State.entries.Length,Is.EqualTo(3));
            var select = Request("select"); select.motionId = gait.id; work = book.HandleAsync(select); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.canPreview && book.State.canWalk && book.State.canAssign,Is.True);
            Assert.That(imports.SelectedLibraryMotionId,Is.EqualTo(gait.id));
            imports.NextClip(); Assert.That(book.State.selected.id,Is.EqualTo(imports.SelectedLibraryMotionId));
            select.sequence = ++sequence; work = book.HandleAsync(select); yield return Until(() => work.IsCompleted);
            var save = Request("save"); save.motionId = gait.id; save.name = "Everyday walk"; save.tags = new[] { "Walk","Calm" }; save.favourite = true;
            work = book.HandleAsync(save); yield return Until(() => work.IsCompleted);
            Assert.That(editor.Motions.Find(gait.id).name,Is.EqualTo("Everyday walk")); Assert.That(imports.Details,Does.Contain("Everyday walk")); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            query = Request("query"); query.query = "calm"; query.favouritesOnly = true;
            work = book.HandleAsync(query); yield return Until(() => work.IsCompleted); Assert.That(book.State.total,Is.EqualTo(1)); Assert.That(book.State.entries[0].id,Is.EqualTo(gait.id));
            var walk = Request("walk"); walk.motionId = gait.id; work = book.HandleAsync(walk); yield return Until(() => work.IsCompleted);
            Assert.That(editor.Read("maestro").walkMotionId,Is.EqualTo(gait.id)); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var assign = Request("rule"); assign.motionId = gait.id; assign.ruleId = rules.Selected.id; assign.stepIndex = rules.SelectedStepIndex;
            work = book.HandleAsync(assign); yield return Until(() => work.IsCompleted); Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(gait.id));
            rules.NewSequence(); assign.sequence = ++sequence; work = book.HandleAsync(assign); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.status,Does.Contain("changed")); Assert.That(rules.Selected.SimpleSteps()[0].action,Is.EqualTo(RuleActionKind.Gesture));
            query = Request("query"); query.query = ""; work = book.HandleAsync(query); yield return Until(() => work.IsCompleted);
            string evidence = Environment.GetEnvironmentVariable("MAESTRO_IMPORT_EVIDENCE");
            if (!string.IsNullOrEmpty(evidence)) { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence,"library-book-state.json"),Newtonsoft.Json.JsonConvert.SerializeObject(book.State)); }
        }
        [UnityTest] public IEnumerator BookLibraryAcknowledgesBadInputAndCannotReplayCancelledOrOldSessionRequests()
        {
            var imports = root.AddComponent<ImportWorkshop>(); imports.Initialize(editor,authoring);
            var book = root.AddComponent<LibraryBookController>(); book.Initialize(editor,imports,rules); book.SetVisible(true);
            string session = book.State.session;
            var preview = new LibraryBookRequest { version = 1,sequence = 1,session = session,action = "preview",motionId = greeting.id };
            var work = book.HandleAsync(preview); Assert.That(work.IsCompleted,Is.False,"Exercise the cold asynchronous load");
            var stop = book.HandleAsync(new LibraryBookRequest { version = 1,sequence = 2,session = session,action = "stop" });
            yield return Until(() => work.IsCompleted && stop.IsCompleted); yield return null;
            Assert.That(avatar.IsImportedClipPlaying,Is.False); Assert.That(book.State.ack,Is.EqualTo(2));
            work = book.HandleAsync(preview); yield return Until(() => work.IsCompleted); Assert.That(avatar.IsImportedClipPlaying,Is.False);
            var malformed = new LibraryBookRequest { version = 1,sequence = 3,session = session,action = "save",motionId = gait.id,name = "Bad",tags = new string[17] };
            work = book.HandleAsync(malformed); yield return Until(() => work.IsCompleted); Assert.That(book.State.ack,Is.EqualTo(3)); Assert.That(book.State.status,Does.Contain("invalid"));
            book.SendMessage("OnApplicationFocus",false); book.SendMessage("OnApplicationFocus",true);
            Assert.That(book.State.session,Is.Not.EqualTo(session)); preview.sequence = 4;
            work = book.HandleAsync(preview); yield return Until(() => work.IsCompleted); Assert.That(avatar.IsImportedClipPlaying,Is.False); Assert.That(book.State.ack,Is.Zero);
            work = book.HandleAsync(new LibraryBookRequest { version = 1,sequence = 1,session = book.State.session,action = "close" }); yield return Until(() => work.IsCompleted);
            Assert.That(book.State.visible,Is.False); Assert.That(book.State.ack,Is.EqualTo(1));
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
            Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id)); Assert.That(rules.Summary,Does.Contain("My greeting"));
            rules.AssignLibraryMotion(gait.id); rules.Undo(); Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(greeting.id));
            rules.SendMessage("OnApplicationPause",true); var restored = new RuleStorage(directory).Load(out var error); Assert.That(error,Is.Null);
            Assert.That(restored.sequences.Single().SimpleSteps().Single().motionId,Is.EqualTo(greeting.id)); Assert.That(restored.buttons.Single().mount,Is.EqualTo(ButtonMount.LeftController));
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
        [UnityTest] public IEnumerator NativeQuickSelectionAndBookLibraryAssignTheSameLiteralBlock()
        {
            rules.AddStep();string second=rules.SelectedStep.id;
            var tray=new GameObject("Shared selection tray");tray.transform.SetParent(root.transform,false);
            var tools=tray.AddComponent<RuleTools>();tools.Build(rules,root.GetComponent<RoomInteraction>());
            Assert.That(tools.Draft.NodeId,Is.EqualTo(second),"Opening the tray keeps the existing library assignment target");
            tools.Draft.Step(-1);Assert.That(rules.SelectedStep.id,Is.EqualTo(tools.Draft.NodeId));
            rules.AssignLibraryMotion(gait.id);
            Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(gait.id));Assert.That(rules.Selected.SimpleSteps()[1].action,Is.EqualTo(RuleActionKind.Wait));
            tools.Draft.Step(1);Assert.That(rules.SelectedStep.id,Is.EqualTo(second));rules.AssignLibraryMotion(greeting.id);
            Assert.That(rules.Selected.SimpleSteps()[0].motionId,Is.EqualTo(gait.id));Assert.That(rules.Selected.SimpleSteps()[1].motionId,Is.EqualTo(greeting.id));
            Assert.That(tools.Draft.NodeId,Is.EqualTo(second));Assert.That(tools.Draft.CapabilityId,Is.EqualTo("animation.play"));
            Assert.That(avatar.LibraryMotionId,Is.Null);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator NamedLibraryOperationReleasesLateLoadsAndTransferredLeasesExactlyOnce()
        {
            var host=new RoomRuleActions(editor,authoring);
            var arguments=new JObject {["target"]="maestro",["seconds"]=1,["loop"]=true,["source"]=new JObject {["kind"]="library",["motionId"]=greeting.id},["channel"]="wholeTarget"};
            Assert.That(BehaviourCatalog.TryCall("animation.play",1,arguments,out var call,out var error),Is.True,error);
            try {
                Assert.That(editor.Motions.Pinned(greeting.id),Is.False);
                Assert.That(host.Start("cancel-load",call,out _,out error),Is.True,error);
                Assert.That(host.State("cancel-load",out _),Is.EqualTo(RuleActionState.Preparing));
                host.Stop("cancel-load",false);host.Stop("cancel-load",true);
                // Await the same real load; cancelling its operation must dispose the late lease.
                var probe=editor.Motions.AcquireAsync(greeting.id,greeting.rigHash);
                yield return Until(()=>probe.IsCompleted);Assert.That(probe.Exception,Is.Null);probe.Result.Dispose();
                yield return Until(()=>!editor.Motions.Pinned(greeting.id));
                Assert.That(avatar.IsImportedClipPlaying,Is.False);

                Assert.That(host.Start("complete",call,out var seconds,out error),Is.True,error);Assert.That(seconds,Is.EqualTo(1));
                yield return Until(()=>host.State("complete",out _)!=RuleActionState.Preparing);
                Assert.That(host.State("complete",out error),Is.EqualTo(RuleActionState.Ready),error);
                Assert.That(avatar.LibraryMotionId,Is.EqualTo(greeting.id));Assert.That(editor.Motions.Pinned(greeting.id),Is.True);
                var head=avatar.PoseRig.Bone(PoseJoint.Head);var before=head.rotation;
                yield return new WaitForSeconds(.2f);Assert.That(Quaternion.Angle(before,head.rotation),Is.GreaterThan(1));
                Assert.That(host.Complete("complete",out error),Is.True,error);
                Assert.That(avatar.LibraryMotionId,Is.Null);Assert.That(editor.Motions.Pinned(greeting.id),Is.False);
                Assert.That(host.Complete("complete",out _),Is.True);host.Stop("complete",false);
                Assert.That(editor.Motions.Pinned(greeting.id),Is.False);
            } finally {host.Stop("cancel-load",false);host.Stop("complete",false);}
        }
        [UnityTest] public IEnumerator NamedLibraryOperationRejectsACompatibleReplacementWhilePreparing()
        {
            var replacement=ModelLibrary.Inspect("replacement.glb",Clip(8,"Replacement"));
            var save=editor.Models.SaveAsync(replacement);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var host=new RoomRuleActions(editor,authoring);
            Assert.That(BehaviourCatalog.TryCall("animation.play",1,new JObject {
                ["target"]="maestro",["seconds"]=1,["loop"]=true,["source"]=new JObject {["kind"]="library",["motionId"]=gait.id},["channel"]="wholeTarget"
            },out var call,out var error),Is.True,error);
            try {
                Assert.That(host.Start("replacement",call,out _,out error),Is.True,error);
                Assert.That(host.State("replacement",out _),Is.EqualTo(RuleActionState.Preparing));
                Assert.That(editor.SetMaestroModel(replacement.Hash),Is.True);yield return Until(()=>!avatar.ModelBusy);
                var probe=editor.Motions.AcquireAsync(gait.id,gait.rigHash);
                yield return Until(()=>probe.IsCompleted);Assert.That(probe.Exception,Is.Null);probe.Result.Dispose();
                yield return Until(()=>host.State("replacement",out _)!=RuleActionState.Preparing);
                Assert.That(host.State("replacement",out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("changed while loading"));
                Assert.That(avatar.LibraryMotionId,Is.Null,"A compatible replacement must not inherit a pending request for the previous model");
            } finally {host.Stop("replacement",false);}
            Assert.That(editor.Motions.Pinned(gait.id),Is.False);
            Assert.That(avatar.ModelHash,Is.EqualTo(replacement.Hash));
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
            editor.SendMessage("OnApplicationPause",false);
            var add = editor.Motions.ImportAsync("unavailable.glb",Clip(13,"Lower leg")); yield return Until(() => add.IsCompleted); Assert.That(add.Exception,Is.Null); var missing = add.Result.Single();
            Assert.That(editor.SetAvatarWalkMotion(missing.id),Is.True); File.Delete(Path.Combine(directory,"motions",missing.hash+".motion.glb"));
            authoring.PreviewWalk(); yield return Until(() => avatar.WalkMotionStatus?.Contains("missing") == true);
            Assert.That(avatar.LibraryMotionId,Is.Null); Assert.That(authoring.ControlsTarget("maestro"),Is.True,"The included gait remains available");
            authoring.Stop(); yield return null; Assert.That(avatar.IsImportedClipPlaying,Is.False);
        }
    }
}
