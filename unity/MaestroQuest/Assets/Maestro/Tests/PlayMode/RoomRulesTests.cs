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
using Newtonsoft.Json.Linq;
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
            for (int i = 0; i < System.Enum.GetValues(typeof(RuleActionKind)).Length && workshop.Selected.SimpleSteps()[0].action != RuleActionKind.RecordedAnimation; i++) workshop.CycleAction();
            Assert.That(workshop.Selected.SimpleSteps()[0].action,Is.EqualTo(RuleActionKind.RecordedAnimation));
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
        [UnityTest] public IEnumerator OneOffNativeWireMovesTheRealItemOnceAndLeavesDocumentsUntouched()
        {
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            var inbox=new RoomAgentInbox();string client=Guid.NewGuid().ToString("N");
            inbox.TryAccept(new RoomAgentSnapshot {clientId=client},out _);
            int revision=editor.Revision,ruleRevision=workshop.Revision;string selected=editor.SelectedId,document=JsonUtility.ToJson(editor.Snapshot()),rules=JsonUtility.ToJson(workshop.Snapshot());
            var call=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false}};
            var raw=new JObject {["version"]=2,["sequence"]=1,["session"]=inbox.Session,["conditions"]=new JArray(new JObject {["id"]=selected,["revision"]=editor.ObjectRevision(selected)}),
                ["commands"]=new JArray(new JObject {["action"]="execution",["execution"]=new JObject {["operation"]="start",["call"]=call}})};
            Assert.That(RoomControls.ValidWire(raw.ToString()),Is.True);
            var request=JsonUtility.FromJson<RoomAgentRequest>(raw.ToString());Assert.That(RoomAgentWire.PopulateStructured(request,raw),Is.True);
            var envelope=new RoomAgentSnapshot {clientId=client,session=inbox.Session,request=request};
            Assert.That(inbox.TryAccept(envelope,out var accepted),Is.True);
            Assert.That(executor.Execute(accepted,out var error,out var created),Is.True,error);Assert.That(created,Is.Empty);
            var view=executor.Executions.Observe();string runId=(string)view["selected"]["id"];
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_EXECUTION_EVIDENCE");if(string.IsNullOrEmpty(output))return;
                Directory.CreateDirectory(output);var state=observer.Observe();state.execution=executor.Executions.Observe();
                File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));
            }
            Evidence("running");Assert.That(inbox.TryAccept(envelope,out _),Is.False,"A duplicated transport request never starts another action");
            var start=block.transform.localPosition;yield return new WaitForSeconds(.25f);
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(start.x+.02f));
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="inspect",["runId"]=runId},out _),Is.True);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="cancel",["runId"]=runId},out _),Is.True);
            Evidence("cancelled");Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("cancelled"));
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="cancel",["runId"]=runId},out _),Is.True);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.SelectedId,Is.EqualTo(selected));
            Assert.That(workshop.Revision,Is.EqualTo(ruleRevision));Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(rules));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));
            raw["sequence"]=2;request=JsonUtility.FromJson<RoomAgentRequest>(raw.ToString());RoomAgentWire.PopulateStructured(request,raw);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return new WaitForSeconds(1.2f);
            Evidence("completed");Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return new WaitForSeconds(.2f);
            var position=block.transform.position;var hand=Hand(1,position-Vector3.forward*.2f);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("cancelled"));
            Assert.That(Vector3.Distance(position,block.transform.position),Is.LessThan(.01f),"Manual grab keeps the current animated position");
            Evidence("grabbed");
        }
        [UnityTest] public IEnumerator OneOffRechecksTargetAndPropRevisionsAndRefusesBusyOrPausedRuntime()
        {
            var executor=new RoomAgentExecutor(editor);string target=editor.SelectedId;int revision=editor.Revision;
            var request=new RoomAgentRequest {version=2,conditions=new[] {new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)-1}},commands=new[] {new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["call"]=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=1,["loop"]=false}}}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.False);Assert.That(error,Does.Contain("target changed"));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            request.conditions[0].revision=editor.ObjectRevision(target);Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("owns"));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.StopAll();runtime.enabled=false;Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("paused"));runtime.enabled=true;
            var args=(JObject)request.commands[0].execution["call"]["arguments"];args["target"]="maestro";args["prop"]=new JObject {["objectId"]=target,["avatarHash"]="",["hand"]="right",["release"]="return",["offset"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1},["releaseAt"]=1};
            Assert.That(Maestro.Quest.Programs.BehaviourCatalog.TryInvocation("animation.recording.play",1,args,out _,out _),Is.True);
            request.conditions=new[] {new RoomObjectCondition {id="maestro",revision=editor.ObjectRevision("maestro")},new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)-1}};
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("target changed"),"A stale prop is rejected before handler/authoring side effects");
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator CatalogDiscoveryAndLiveChecksDoNotEditOrInterruptTheRoom()
        {
            var executor=new RoomAgentExecutor(editor);int roomRevision=editor.Revision,ruleRevision=workshop.Revision;
            string selected=editor.SelectedId,document=JsonUtility.ToJson(editor.Snapshot());
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            JObject Query(JObject query,string phase) {
                Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="catalog",catalog=query}}},out var error,out var created),Is.True,error);
                Assert.That(created,Is.Empty);var result=executor.Catalog.Observe();
                string output=Environment.GetEnvironmentVariable("MAESTRO_CATALOG_EVIDENCE");
                if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);var state=observer.Observe();state.catalog=result;File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
                Assert.That(editor.Revision,Is.EqualTo(roomRevision));Assert.That(workshop.Revision,Is.EqualTo(ruleRevision));Assert.That(editor.SelectedId,Is.EqualTo(selected));
                return result;
            }
            var first=Query(new JObject {["operation"]="search",["query"]="",["offset"]=0},"search");
            var second=Query(new JObject {["operation"]="search",["query"]="",["offset"]=6},"search-next");
            Assert.That(first["entries"].Count(),Is.EqualTo(6));Assert.That((int)first["total"],Is.EqualTo(Maestro.Quest.Programs.BehaviourCatalog.Actions.Count));
            Assert.That(first["entries"].Select(x=>(string)x["id"]).Intersect(second["entries"].Select(x=>(string)x["id"])),Is.Empty);
            var inspected=Query(new JObject {["operation"]="inspect",["capability"]="animation.recording.play",["version"]=1},"inspect");
            Assert.That(JToken.DeepEquals(inspected["definition"],Maestro.Quest.Programs.BehaviourCatalog.Action("animation.recording.play").ToJson()),Is.True);
            var check=new JObject {["operation"]="check",["call"]=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false}}};
            var ready=Query(check,"ready");Assert.That((bool)ready["valid"],Is.True);Assert.That((bool)ready["available"],Is.True);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(runtime.Trigger(sequenceId),Is.True);yield return null;
            var occupied=Query(check,"occupied");Assert.That((bool)occupied["occupied"],Is.True);Assert.That((bool)occupied["available"],Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Checking cannot stop another action");
            runtime.StopAll();Assert.That((bool)executor.Catalog.Observe()["available"],Is.True,"Checks refresh without another command");
            runtime.enabled=false;Assert.That((bool)executor.Catalog.Observe()["available"],Is.False);runtime.enabled=true;
            check["call"]["arguments"]["target"]=Guid.NewGuid().ToString("N");var missing=Query(check,"missing");
            Assert.That((bool)missing["valid"],Is.True);Assert.That((bool)missing["available"],Is.False);
            check["call"]["arguments"]["seconds"]=100;var invalid=Query(check,"invalid");
            Assert.That((bool)invalid["valid"],Is.False);Assert.That((bool)invalid["available"],Is.False);
            var unknown=Query(new JObject {["operation"]="inspect",["capability"]="future.unknown",["version"]=1},"unknown");
            Assert.That(unknown["definition"].Type,Is.EqualTo(JTokenType.Null));
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
            int revision=workshop.Revision;var sequence=workshop.Selected;string originalStep=sequence.SimpleSteps()[0].id;
            sequence.SetSimpleSteps(sequence.SimpleSteps().Append(new RuleStep {action=RuleActionKind.Wait,seconds=1}).ToArray());
            var edit=new RuleRequest {action="edit",revision=revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence},new RuleEdit {kind="button",target=sequence.id,mount=ButtonMount.LeftController}}};
            Assert.That(workshop.Execute(edit,out var error,out _),Is.True,error);Assert.That(workshop.Revision,Is.EqualTo(revision+1));
            var saved=workshop.Selected;Assert.That(saved.SimpleSteps()[0].id,Is.EqualTo(originalStep));Assert.That(RuleDocument.IsId(saved.SimpleSteps()[1].id),Is.True);
            Assert.That(workshop.Execute(edit,out _,out _),Is.False,"An old draft cannot replace a newer edit");
            string before=JsonUtility.ToJson(workshop.Snapshot());var invalid=saved.SimpleSteps();invalid[0].seconds=-1;saved.SetSimpleSteps(invalid);
            edit.revision=workshop.Revision;edit.edits[0].sequence=saved;Assert.That(workshop.Execute(edit,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(before));
            Assert.That(workshop.Execute(new RuleRequest {action="undo",revision=workshop.Revision},out _,out _),Is.True);
            Assert.That(workshop.Selected.SimpleSteps().Length,Is.EqualTo(1));Assert.That(workshop.Snapshot().buttons.Length,Is.Zero);
            Assert.That(workshop.Execute(new RuleRequest {action="redo",revision=workshop.Revision},out _,out _),Is.True);
            var restored=workshop.Selected;string added=restored.SimpleSteps()[1].id;restored.SetSimpleSteps(restored.SimpleSteps().Reverse().ToArray());
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=restored}}},out _,out _),Is.True);
            Assert.That(workshop.Selected.SimpleSteps()[0].id,Is.EqualTo(added));workshop.AddStep();Assert.That(workshop.Observe().revision,Is.GreaterThan(revision));
            workshop.SendMessage("OnApplicationPause",true);var loaded=new RuleStorage(directory).Load(out _);Assert.That(loaded.sequences.First(x=>x.id==sequence.id).SimpleSteps()[0].id,Is.EqualTo(added));
            yield return null;
        }
        [UnityTest] public IEnumerator AgentRuleRecipePlaybackSharesTutorEventsAndActualButtonInput()
        {
            var agent=new RoomAgentExecutor(editor);var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;
            Assert.That(agent.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[]{new RoomAgentCommand {action="create",kind="recipe",reference="robot",name="Robot",recipe=recipe}}},out var error,out var created),Is.True,error);
            string target=created.Single();var geometry=editor.Find(target).GetComponent<RecipeObject>();Assert.That(geometry.IsPlaying,Is.False);
            var sequence=new RuleSequence {id="",name="Wave with speech",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.RecipeAnimation,targetId=target,seconds=.3f,loop=true})};
            var request=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{
                new RuleEdit {kind="save",reference="wave",sequence=sequence},
                new RuleEdit {kind="bind",binding=new RuleBinding {sequenceId="wave",trigger=RuleEventKind.Speaking,stopOnExit=true}},
                new RuleEdit {kind="button",target="wave",mount=ButtonMount.Room}}};
            Assert.That(agent.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=request}}},out error,out created),Is.True,error);
            string id=created.Single();yield return null;runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);Assert.That(workshop.Observe().running.Single().nodeId,Is.EqualTo(workshop.Selected.SimpleSteps()[0].id));
            var arm=geometry.Part("RightUpperArm");var rotation=arm.localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(rotation,arm.localRotation),Is.GreaterThan(.1f));
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});Assert.That(geometry.IsPlaying,Is.False);
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);Assert.That(geometry.IsPlaying,Is.True);
            yield return new WaitForSeconds(.4f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(geometry.IsPlaying,Is.False);
            Assert.That(editor.Read(target).recipe.playing,Is.False,"Rule playback does not rewrite the saved recipe flags");
        }
        [UnityTest] public IEnumerator ProgramsFromAgentDriveRealRecipeThroughEventsButtonsStopAndUndo()
        {
            var executor=new RoomAgentExecutor(editor);var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;
            Assert.That(executor.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[] {new RoomAgentCommand {action="create",kind="recipe",reference="robot",name="Program robot",recipe=recipe}}},out var error,out var created),Is.True,error);
            string target=created.Single();var geometry=editor.Find(target).GetComponent<RecipeObject>();
            var json=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json")));
            json["resources"]=new JArray(target);
            foreach(var token in new[] {json["functions"][0]["body"][1]["then"][0],json["functions"][0]["body"][1]["else"][0]}) {
                token["capability"]="animation.recipe.play";token["arguments"]=new JObject {["target"]=target,["seconds"]=.8f,["loop"]=true};
            }
            var sequence=new RuleSequence {id="",name="Programmed wave",program=json.ToString(Newtonsoft.Json.Formatting.None)};
            var request=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {
                new RuleEdit {kind="save",reference="wave",sequence=sequence},
                new RuleEdit {kind="bind",binding=new RuleBinding {sequenceId="wave",trigger=RuleEventKind.Speaking,stopOnExit=true}},
                new RuleEdit {kind="button",target="wave",mount=ButtonMount.LeftController}}};
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=request}}},out error,out created),Is.True,error);
            string id=created.Single();var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_PROGRAM_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
                var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);
                File.WriteAllText(Path.Combine(output,"program-"+phase+".json"),RoomAgentWire.Serialize(state));
            }
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;
            Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);Assert.That(workshop.Observe().running.Single().nodeId,Is.EqualTo("prime_wave"));Evidence("running");
            var arm=geometry.Part("RightUpperArm");var rotation=arm.localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(rotation,arm.localRotation),Is.GreaterThan(.1f));
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});Assert.That(geometry.IsPlaying,Is.False);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("cancelled"));Evidence("cancelled");
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);Assert.That(router.Begin(0,ray),Is.False);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;Assert.That(geometry.IsPlaying,Is.True);
            yield return new WaitForSeconds(.95f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));Evidence("completed");
            Assert.That(editor.Read(target).recipe.playing,Is.False);
            var saved=workshop.Selected;string original=saved.program;Assert.That(runtime.Trigger(id),Is.True);saved.name="Changed program";
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=saved}}},out error,out _),Is.True,error);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Editing stops the previous program revision");workshop.Undo();Assert.That(workshop.Selected.name,Is.EqualTo("Programmed wave"));Assert.That(workshop.Selected.program,Is.EqualTo(original));
            workshop.SendMessage("OnApplicationPause",true);var restored=new RuleStorage(directory).Load(out error);Assert.That(restored.sequences.Single(x=>x.id==id).program,Is.EqualTo(original),error);workshop.SendMessage("OnApplicationPause",false);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(workshop.Execute(new RuleRequest {action="play",revision=workshop.Revision,target=id},out error,out _),Is.True,error);
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;Assert.That(geometry.IsPlaying,Is.True);runtime.SendMessage("OnApplicationPause",true);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(geometry.IsPlaying,Is.False);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
