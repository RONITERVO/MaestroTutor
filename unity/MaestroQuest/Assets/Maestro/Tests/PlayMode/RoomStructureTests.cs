// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject StructureSaveCall(params string[] ids)=>new() {["id"]="structure.save",["version"]=1,["arguments"]=new JObject {["id"]="",["revision"]=0,["source"]=new JObject {["kind"]="capture",["name"]="Castle",["positionTolerance"]=.05,["rotationTolerance"]=15,["scaleTolerance"]=.05,["members"]=new JArray(ids.Select((id,i)=>new JObject {["slot"]="piece_"+i,["target"]=id}))}}};
        JObject StructureResetCall(string id,int revision,params string[] ids)=>new() {["id"]="structure.reset",["version"]=1,["arguments"]=new JObject {["id"]=id,["revision"]=revision,["members"]=new JArray(ids)}};
        RoomAgentRequest StructureRequest(JObject call) {
            var request=TemplateRequest(call);
            request.conditions=CapabilityArguments.Resources((JObject)call["arguments"],CapabilityModules.All.Single(module=>module.Id==(string)call["id"]&&module.Version==(int)call["version"]).InputSchema).Select(id=>new RoomObjectCondition {id=id,revision=editor.ObjectRevision(id)}).ToArray();
            return request;
        }
        string SaveGroup(RoomAgentExecutor executor,params string[] ids){Assert.That(executor.Execute(StructureRequest(StructureSaveCall(ids)),out var error,out _),Is.True,error);return (string)executor.Executions.Observe()["selected"]["output"]["structureId"];}
        [UnityTest] public IEnumerator HighJournalRevisionsRoundTripThroughNativeFactsProgramBindingsAndReset()
        {
            string a=LayoutObject(new Vector3(2,1,0)),b=LayoutObject(new Vector3(2.25f,1,0));
            var journal=(RoomJournal)typeof(RoomEditor).GetField("journal",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
            var clock=typeof(RoomJournal).GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(journal);
            clock.GetType().GetField("Next").SetValue(clock,int.MaxValue-1000);
            editor.Find(a).transform.localPosition+=Vector3.right*.2f;editor.RememberPlacement(a);int revision=editor.ObjectRevision(a);Assert.That(revision,Is.GreaterThan(1000000));
            var actions=new RoomRuleActions(editor,animations);Assert.That(actions.TryRead("object.definition",1,new JObject {["target"]=a},out var before),Is.True);Assert.That((int)((JObject)before.Value)["revision"],Is.EqualTo(revision));
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-structure-reset.json")).Replace(new string('a',32),a).Replace(new string('b',32),b);
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="high",sequence=new RuleSequence {id="",name="High revision structure",program=source}}}},out var error,out var sequences),Is.True,error);
            Assert.That(runtime.Trigger(sequences.Single()),Is.True,runtime.Scheduler.LastError);for(int i=0;i<30&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);var definition=editor.Structures().Single();int groupRevision=editor.StructureRevision(definition.id);Assert.That(groupRevision,Is.GreaterThan(1000000));
            Assert.That(actions.TryRead("structure.definition",1,new JObject {["id"]=definition.id},out var fact),Is.True);Assert.That((int)((JObject)fact.Value)["revision"],Is.EqualTo(groupRevision));
            var executor=new RoomAgentExecutor(editor);editor.Find(a).transform.localPosition+=Vector3.right;
            Assert.That(executor.Execute(StructureRequest(StructureResetCall(definition.id,groupRevision,a,b)),out error,out _),Is.True,error);Assert.That(editor.ObserveStructure(definition).Displaced,Is.Zero);
            Assert.That(executor.Execute(StructureRequest(StructureResetCall(definition.id,groupRevision-1,a,b)),out error,out _),Is.False,"Large stale revisions must still fail");
        }
        [UnityTest] public IEnumerator OneProgramBuildsCapturesDisplacesAndResetsItsOwnPiecesWithTemporaryDiscard() {
            var before=editor.Snapshot().objects.Select(o=>o.id).ToHashSet();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            float end=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;Assert.That(editor.TemporarySavePending,Is.False);
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-build-structure.json"));
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="built",sequence=new RuleSequence {id="",name="Build and restore castle",program=source}}}},out error,out var ids),Is.True,error);
            Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);for(int i=0;i<50&&runtime.Scheduler.RunningCount>0;i++){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);var group=editor.Structures().Single();Assert.That(group.slots.Length,Is.EqualTo(6));Assert.That(editor.ObserveStructure(group).Displaced,Is.Zero);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before.Count+6));Assert.That(group.slots.All(slot=>!before.Contains(slot.placement.target)),Is.True);
            editor.Undo();Assert.That(editor.ObserveStructure(group).Displaced,Is.EqualTo(1),"Undo the final reset must recover the program's displaced member");
            Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(before.Count));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.Structures(),Is.Empty);Assert.That(editor.Snapshot().objects.Select(o=>o.id),Is.EquivalentTo(before));
        }
        [UnityTest] public IEnumerator SharedStructureCaptureObservesDisplacementAndResetUsesLivePoseUndo()
        {
            string a=LayoutObject(new Vector3(2,1,0)),b=LayoutObject(new Vector3(2.25f,1,0));var executor=new RoomAgentExecutor(editor);string id=SaveGroup(executor,a,b);int revision=editor.StructureRevision(id);
            Assert.That(new RoomStorage(directory).Load(out _).structures.Single().id,Is.EqualTo(id));editor.Find(a).transform.localPosition=new Vector3(3,2,0);
            var actions=new RoomRuleActions(editor,animations);Assert.That(actions.TryRead("structure.state",1,new JObject {["id"]=id},out var state),Is.True);Assert.That((int)((JObject)state.Value)["displaced"],Is.EqualTo(1));
            var call=StructureResetCall(id,revision,a,b);var request=StructureRequest(call);Assert.That(executor.Execute(request,out var error,out _),Is.True,error);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(2));
            editor.Undo();Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(3));Assert.That(editor.StructureRevision(id),Is.EqualTo(revision));Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(3),"Receipt replay must not reset a second time");
            Assert.That(editor.DeleteObject(b,out error),Is.True,error);Assert.That(editor.ReadStructure(id).slots.Length,Is.EqualTo(2));Assert.That(editor.ObserveStructure(editor.ReadStructure(id)).Missing,Is.EqualTo(1));
            Assert.That(executor.Execute(StructureRequest(call),out error,out _),Is.False);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(3),"Missing members cannot produce a partial reset");yield return null;
        }
        [UnityTest] public IEnumerator StructureDefinitionsFollowTemporaryKeepDiscardAndSavedUndo()
        {
            string a=LayoutObject(new Vector3(2,1,0));var executor=new RoomAgentExecutor(editor);string id=SaveGroup(executor,a);int revision=editor.StructureRevision(id);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);float end=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;Assert.That(editor.TemporarySavePending,Is.False);
            var definition=editor.ReadStructure(id);definition.name="Saved castle";Assert.That(editor.SaveStructure(definition,revision,out error),Is.True,error);Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);end=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.TemporarySaveError,Is.Null);
            definition=editor.ReadStructure(id);definition.name="Discard me";Assert.That(editor.SaveStructure(definition,editor.StructureRevision(id),out error),Is.True,error);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.ReadStructure(id).name,Is.EqualTo("Saved castle"));
            Assert.That(new RoomStorage(directory).Load(out _).structures.Single().name,Is.EqualTo("Saved castle"));editor.Undo();Assert.That(editor.ReadStructure(id).name,Is.EqualTo("Castle"));Assert.That(editor.StructureRevision(id),Is.GreaterThan(revision));
        }
        [UnityTest] public IEnumerator StructureSaveFailureAndStaleEditsPreserveMembersAndForgetIsUndoable()
        {
            string a=LayoutObject(new Vector3(2,1,0));var executor=new RoomAgentExecutor(editor);string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(executor.Execute(StructureRequest(StructureSaveCall(a)),out _,out _),Is.False);Assert.That(editor.Structures(),Is.Empty);Assert.That(editor.Find(a),Is.Not.Null);}finally{Directory.Delete(obstacle);}
            string id=SaveGroup(executor,a);int revision=editor.StructureRevision(id);var definition=editor.ReadStructure(id);definition.name="Changed";Assert.That(editor.SaveStructure(definition,revision,out var error),Is.True,error);
            Assert.That(editor.ResetStructure(id,revision,new[]{a},out _),Is.False);Assert.That(editor.ForgetStructure(id,revision,out _),Is.False);
            var call=new JObject {["id"]="structure.forget",["version"]=1,["arguments"]=new JObject {["id"]=id,["revision"]=editor.StructureRevision(id)}};Assert.That(executor.Execute(StructureRequest(call),out error,out _),Is.True,error);Assert.That(editor.ReadStructure(id),Is.Null);Assert.That(editor.Find(a),Is.Not.Null);editor.Undo();Assert.That(editor.ReadStructure(id).name,Is.EqualTo("Changed"));yield return null;
        }
        [UnityTest] public IEnumerator PersistentStructureDetectsRealBallKnockdownAndResetsItsSavedBaseline() {
            RoomPhysicsLayers.Configure();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(2,-.1f,0);floor.transform.localScale=new Vector3(3,.2f,3);floor.layer=RoomPhysicsLayers.Scanned;
            var executor=new RoomAgentExecutor(editor);var entry=CreationTemplates.All.First(e=>e.Id=="brick");var ids=new string[6];
            for(int i=0;i<ids.Length;i++) {Assert.That(editor.CreateRecipe("Castle brick",new Vector3(2+(i%2)*.25f,.04f+(i/2)*.08f,0),1,entry.Recipe,entry.Collision,entry.Physics,out ids[i],out var error),Is.True,error);}
            var baseline=new RoomLayout {placements=ids.Select(id=>editor.Frame.Placement(id,editor.Find(id).transform)).ToArray()};Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic castle floor");physics.StartPhysics();
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ids[4]).transform.localPosition.y,Is.GreaterThan(.12f),"The initial top brick must settle on its stack");
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Knockdown ball",new Vector3(2,.16f,-.6f),.7f,Color.white,out var ballId,out var createError),Is.True,createError);
            // Reset the projectile too: leaving it inside the rebuilding stack makes
            // subsequent stability a different physical question than layout reset.
            baseline.placements=baseline.placements.Append(editor.Frame.Placement(ballId,editor.Find(ballId).transform)).ToArray();
            var save=StructureSaveCall(baseline.placements.Select(p=>p.target).ToArray());var source=(JObject)save["arguments"]["source"];source["kind"]="definition";source.Remove("members");source["slots"]=new JArray(baseline.placements.Select((p,i)=>new JObject {["slot"]="piece_"+i,["placement"]=JObject.Parse(JsonUtility.ToJson(p))}));
            Assert.That(executor.Execute(StructureRequest(save),out var groupError,out _),Is.True,groupError);string group=(string)executor.Executions.Observe()["selected"]["output"]["structureId"];int groupRevision=editor.StructureRevision(group);
            // Observe only the castle. The projectile is part of reset, not part of
            // the collapse predicate: moving the ball alone must not count as a hit.
            string watched=SaveGroup(executor,ids);var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-structure-watch.json")));
            program["state"][0]["initial"]=watched;program["state"][1]["initial"]=editor.StructureRevision(watched);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="watchCastle",sequence=new RuleSequence {id="",name="Watch castle and rearm",program=program.ToString(Newtonsoft.Json.Formatting.None)}}}}}}},out var watchError,out var saved),Is.True,watchError);
            string watcher=saved.Single();var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);workshop.Modules.Flush();string moduleHash=(string)program["imports"][0]["hash"];
            void Capture(string phase){string output=Environment.GetEnvironmentVariable("MAESTRO_STRUCTURE_WATCH_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);state.catalog=executor.Catalog.Observe();File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
            Assert.That(executor.Catalog.Execute(new JObject {["operation"]="search",["category"]="modules",["query"]="Structure state waits",["offset"]=0},out _),Is.True);Capture("library");
            Assert.That(executor.Catalog.Execute(new JObject {["operation"]="inspect",["category"]="modules",["capability"]=moduleHash,["version"]=1},out _),Is.True);Assert.That((bool)executor.Catalog.Observe()["included"],Is.True);Capture("saved");Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Saving an example must not run it");
            Assert.That(runtime.Trigger(watcher),Is.True,runtime.Scheduler.LastError);yield return new WaitForSeconds(.5f);
            string WatchValue(string name)=>runtime.Scheduler.ObserveRuns().Single(r=>r.sequenceId==watcher).state.Single(v=>v.name==name).value;
            Assert.That(WatchValue("phase"),Is.EqualTo("waitingForDisturbance"));Assert.That(runtime.Scheduler.TargetsBusy(ids),Is.False);Capture("armed");
            yield return new WaitForFixedUpdate();var ballBody=editor.Find(ballId).GetComponent<Rigidbody>();ballBody.position=root.transform.TransformPoint(baseline.placements.Last().position);ballBody.angularVelocity=Vector3.zero;ballBody.linearVelocity=Vector3.forward*3;
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(ids.Any(id=>Vector3.Distance(editor.Find(id).transform.localPosition,baseline.placements.Single(p=>p.target==id).position)>.1f),Is.True,"A real ball contact must displace at least one brick");
            Assert.That(editor.ObserveStructure(editor.ReadStructure(group)).Displaced,Is.GreaterThan(0));
            Assert.That(WatchValue("cycles"),Is.EqualTo("1"),runtime.Scheduler.LastError);Assert.That(WatchValue("phase"),Is.EqualTo("waitingForRebuild"));Capture("disturbed");
            Assert.That(executor.Execute(StructureRequest(StructureResetCall(group,groupRevision,baseline.placements.Select(p=>p.target).ToArray())),out var error2,out _),Is.True,error2);
            Assert.That(editor.ObserveStructure(editor.ReadStructure(group)).Displaced,Is.EqualTo(0));
            foreach(var p in baseline.placements) {var item=editor.Find(p.target);Assert.That(Vector3.Distance(item.transform.localPosition,p.position),Is.LessThan(.001f));Assert.That(item.GetComponent<Rigidbody>().linearVelocity,Is.EqualTo(Vector3.zero));}
            Assert.That(physics.Running,Is.True);for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();Assert.That(editor.Find(ids[4]).transform.localPosition.y,Is.GreaterThan(.12f),"Rebuilt top brick: "+editor.Find(ids[4]).transform.localPosition+"; reset ball: "+editor.Find(ballId).transform.localPosition);
            Assert.That(WatchValue("phase"),Is.EqualTo("waitingForDisturbance"));Assert.That(WatchValue("cycles"),Is.EqualTo("1"));Capture("rebuilt");
            var changed=editor.ReadStructure(watched);changed.name="Changed definition";Assert.That(editor.SaveStructure(changed,editor.StructureRevision(watched),out var changedError),Is.True,changedError);
            // The watch includes a stability interval and frame-budgeted program
            // steps. Wait for its observable completion, not half a second that
            // can elapse in one slow editor frame before the steps are serviced.
            float stoppedBy=Time.realtimeSinceStartup+5;
            while(runtime.Scheduler.RunningCount>0&&Time.realtimeSinceStartup<stoppedBy)yield return null;
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,runtime.Scheduler.LastError);Assert.That(runtime.Scheduler.Outcomes.Last().nodeId,Is.EqualTo("stop_disturbed"));Capture("changed");

        }
    }
}
