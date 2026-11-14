// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject PendingRegionArgs(string id="")=>new(){["id"]=id,["revision"]=editor.RegionRevision(id),["name"]=id==""?"Garden":editor.ReadRegion(id).name,["members"]=new JArray(editor.ReadRegion(id)?.members??Array.Empty<string>())};
        string SavedStructureFixture(params string[] ids) {
            var value=new RoomStructure{id=Guid.NewGuid().ToString("N"),name="Castle",slots=ids.Select((id,i)=>new StructureSlot{slot="piece_"+i,placement=editor.Frame.Placement(id,editor.Find(id).transform)}).ToArray()};
            Assert.IsTrue(editor.SaveStructure(value,0,out var error),error);return value.id;
        }
        JObject PendingStructureArgs(string id) {
            var source=JObject.Parse(JsonUtility.ToJson(editor.ReadStructure(id)));source.Remove("id");source.Remove("version");source["kind"]="definition";
            return new(){["id"]=id,["revision"]=editor.StructureRevision(id),["source"]=source};
        }
        IEnumerator PendingGroupTemporaryDone() {
            float deadline=Time.realtimeSinceStartup+20;
            while(editor.TemporarySavePending){Assert.Less(Time.realtimeSinceStartup,deadline,"Temporary group save did not finish");yield return null;}
            Assert.IsNull(editor.TemporarySaveError);
        }
        IEnumerator PendingGroupReady(CapabilityOperation operation) {
            float deadline=Time.realtimeSinceStartup+20;
            while(operation.State(out _)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,deadline);yield return null;}
            Assert.AreEqual(RuleActionState.Ready,operation.State(out var error),error);
        }
        [UnityTest] public IEnumerator PendingRegionQueueCapturesNamesAndReturnsSavedStableIdentities() {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation first=null,second=null;
            try {
                yield return Prepared(blocker);var args=PendingRegionArgs();var module=new RegionSaveCapability();var context=new CapabilityContext(editor,animations);
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(context,"one",args,out first,out error),error);
                args["name"]="Courtyard";Assert.IsTrue(module.Start(context,"two",args,out second,out error),error);args["name"]="Changed after both requests";
                Assert.IsEmpty(editor.Regions());Assert.IsEmpty(first.Result);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Preparing,second.State(out error),error);Assert.IsEmpty(editor.Regions());
                yield return PlacementOperationDone(first);yield return PlacementOperationDone(second);
                string id=(string)first.Result["id"],other=(string)second.Result["id"];Assert.AreNotEqual(id,other);Assert.AreEqual("Garden",editor.ReadRegion(id).name);Assert.AreEqual("Courtyard",editor.ReadRegion(other).name);
                Assert.AreEqual(2,new RoomStorage(directory).Load(out error).regions.Length,error);int revision=(int)first.Result["revision"];first.Result["revision"]=-1;Assert.AreEqual(revision,(int)first.Result["revision"]);
                editor.Undo();Assert.IsNull(editor.ReadRegion(other));
            }finally{first?.Stop(false);second?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingRegionMoveIsAtomicAndKeepsItsAcceptedObjectRevisionAfterHumanEdits() {
            string from=NewRegion(),to=NewRegion(),target=editor.Identity(block);Assert.IsTrue(AssignRegion(target,from,out var error),error);
            foreach(bool cancel in new[]{true,false}) {
                Assert.IsTrue(editor.BeginRegionBinding(target,editor.ObjectRevision(target),to,editor.RegionRevision(to),out var pending,out error),error);
                using(pending) {
                    yield return Prepared(pending);Assert.AreEqual(from,editor.RegionFor(target));Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);
                    if(cancel){pending.Dispose();yield return Drained(pending);Assert.AreEqual(from,editor.RegionFor(target));Assert.IsEmpty(new RoomStorage(directory).Load(out error).regions.Single(x=>x.id==to).members);continue;}
                    yield return Completed(pending);Assert.AreEqual(to,editor.RegionFor(target));Assert.IsEmpty(editor.ReadRegion(from).members);
                    var disk=new RoomStorage(directory).Load(out error);Assert.IsEmpty(disk.regions.Single(x=>x.id==from).members);Assert.AreEqual(target,disk.regions.Single(x=>x.id==to).members.Single());
                    int accepted=editor.ObjectRevision(target);Assert.IsTrue(editor.PaintObject(target,Color.blue,out error),error);Assert.AreEqual(accepted,pending.AcceptedObjectRevision(target));Assert.AreNotEqual(accepted,editor.ObjectRevision(target));
                    editor.Undo();editor.Undo();Assert.AreEqual(from,editor.RegionFor(target));Assert.IsEmpty(editor.ReadRegion(to).members);
                }
            }
            string home=LayoutObject(Vector3.one);int original=editor.ObjectRevision(home);
            var args=new JObject{["target"]=home,["revision"]=original,["regionId"]="",["regionRevision"]=0};
            Assert.IsTrue(new RegionAssignCapability().Start(new CapabilityContext(editor,animations),"same-home",args,out var operation,out error),error);
            try{yield return PendingGroupReady(operation);Assert.IsTrue(editor.PaintObject(home,Color.red,out error),error);Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(original,(int)operation.Result["revision"]);}finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingRegionRenameKeepsMovingMembersClockAndItsAcceptedRevision() {
            string id=NewRegion(),target=editor.Identity(block);Assert.IsTrue(AssignRegion(target,id,out var error),error);
            var value=editor.ReadRegion(id);value.name="Living garden";
            Assert.IsTrue(editor.BeginRegion(value,id,editor.RegionRevision(id),value.members,out var pending,out error),error);
            using(pending) {
                yield return Prepared(pending);var moved=block.transform.position+Vector3.right*.2f;block.transform.position=moved;editor.RememberPlacement(target);editor.TickWorldTime(.3);double now=editor.WorldSecond;
                yield return Completed(pending);Assert.AreEqual(moved,block.transform.position);Assert.GreaterOrEqual(editor.WorldSecond,now);Assert.IsTrue(editor.HasUnsavedChanges);
                int accepted=editor.RegionRevision(id);var later=editor.ReadRegion(id);later.name="Human rename";Assert.IsTrue(editor.EditRegion(later,id,accepted,later.members,out error),error);
                Assert.AreEqual(accepted,pending.AcceptedRegionRevision(id));Assert.AreNotEqual(accepted,editor.RegionRevision(id));
            }
        }
        [UnityTest] public IEnumerator PendingRegionRefusesChangedMembershipOrCapacityWhileWaiting() {
            foreach(bool fill in new[]{false,true}) {
                string id=NewRegion(),target=editor.Identity(block);if(!fill)Assert.IsTrue(AssignRegion(target,id,out _));
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try {
                    yield return Prepared(blocker);var args=PendingRegionArgs(fill?"":id);args["name"]="Waiting area";
                    Assert.IsTrue(new RegionSaveCapability().Start(new CapabilityContext(editor,animations),"waiting",args,out operation,out error),error);
                    if(fill){while(editor.Regions().Length<RoomRegion.MaximumRegions)NewRegion();}
                    else {string other=LayoutObject(Vector3.one);Assert.IsTrue(AssignRegion(other,id,out error),error);}
                    blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);Assert.IsFalse(editor.Regions().Any(x=>x.name=="Waiting area"));
                }finally{operation?.Stop(false);blocker.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingRegionNoopRemovalAndTemporaryKeepDiscardUseTheSharedJournal() {
            string id=NewRegion();int revision=editor.RegionRevision(id);var context=new CapabilityContext(editor,animations);
            Assert.IsTrue(new RegionSaveCapability().Start(context,"same",PendingRegionArgs(id),out var operation,out var error),error);yield return PlacementOperationDone(operation);Assert.AreEqual(revision,(int)operation.Result["revision"]);
            editor.Undo();Assert.IsNull(editor.ReadRegion(id));editor.Redo();
            Assert.IsTrue(new RegionRemoveCapability().Start(context,"remove",new JObject{["id"]=id,["revision"]=editor.RegionRevision(id)},out operation,out error),error);yield return PlacementOperationDone(operation);Assert.AreEqual(0,(int)operation.Result["revision"]);editor.Undo();
            foreach(bool keep in new[]{false,true}) {
                Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);yield return PendingGroupTemporaryDone();
                Assert.IsTrue(new RegionAssignCapability().Start(context,"assign",(JObject)RegionCall(editor.Identity(block),id)["arguments"],out operation,out error),error);yield return PlacementOperationDone(operation);Assert.IsTrue((bool)operation.Result["temporary"]);
                Assert.IsEmpty(new RoomStorage(directory).Load(out error).regions.Single(x=>x.id==id).members,error);
                if(keep)Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);else Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                yield return PendingGroupTemporaryDone();Assert.AreEqual(keep,new RoomStorage(directory).Load(out error).regions.Single(x=>x.id==id).members.Contains(editor.Identity(block)),error);
            }
        }
        [UnityTest] public IEnumerator PendingStructureCaptureKeepsRequestedBaselineWithoutMovingItsLivePieces() {
            string target=editor.Identity(block);var original=editor.Frame.Placement(target,block.transform);var args=(JObject)StructureSaveCall(target)["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(new StructureSaveCapability().Start(new CapabilityContext(editor,animations),"capture",args,out operation,out error),error);
                args["source"]["name"]="Changed request";args["source"]["members"][0]["target"]=new string('e',32);
                var moved=block.transform.position+Vector3.right*.2f;block.transform.position=moved;editor.RememberPlacement(target);
                blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                string id=(string)operation.Result["structureId"];var value=editor.ReadStructure(id);Assert.AreEqual("Castle",value.name);Assert.AreEqual(target,value.slots.Single().placement.target);Assert.AreEqual(original.position,value.slots.Single().placement.position);Assert.AreEqual(moved,block.transform.position);
                Assert.AreEqual(id,new RoomStorage(directory).Load(out error).structures.Single().id,error);editor.Undo();Assert.IsNull(editor.ReadStructure(id));Assert.AreEqual(moved,block.transform.position);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingStructureNoopExactReceiptAndForgetPreservePiecesAndUndo() {
            string target=editor.Identity(block),id=SavedStructureFixture(target);int revision=editor.StructureRevision(id);var context=new CapabilityContext(editor,animations);
            Assert.IsTrue(new StructureSaveCapability().Start(context,"same",PendingStructureArgs(id),out var operation,out var error),error);yield return PlacementOperationDone(operation);Assert.AreEqual(revision,(int)operation.Result["revision"]);
            editor.Undo();Assert.IsNull(editor.ReadStructure(id));editor.Redo();
            var args=PendingStructureArgs(id);args["source"]["name"]="Edited castle";Assert.IsTrue(new StructureSaveCapability().Start(context,"edit",args,out operation,out error),error);
            try {
                yield return PendingGroupReady(operation);int accepted=editor.StructureRevision(id);var later=editor.ReadStructure(id);later.name="Later human edit";Assert.IsTrue(editor.SaveStructure(later,accepted,out error),error);
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(accepted,(int)operation.Result["revision"]);Assert.AreNotEqual(accepted,editor.StructureRevision(id));
            }finally{operation.Stop(false);}
            Assert.IsTrue(new StructureForgetCapability().Start(context,"forget",new JObject{["id"]=id,["revision"]=editor.StructureRevision(id)},out operation,out error),error);yield return PlacementOperationDone(operation);
            Assert.IsEmpty(operation.Result);Assert.IsNull(editor.ReadStructure(id));Assert.IsNotNull(editor.Find(target));editor.Undo();Assert.AreEqual("Later human edit",editor.ReadStructure(id).name);
        }
        [UnityTest] public IEnumerator PendingStructureResetRefusesLateDefinitionAndMissingMembersBeforeMovingAnyPiece() {
            foreach(bool remove in new[]{false,true}) {
                string target=LayoutObject(Vector3.one),other=LayoutObject(Vector3.one*2),id=SavedStructureFixture(target,other);editor.Find(target).transform.localPosition+=Vector3.right;var before=editor.Find(target).transform.position;
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try {
                    yield return Prepared(blocker);var args=(JObject)StructureResetCall(id,editor.StructureRevision(id),target,other)["arguments"];
                    Assert.IsTrue(new StructureResetCapability().Start(new CapabilityContext(editor,animations),"reset",args,out operation,out error),error);
                    if(remove)Assert.IsTrue(editor.DeleteObject(other,out error),error);else {var changed=editor.ReadStructure(id);changed.name="Changed baseline";Assert.IsTrue(editor.SaveStructure(changed,editor.StructureRevision(id),out error),error);}
                    blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.AreEqual(before,editor.Find(target).transform.position);Assert.IsEmpty(operation.Result);
                }finally{operation?.Stop(false);blocker.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingLayoutQueueCapturesAllPlacementsAndUndoRestoresActualPreEditPoses() {
            string a=LayoutObject(Vector3.one),b=LayoutObject(Vector3.one*2);editor.Find(a).transform.localPosition+=Vector3.up*.4f;editor.Find(b).transform.localPosition+=Vector3.right*.4f;
            var beforeA=editor.Find(a).transform.localPosition;var beforeB=editor.Find(b).transform.localPosition;var layout=Layout(a,b);var args=JObject.Parse(JsonUtility.ToJson(layout));
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(new LayoutCapability().Start(new CapabilityContext(editor,animations),"layout",args,out operation,out error),error);
                args["placements"][0]["position"]["x"]=9;args["placements"][1]["target"]=new string('e',32);
                Assert.IsFalse(editor.Find(a).GetComponent<RigidRoomItem>().AnimationOwned,"A waiting action must not hold poses");
                blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                Assert.AreEqual(2,(int)operation.Result["count"]);var disk=new RoomStorage(directory).Load(out error);
                foreach(var desired in layout.placements){Assert.AreEqual(desired.position,editor.Find(desired.target).transform.localPosition);Assert.AreEqual(desired.position,disk.objects.Single(x=>x.id==desired.target).position,error);}
                editor.Undo();Assert.AreEqual(beforeA,editor.Find(a).transform.localPosition);Assert.AreEqual(beforeB,editor.Find(b).transform.localPosition);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingLayoutCancellationReleasesOnlyItsOwnBodiesAndResumesTheirMomentum() {
            string a=LayoutObject(new Vector3(2,2,0)),b=LayoutObject(new Vector3(3,2,0));
            foreach(string id in new[]{a,b,editor.Identity(block)})Assert.IsTrue(editor.SetItemPhysics(id,new ObjectPhysicsSettings{mode="solid",shape="box",mass=1}),editor.Status);
            physics.SetSurfaces(true,"Synthetic layout room");physics.StartPhysics();Assert.IsTrue(physics.Running);
            foreach(string id in new[]{a,b,editor.Identity(block)})Assert.IsFalse(editor.Find(id).GetComponent<Rigidbody>().isKinematic);
            var first=editor.Find(a);var second=editor.Find(b);first.GetComponent<Rigidbody>().linearVelocity=Vector3.right;second.GetComponent<Rigidbody>().linearVelocity=Vector3.forward*2;
            var beforeA=first.transform.position;var beforeB=second.transform.position;var other=block.transform.position;var layout=Layout(a,b);foreach(var point in layout.placements)point.position+=Vector3.right*5;
            var revisions=layout.placements.ToDictionary(x=>x.target,x=>editor.ObjectRevision(x.target));Assert.IsTrue(editor.BeginLayout(layout,revisions,null,out var pending,out var error),error);
            using(pending) {
                Assert.IsTrue(first.GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsTrue(second.GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);
                yield return Prepared(pending);yield return new WaitForFixedUpdate();Assert.AreEqual(beforeA,first.transform.position);Assert.AreEqual(beforeB,second.transform.position);Assert.AreNotEqual(other,block.transform.position,"Unrelated physics must keep running");
                pending.Dispose();
                Assert.IsFalse(first.GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(second.GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsTrue(physics.Running);
                Assert.AreEqual(1,first.GetComponent<Rigidbody>().linearVelocity.x,.001);Assert.AreEqual(2,second.GetComponent<Rigidbody>().linearVelocity.z,.001);Assert.IsFalse(pending.Accepted);
                yield return Drained(pending);
            }
        }
        [UnityTest] public IEnumerator PendingLayoutLostNativeMemberRefusesEveryPlacementAndReleasesAllHolds() {
            string a=LayoutObject(Vector3.one),b=LayoutObject(Vector3.one*2);var first=editor.Find(a);var second=editor.Find(b);var before=first.transform.position;var layout=Layout(a,b);
            Assert.IsTrue(editor.BeginLayout(layout,layout.placements.ToDictionary(x=>x.target,x=>editor.ObjectRevision(x.target)),null,out var pending,out var error),error);
            try {using(pending) {
                yield return Prepared(pending);second.gameObject.SetActive(false);Assert.IsFalse(pending.Poll(out _,out error));yield return Drained(pending);Assert.IsFalse(pending.Accepted);
                Assert.AreEqual(before,first.transform.position);Assert.IsFalse(first.GetComponent<RigidRoomItem>().AnimationOwned);Assert.AreEqual(Vector3.one,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==a).position,error);
            }}finally{second.gameObject.SetActive(true);}
        }
        [UnityTest] public IEnumerator PendingLayoutAcceptedStopDrainsOneSaveAndUndoRestoresEveryMember() {
            string a=LayoutObject(Vector3.one),b=LayoutObject(Vector3.one*2);var layout=Layout(a,b);var revisions=layout.placements.ToDictionary(x=>x.target,x=>editor.ObjectRevision(x.target));using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;
            Assert.IsTrue(PersistedEditOperation.Start(editor,null,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginLayout(layout,revisions,null,out edit,out issue,pause.Check);pending=edit;return ok;},
                out var operation,out var error,validate:(out string issue)=>editor.CanApplyCapturedLayout(layout,revisions,null,null,out issue)),error);
            try {
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);Assert.IsFalse(operation.Complete(out _));
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);var disk=new RoomStorage(directory).Load(out error);
            foreach(var desired in layout.placements)Assert.AreEqual(desired.position,disk.objects.Single(x=>x.id==desired.target).position,error);
            editor.Undo();Assert.AreEqual(Vector3.one,editor.Find(a).transform.localPosition);Assert.AreEqual(Vector3.one*2,editor.Find(b).transform.localPosition);
        }
        [UnityTest] public IEnumerator PendingConstructionHandleShowsSharedSaveAndBlocksAnotherGripUntilReceiptCompletes() {
            string a=LayoutObject(Vector3.one),b=LayoutObject(Vector3.one+Vector3.right*.3f);var tool=Mover(a,b);var hand=Hand(1,tool.Handle.transform.position);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try {
                yield return Prepared(blocker);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);MoveHandle(tool,Vector3.up*.3f);string runId=runtime.Scheduler.Receipts.NextId;
                hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,tool.Handle.Grab);yield return null;
                Assert.AreEqual("preparing",(string)runtime.Scheduler.Invocation(runId)["phase"]);Assert.IsFalse(tool.Process((IXRSelectInteractor)hand,tool.Handle.Grab));CaptureConstructionHandle(tool,"construction-saving.png");
                blocker.Dispose();yield return Drained(blocker);yield return SpatialSettingsCompletion.WaitInvocation(runtime.Scheduler,runId);yield return null;
                Assert.IsEmpty(tool.Error);Assert.IsTrue(tool.Process((IXRSelectInteractor)hand,tool.Handle.Grab));Assert.AreEqual(1.3f,editor.Read(a).position.y,.001f);editor.Undo();Assert.AreEqual(Vector3.one,editor.Find(a).transform.localPosition);
            }finally{blocker.Dispose();}
        }
    }
}
