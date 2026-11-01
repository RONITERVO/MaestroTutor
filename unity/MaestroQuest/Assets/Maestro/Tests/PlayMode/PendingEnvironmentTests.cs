// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        JObject EnvironmentAssignment(string target,string profile)=>new(){["target"]=target,["revision"]=editor.ObjectRevision(target),["profileId"]=profile,["profileRevision"]=editor.EnvironmentRevision(profile)};
        [UnityTest] public IEnumerator PendingEnvironmentDefinitionsShareQueueCaptureInputsAndReturnStableReceipts()
        {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            CapabilityOperation first=null,second=null;
            try {
                yield return Prepared(blocker);var context=new CapabilityContext(editor,animations);var module=new EnvironmentProfileSaveCapability();
                Assert.AreEqual("completion",module.Duration);var args=SaveEnvironmentArgs();
                Assert.IsTrue(module.Start(context,"first",args,out first,out error),error);args["name"]="later input";args["realCollisions"]=true;
                Assert.IsTrue(module.Start(context,"second",args,out second,out error),error);
                Assert.IsEmpty(first.Result);Assert.IsEmpty(editor.EnvironmentProfiles());
                blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Preparing,second.State(out error),error);Assert.IsEmpty(editor.EnvironmentProfiles());
                yield return PlacementOperationDone(first);yield return PlacementOperationDone(second);
                string id=(string)first.Result["id"],other=(string)second.Result["id"];
                Assert.AreNotEqual(id,other);Assert.AreEqual("Virtual actors",editor.ReadEnvironment(id).name);
                Assert.IsFalse(editor.ReadEnvironment(id).realCollisions);Assert.IsTrue(editor.ReadEnvironment(other).realCollisions);
                Assert.AreEqual(2,new RoomStorage(directory).Load(out error).environmentProfiles.Length,error);
                int revision=(int)first.Result["revision"];first.Result["revision"]=-1;Assert.AreEqual(revision,(int)first.Result["revision"]);
                editor.Undo();Assert.IsNull(editor.ReadEnvironment(other));Assert.IsNotNull(editor.ReadEnvironment(id));
            }finally{first?.Stop(false);second?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingEnvironmentCancellationAndChangedMembershipCannotApplyOldProfile()
        {
            string id=NewEnvironment();Assert.IsTrue(AssignEnvironment("maestro",id,out var error),error);
            int revision=editor.EnvironmentRevision(id);var change=editor.ReadEnvironment(id);change.realCollisions=true;
            Assert.IsTrue(editor.BeginEnvironment(change,id,revision,new[]{"maestro"},out var cancelled,out error),error);
            change.realCollisions=false;yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(revision,editor.EnvironmentRevision(id));Assert.IsFalse(editor.ReadEnvironment(id).realCollisions);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out error),error);CapabilityOperation waiting=null;
            try {
                yield return Prepared(blocker);var args=EditEnvironmentArgs(id);args["realCollisions"]=true;
                Assert.IsTrue(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"waiting",args,out waiting,out error),error);
                Assert.IsTrue(AssignEnvironment(editor.Identity(block),id,out error),error);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,waiting.State(out error));StringAssert.Contains("membership",error);
                Assert.IsEmpty(waiting.Result);Assert.IsFalse(editor.ReadEnvironment(id).realCollisions);
            }finally{waiting?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingEnvironmentAcceptsOnlyCurrentGeometryWhenARealWallAppears()
        {
            RoomPhysicsLayers.Configure();string id=NewEnvironment();Assert.IsTrue(AssignEnvironment("maestro",id,out var error),error);
            physics.SetSurfaces(true,"Aligned");int revision=editor.ObjectRevision("maestro");
            Assert.IsTrue(editor.BeginEnvironmentBinding("maestro",revision,"",0,out var pending,out error),error);
            using(pending){
                yield return Prepared(pending);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
                wall.transform.position=editor.Find("maestro").transform.position;wall.layer=RoomPhysicsLayers.Scanned;Physics.SyncTransforms();
                Assert.IsFalse(pending.Poll(out _,out error));StringAssert.Contains("clear",error);yield return Drained(pending);
                Assert.AreEqual(revision,editor.ObjectRevision("maestro"));Assert.IsFalse(physics.IncludesRealRoom(editor.Find("maestro")));
                Assert.AreEqual(id,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").environmentProfile,error);
            }
        }
        [UnityTest] public IEnumerator PendingEnvironmentProfileDoesNotRewindUnrelatedMovementOrItsClock()
        {
            string id=NewEnvironment();Assert.IsTrue(AssignEnvironment("maestro",id,out var error),error);
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true},out error),error);
            var definition=editor.ReadEnvironment(id);definition.realCollisions=true;
            Assert.IsTrue(editor.BeginEnvironment(definition,id,editor.EnvironmentRevision(id),new[]{"maestro"},out var pending,out error),error);
            definition.name="mutated source";definition.realCollisions=false;
            using(pending){
                yield return Prepared(pending);var position=block.transform.position+Vector3.right*.2f;block.transform.position=position;editor.RememberPlacement(editor.Identity(block));
                editor.TickWorldTime(.2);editor.TickWorldTime(.2);double now=editor.WorldSecond;
                yield return Completed(pending);Assert.IsTrue(editor.ReadEnvironment(id).realCollisions);Assert.AreEqual("Virtual actors",editor.ReadEnvironment(id).name);
                Assert.IsTrue(physics.IncludesRealRoom(editor.Find("maestro")));Assert.AreEqual(position,block.transform.position);Assert.GreaterOrEqual(editor.WorldSecond,now);
                Assert.IsTrue(editor.HasUnsavedChanges);editor.Undo();Assert.IsFalse(editor.ReadEnvironment(id).realCollisions);Assert.AreEqual(position,block.transform.position);
            }
        }
        [UnityTest] public IEnumerator PendingEnvironmentReceiptsKeepAcceptedRevisionAfterASecondHumanEdit()
        {
            var module=new EnvironmentProfileSaveCapability();Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"create",SaveEnvironmentArgs(),out var operation,out var error),error);
            try {
                float end=Time.realtimeSinceStartup+20;
                while(operation.State(out error)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,end,error);yield return null;}
                Assert.AreEqual(RuleActionState.Ready,operation.State(out error),error);var profile=editor.EnvironmentProfiles().Single();int accepted=editor.EnvironmentRevision(profile.id);
                profile.name="Human's newer name";Assert.IsTrue(editor.EditEnvironment(profile,profile.id,accepted,Array.Empty<string>(),out error),error);
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(accepted,(int)operation.Result["revision"]);
                Assert.AreEqual("Human's newer name",editor.ReadEnvironment(profile.id).name);Assert.AreNotEqual(accepted,editor.EnvironmentRevision(profile.id));
            }finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingEnvironmentBindingCapturesInputsAndRefusesChangedWaitingTarget()
        {
            string id=NewEnvironment(),target=editor.Identity(block);var context=new CapabilityContext(editor,animations);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var args=EnvironmentAssignment(target,id);
                Assert.IsTrue(new EnvironmentAssignCapability().Start(context,"assign",args,out operation,out error),error);
                args["profileId"]="";args["profileRevision"]=0;blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                Assert.AreEqual(id,editor.Read(target).environmentProfile);Assert.AreEqual(target,(string)operation.Result["target"]);
            }finally{operation?.Stop(false);blocker.Dispose();}
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out blocker,out error),error);operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(new EnvironmentAssignCapability().Start(context,"inherit",EnvironmentAssignment(target,""),out operation,out error),error);
                Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("target changed",error);
                Assert.IsEmpty(operation.Result);Assert.AreEqual(id,editor.Read(target).environmentProfile);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingEnvironmentAcceptedStopReportsResourceAndDrainsWithoutAResult()
        {
            string id=Guid.NewGuid().ToString("N");var definition=new RoomEnvironmentProfile{id=id,name="Virtual actors",realCollisions=false};
            using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;
            Assert.IsTrue(PersistedEditOperation.Start(editor,null,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginEnvironment(definition,id,0,Array.Empty<string>(),out edit,out issue,pause.Check);pending=edit;return ok;},
                out var operation,out var error,validate:(out string issue)=>editor.CanEditEnvironment(definition,id,0,Array.Empty<string>(),out issue),
                output:()=>new JObject{["id"]=id,["revision"]=pending.AcceptedEnvironmentRevision(id)}),error);
            try {
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains(id,operation.InterruptionStatus);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);
                Assert.IsFalse(operation.Complete(out _));Assert.IsNotNull(editor.ReadEnvironment(id));
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(id,new RoomStorage(directory).Load(out error).environmentProfiles.Single().id,error);
            editor.Undo();Assert.IsNull(editor.ReadEnvironment(id));
        }
        [UnityTest] public IEnumerator PendingEnvironmentNoopDoesNotAddUndoAndRemovalHasAConfirmedZeroRevision()
        {
            string id=NewEnvironment();int revision=editor.EnvironmentRevision(id);var args=EditEnvironmentArgs(id);
            Assert.IsTrue(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"same",args,out var same,out var error),error);
            yield return PlacementOperationDone(same);Assert.AreEqual(revision,(int)same.Result["revision"]);
            editor.Undo();Assert.IsNull(editor.ReadEnvironment(id));editor.Redo();
            var removeArgs=new JObject{["id"]=id,["revision"]=editor.EnvironmentRevision(id)};
            Assert.IsTrue(new EnvironmentProfileRemoveCapability().Start(new CapabilityContext(editor,animations),"remove",removeArgs,out var remove,out error),error);
            Assert.IsEmpty(remove.Result);yield return PlacementOperationDone(remove);Assert.AreEqual(0,(int)remove.Result["revision"]);Assert.IsNull(editor.ReadEnvironment(id));
            Assert.IsEmpty(new RoomStorage(directory).Load(out error).environmentProfiles,error);editor.Undo();Assert.IsNotNull(editor.ReadEnvironment(id));
            Assert.IsTrue(remove.Complete(out error),error);Assert.IsNotNull(editor.ReadEnvironment(id),"Receipt observation must not remove it again");
        }
        [UnityTest] public IEnumerator PendingEnvironmentTemporaryCreationIsDiscardableAndKeepPersistsIt()
        {
            foreach(bool keep in new[]{false,true}) {
                Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);while(editor.TemporarySavePending)yield return null;
                Assert.IsTrue(new EnvironmentProfileSaveCapability().Start(new CapabilityContext(editor,animations),"temporary",SaveEnvironmentArgs(),out var operation,out error),error);
                yield return PlacementOperationDone(operation);string id=(string)operation.Result["id"];Assert.IsTrue((bool)operation.Result["temporary"]);
                Assert.IsEmpty(new RoomStorage(directory).Load(out error).environmentProfiles,error);
                if(keep)Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);else Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                while(editor.TemporarySavePending)yield return null;
                Assert.AreEqual(keep,editor.ReadEnvironment(id)!=null);
                Assert.AreEqual(keep,new RoomStorage(directory).Load(out error).environmentProfiles.Any(x=>x.id==id),error);
            }
        }
    }
}
