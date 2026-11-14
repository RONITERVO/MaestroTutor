// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator SavedEnvironmentClientCanDiscoverDeferredCreationAndWorldMembership(){
            var features=RoomControls.Capabilities(editor);
            Assert.That(features,Does.Contain(CreationLoading.Feature));
            Assert.That(features,Does.Contain(RoomAgentWire.SnapshotFeature));
            Assert.That(features,Does.Contain(EnvironmentProfileCapability.WorldFeature));
            yield return null;
        }
        [UnityTest]public IEnumerator SavedEnvironmentLargeClientFramesReachRevisionChecksWithoutMutation(){
            var agent=root.AddComponent<RoomAgent>();agent.Initialize(editor,null);string client=Guid.NewGuid().ToString("N");
            agent.Receive(new JObject{["clientId"]=client}.ToString());string session=agent.Observe().session;
            var members=new JArray(Enumerable.Range(0,256).Select(i=>new JObject{["target"]=i.ToString("x32"),["revision"]=int.MaxValue}));
            members.Add(new JObject{["target"]="book",["revision"]=int.MaxValue});members.Add(new JObject{["target"]="maestro",["revision"]=int.MaxValue});
            var args=new JObject{["id"]=Guid.NewGuid().ToString("N"),["revision"]=int.MaxValue,["membership"]=new string('a',32),["name"]="Landscape",["realCollisions"]=false,["members"]=members};
            var call=new JObject{["id"]="environment.profile.save",["version"]=2,["arguments"]=args};
            Assert.That(RoomCapabilityCatalog.ValidCall(call),Is.True);
            var request=new JObject{["version"]=2,["session"]=session,["sequence"]=1,["sceneRevision"]=editor.Revision,
                ["conditions"]=new JArray(members.Select(x=>new JObject{["id"]=x["target"].DeepClone(),["revision"]=x["revision"].DeepClone()})),
                ["commands"]=new JArray(new JObject{["action"]="execution",["execution"]=new JObject{["operation"]="start",["call"]=call}})};
            var frame=new JObject{["clientId"]=client,["session"]=session,["request"]=request};
            string before=JsonUtility.ToJson(editor.Snapshot()),wire=frame.ToString(Newtonsoft.Json.Formatting.None);
            Assert.That(wire.Length,Is.GreaterThan(RoomAgentWire.LegacySnapshotCharacters));
            agent.Receive(wire);var refused=agent.Observe();Assert.That(refused.ack,Is.EqualTo(1));Assert.That(refused.ok,Is.False);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            request["sequence"]=2;wire=frame.ToString(Newtonsoft.Json.Formatting.None).PadRight(RoomAgentWire.MaximumSnapshotCharacters);
            agent.Receive(wire+" ");Assert.That(agent.Observe().ack,Is.EqualTo(1),"Oversized frames never enter the inbox");
            agent.Receive(wire);Assert.That(agent.Observe().ack,Is.EqualTo(2),"The exact bounded frame reaches revision validation");
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));yield return null;
        }
        JObject EnvironmentMember(string target)=>new(){["target"]=target,["revision"]=editor.ObjectRevision(target)};
        JObject EditEnvironmentArgs(string id){
            var summary=editor.ObserveEnvironmentSummary(id);
            return new JObject{["id"]=id,["revision"]=summary["revision"].DeepClone(),["membership"]=summary["membership"].DeepClone(),
                ["name"]=summary["name"].DeepClone(),["realCollisions"]=summary["realCollisions"].DeepClone(),
                ["members"]=new JArray(editor.EnvironmentMembers(id).Select(EnvironmentMember))};
        }
        [UnityTest]public IEnumerator SavedEnvironmentWholeWorldUsesPagedGuardsWithoutNativeAdmission(){
            yield return WaitForModuleLibrary();string profile=NewEnvironment(),area=NewRegion();
            foreach(var item in editor.Snapshot().objects){
                Assert.That(AssignEnvironment(item.id,profile,out var issue),Is.True,issue);
                if(!item.IsBuiltIn)Assert.That(AssignRegion(item.id,area,out issue),Is.True,issue);
            }
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));
            int remaining=RoomDocument.MaximumObjects+2-editor.Snapshot().objects.Length;
            var prototype=new CreationPrototype{kind="block",physics=new ObjectPhysicsSettings{mode="fixed",shape="automatic",mass=.5f}};
            while(remaining>0){
                int count=Math.Min(16,remaining);
                var batch=new CreationBatch{position=new Vector3(2,1,1),blueprint=new CreationBlueprint{pieces=Enumerable.Range(0,count).Select(i=>new CreationPiece{
                    slot="piece_"+i,name="Landscape piece",source=new CreationSource{kind="prototype",prototype=prototype}}).ToArray()}};
                Assert.That(editor.CreateBatch(batch,out var created,out var issue),Is.True,issue);
                foreach(string id in created){
                    Assert.That(AssignEnvironment(id,profile,out issue),Is.True,issue);
                    Assert.That(AssignRegion(id,area,out issue),Is.True,issue);
                }
                yield return NativeAreaCompletion(editor.RetireNativeArea(area));remaining-=count;
            }
            var members=editor.EnvironmentMembers(profile);Assert.That(members.Length,Is.EqualTo(258));
            var cold=members.Where(editor.NativeEntityDormant).ToArray();Assert.That(cold.Length,Is.EqualTo(256));
            var original=editor.Snapshot().objects.Select(JsonUtility.ToJson).ToArray();
            var leases=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try{
                var summary=editor.ObserveEnvironmentSummary(profile);var discovered=new List<string>();var pages=new JArray();string cursor="";
                do{
                    var page=editor.ObserveResourceMembers("environment",profile,cursor);Assert.That(page,Is.Not.Null);
                    Assert.That((string)page["membership"],Is.EqualTo((string)summary["membership"]));
                    Assert.That((int)page["total"],Is.EqualTo(258));Assert.That(((JArray)page["members"]).Count,Is.LessThanOrEqualTo(16));
                    discovered.AddRange(((JArray)page["members"]).Values<string>());cursor=(string)page["next"];
                    pages.Add(page);
                }while(cursor.Length>0);
                Assert.That(discovered,Is.EqualTo(members));Assert.That(editor.ObserveEnvironmentProfile(profile),Is.Null,"Never truncate the compatibility fact");
                Assert.That(EnvironmentProfileFacts.Profile().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=profile},out _),Is.False);
                Assert.That(EnvironmentProfileFacts.Summary().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=profile},out _),Is.True);
                var args=EditEnvironmentArgs(profile);args["realCollisions"]=true;
                var call=new JObject{["id"]="environment.profile.save",["version"]=2,["arguments"]=args};
                var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(call);
                request.conditions=members.Select(id=>new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}).ToArray();
                // A fresh outer read set cannot authorize a stale member on the final page.
                args["members"][257]["revision"]=(int)args["members"][257]["revision"]-1;
                Assert.That(executor.Execute(request,out var error,out _),Is.False);
                Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);
                args["members"][257]["revision"]=editor.ObjectRevision(members[257]);
                request=TemplateRequest(call);request.conditions=members.Select(id=>new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}).ToArray();
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);
                yield return SpatialSettingsCompletion.Wait(executor.Executions);
                var receipt=executor.Executions.Observe()["selected"];
                Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
                Assert.That(((JArray)receipt["resources"]).Values<string>(),Is.EquivalentTo(members));
                Assert.That(new RoomStorage(directory).Load(out error).environmentProfiles.Single(x=>x.id==profile).realCollisions,Is.True,error);
                editor.Undo();Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);
                Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False,"Replaying a receipt must not redo an undone edit");
                editor.Redo();Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.True);
                var before=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;
                var definition=editor.ReadEnvironment(profile);definition.realCollisions=false;
                Assert.That(editor.BeginEnvironment(definition,profile,editor.EnvironmentRevision(profile),members,out var pending,out error),Is.True,error);
                try{yield return Prepared(pending);}finally{pending.Dispose();}yield return Drained(pending);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
                string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
                try{
                    Assert.That(editor.EditEnvironment(definition,profile,editor.EnvironmentRevision(profile),members,out error),Is.False);
                    Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
                    CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                }finally{Directory.Delete(obstacle);}
                Assert.That(cold.All(id=>editor.NativeEntityDormant(id)&&!editor.Find(id)),Is.True);
                Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(NativeObjectResources.MaximumInstances));
                Assert.That(editor.NativeActivationPending,Is.False);
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_ENTITY_ENVIRONMENT");
                if(!string.IsNullOrEmpty(evidence)){
                    Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"whole-world.json"),new JObject{
                        ["call"]=call.DeepClone(),["receipt"]=receipt.DeepClone(),["summary"]=summary,["pages"]=pages,
                        ["savedMembers"]=members.Length,["dormantMembers"]=cold.Length,["nativeInstances"]=(int)NativeObjectResources.ObserveBudget()["instances"],
                        ["undoRedo"]=true,["cancelPreserved"]=true,["failedSavePreserved"]=true,["headsetMeasured"]=false}.ToString());
                }
            }finally{foreach(var lease in leases)lease.Dispose();}
            Assert.That(editor.Snapshot().objects.Select(JsonUtility.ToJson),Is.EqualTo(original));
        }
        [UnityTest]public IEnumerator SavedEnvironmentEditsStayDormantAtCapacityAndUndoPreservesPose(){
            string target=editor.Identity(block),profile=NewEnvironment();
            yield return NativeAreaCompletion(editor.RetireNativeArea(NativeAreaFor(target)));
            var pose=JsonUtility.ToJson(editor.Read(target));var leases=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
            try{
                int nativeCount=(int)NativeObjectResources.ObserveBudget()["instances"];
                var context=new CapabilityContext(editor,animations);
                Assert.That(new EnvironmentAssignCapability().Start(context,"assign",EnvironmentAssignment(target,profile),out var assign,out var error),Is.True,error);
                yield return PlacementOperationDone(assign);
                Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
                var saved=editor.Read(target);saved.environmentProfile="";Assert.That(JsonUtility.ToJson(saved),Is.EqualTo(pose));
                var args=EditEnvironmentArgs(profile);args["realCollisions"]=true;
                Assert.That(new EnvironmentProfileSaveCapability().Start(context,"edit",args,out var edit,out error),Is.True,error);
                yield return PlacementOperationDone(edit);
                Assert.That((bool)editor.ObserveEnvironmentSettings(target)["realCollisions"],Is.True);
                Assert.That(EnvironmentProfileFacts.Summary().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=profile},out _),Is.True);
                var summary=editor.ObserveEnvironmentSummary(profile);var page=editor.ObserveResourceMembers("environment",profile,"");
                Assert.That((string)summary["membership"],Is.EqualTo((string)page["membership"]));Assert.That((int)summary["memberCount"],Is.EqualTo(1));
                Assert.That(summary["members"],Is.Null);Assert.That(summary["state"],Is.Null);
                editor.Undo();Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);
                editor.Undo();Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(pose));
                editor.Redo();editor.Redo();Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.True);
                Assert.That(editor.Read(target).environmentProfile,Is.EqualTo(profile));Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
                Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(nativeCount));Assert.That(editor.NativeActivationPending,Is.False);
            }finally{foreach(var lease in leases)lease.Dispose();}
        }
        [UnityTest]public IEnumerator SavedEnvironmentRestorationUsesNewPolicyAndHoldsInsideNewWall(){
            string target=editor.Identity(block),profile=NewEnvironment(),area=NativeAreaFor(target);Vector3 pose=block.transform.position;
            Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);
            yield return NativeAreaCompletion(editor.RetireNativeArea(area));RestorationWall(pose);
            var args=EditEnvironmentArgs(profile);args["realCollisions"]=true;
            Assert.That(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"edit",args,out var operation,out error),Is.True,error);
            yield return PlacementOperationDone(operation);int revision=editor.Revision;
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
            Assert.That(RestoredBinding(target).RestorationHeld,Is.True);Assert.That(physics.IncludesRealRoom(editor.Find(target)),Is.True);
            Assert.That(physics.CanSimulate(pose,editor.Find(target)),Is.False);Assert.That(editor.Find(target).transform.position,Is.EqualTo(pose));
            Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator SavedEnvironmentMixedProfileRefusesLiveCollisionWithoutEditingDormantMember(){
            string target=editor.Identity(block),profile=NewEnvironment();
            Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);Assert.That(AssignEnvironment("maestro",profile,out error),Is.True,error);
            yield return NativeAreaCompletion(editor.RetireNativeArea(NativeAreaFor(target)));RestorationWall(editor.Find("maestro").transform.position);
            int revision=editor.Revision;var args=EditEnvironmentArgs(profile);args["realCollisions"]=true;
            Assert.That(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"edit",args,out _,out error),Is.False);
            Assert.That(error,Does.Contain("clear"));Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);
            Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator SavedEnvironmentMembershipUndoAndMemberRevisionEachInvalidateTheRead(){
            string profile=NewEnvironment(),target=editor.Identity(block);Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);
            var args=EditEnvironmentArgs(profile);int revision=editor.EnvironmentRevision(profile);
            Assert.That(AssignEnvironment("book",profile,out error),Is.True,error);editor.Undo();
            Assert.That(editor.EnvironmentRevision(profile),Is.EqualTo(revision));Assert.That(editor.EnvironmentMembers(profile),Is.EqualTo(new[]{target}));
            var module=new EnvironmentProfileSaveCapability();var context=new CapabilityContext(editor,animations);
            Assert.That(module.CanRun(context,args,out error),Is.False);Assert.That(error,Does.Contain("membership"));
            args=EditEnvironmentArgs(profile);Assert.That(editor.PaintObject(target,Color.red,out error),Is.True,error);
            Assert.That(module.CanRun(context,args,out error),Is.False);Assert.That(error,Does.Contain("saved revision"));
            args=EditEnvironmentArgs(profile);((JArray)args["members"]).Add(EnvironmentMember(target));
            Assert.That(module.CanRun(context,args,out error),Is.False);Assert.That(error,Does.Contain("exactly once"));yield return null;
        }
        [UnityTest]public IEnumerator SavedEnvironmentQueuedEditCapturesMemberGuardsAndRefusesNewRevision(){
            string profile=NewEnvironment(),target=editor.Identity(block);Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);
            Assert.That(editor.BeginRotation("book",Quaternion.identity,out var blocker,out error),Is.True,error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);var args=EditEnvironmentArgs(profile);args["realCollisions"]=true;
                Assert.That(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"wait",args,out operation,out error),Is.True,error);
                Assert.That(editor.PaintObject(target,Color.red,out error),Is.True,error);
                args["members"][0]["revision"]=editor.ObjectRevision(target);blocker.Dispose();yield return Drained(blocker);
                Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("saved revision"));
                Assert.That(operation.Result,Is.Empty);Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest]public IEnumerator SavedEnvironmentPreparedEditRechecksMemberRevisionBeforeAcceptance(){
            string profile=NewEnvironment(),target=editor.Identity(block);Assert.That(AssignEnvironment(target,profile,out var error),Is.True,error);
            var args=EditEnvironmentArgs(profile);var revisions=editor.EnvironmentMembers(profile).ToDictionary(id=>id,id=>editor.ObjectRevision(id));
            var definition=editor.ReadEnvironment(profile);definition.realCollisions=true;int revision=editor.EnvironmentRevision(profile);string membership=(string)args["membership"];
            Assert.That(editor.BeginEnvironment(definition,profile,revision,revisions.Keys.ToArray(),out var pending,out error,
                validateSnapshot:()=>editor.CheckEnvironmentSnapshot(profile,revision,membership,revisions)),Is.True,error);
            using(pending){
                yield return Prepared(pending);block.transform.position+=Vector3.right*.1f;editor.RememberPlacement(target);
                Assert.That(pending.Poll(out _,out error),Is.False);Assert.That(error,Does.Contain("saved revision"));yield return Drained(pending);
                Assert.That(editor.ReadEnvironment(profile).realCollisions,Is.False);Assert.That(editor.EnvironmentRevision(profile),Is.EqualTo(revision));
            }
        }
        [UnityTest]public IEnumerator SavedEnvironmentTemporaryKeepAndDiscardRetainDormancy(){
            string target=editor.Identity(block),profile=NewEnvironment();yield return NativeAreaCompletion(editor.RetireNativeArea(NativeAreaFor(target)));
            foreach(bool keep in new[]{false,true}){
                Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
                Assert.That(new EnvironmentAssignCapability().Start(new CapabilityContext(editor,animations),"assign",EnvironmentAssignment(target,profile),out var operation,out error),Is.True,error);
                yield return PlacementOperationDone(operation);Assert.That((bool)operation.Result["temporary"],Is.True);
                if(keep)Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);else Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
                while(editor.TemporarySavePending)yield return null;
                Assert.That(editor.Read(target).environmentProfile,Is.EqualTo(keep?profile:""));
                Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
                Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).environmentProfile,Is.EqualTo(keep?profile:""),error);
            }
        }
        [UnityTest]public IEnumerator SavedEnvironmentDoesNotTreatALostNativeViewAsDormant(){
            string target=editor.Identity(block),profile=NewEnvironment();UnityEngine.Object.Destroy(block.gameObject);yield return null;
            Assert.That(new EnvironmentAssignCapability().CanRun(new CapabilityContext(editor,animations),EnvironmentAssignment(target,profile),out _),Is.False);
            Assert.That(editor.Read(target).environmentProfile,Is.Empty);
        }
    }
}
