// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
        JObject EnvironmentMember(string target)=>new(){["target"]=target,["revision"]=editor.ObjectRevision(target)};
        JObject EditEnvironmentArgs(string id){
            var summary=editor.ObserveEnvironmentSummary(id);
            return new JObject{["id"]=id,["revision"]=summary["revision"].DeepClone(),["membership"]=summary["membership"].DeepClone(),
                ["name"]=summary["name"].DeepClone(),["realCollisions"]=summary["realCollisions"].DeepClone(),
                ["members"]=new JArray(editor.EnvironmentMembers(id).Select(EnvironmentMember))};
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
