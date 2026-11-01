// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
        [UnityTest] public IEnumerator PendingMaterialStoreCapturesDefinitionAndCancellationThenRemovalKeepsUndo() {
            var(ex,_,store)=MaterialTransferSetup();int revision=editor.ObjectRevision(store);
            var definition=editor.Read(store).materialStores[0];definition.amountLitres=.3;
            Assert.IsTrue(editor.BeginMaterialStore(store,revision,definition,out var cancelled,out var error),error);
            yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(revision,editor.ObjectRevision(store));Assert.AreEqual(0,MaterialAmount(store));
            Assert.IsTrue(editor.BeginMaterialStore(store,revision,definition,out var pending,out error),error);
            using(pending){definition.amountLitres=.8;yield return Completed(pending);}
            Assert.AreEqual(.3,MaterialAmount(store));
            Assert.AreEqual(.3,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==store).materialStores[0].amountLitres,.000000001,error);
            var item=editor.Find(store);item.transform.position+=Vector3.right*.2f;editor.RememberPlacement(store);var pose=item.transform.position;
            editor.Undo();Assert.AreEqual(0,MaterialAmount(store));Assert.AreEqual(pose,item.transform.position);
            var remove=new JObject{["id"]="object.material.edit",["version"]=1,["arguments"]=new JObject{["operation"]="remove",["target"]=store,["revision"]=editor.ObjectRevision(store)}};
            yield return ContainerAction(ex,remove);Assert.IsEmpty(editor.Read(store).materialStores);
            editor.Undo();Assert.AreEqual(0,MaterialAmount(store));Assert.AreEqual(pose,item.transform.position);
        }
        [UnityTest] public IEnumerator PendingMaterialPackCapturesQueuedInputsAndReturnsOneDurableIdentity() {
            string field=Snow(new RoomAgentExecutor(editor));var before=editor.Read(field).heightFields[0];int count=editor.Snapshot().objects.Length;
            var args=(JObject)PackCall(field)["arguments"];Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.material.pack").Module;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"pack",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(operation.Result);
                Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
                args["name"]="Changed after enqueue";args["amountLitres"]=.1;args["source"]["centre"]["x"]=99;
                blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                var receipt=operation.Result;string id=(string)receipt["objectId"];double amount=(double)receipt["amountLitres"];
                Assert.AreEqual("Packed snowball",editor.Read(id).name);Assert.AreEqual(.5,amount,.000001);
                Assert.AreEqual(before.VolumeLitres-editor.Read(field).heightFields[0].VolumeLitres,amount,.000000001);
                Assert.AreEqual(amount,MaterialAmount(id));Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
                Assert.IsTrue(operation.Complete(out error),error);receipt["objectId"]="changed";Assert.AreEqual(id,(string)operation.Result["objectId"]);
                Assert.AreEqual(amount,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).materialStores[0].amountLitres,.000000001,error);
                editor.Undo();Assert.IsNull(editor.Read(id));Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
                editor.Redo();Assert.AreEqual(amount,MaterialAmount(id));Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingMaterialPackCancellationAndAcceptedFailureNeverLeaveHalfATransaction() {
            string field=Snow(new RoomAgentExecutor(editor));var args=(JObject)PackCall(field)["arguments"];var before=editor.Read(field).heightFields[0];int count=editor.Snapshot().objects.Length;
            Assert.IsTrue(editor.BeginPackMaterial(args,out var cancelledId,out _,out var cancelled,out var error),error);
            yield return Prepared(cancelled);Assert.IsNull(editor.Read(cancelledId));cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
            byte[] original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            string id=null;double amount=0;RoomEditor.PendingPersistedEdit failed=null;
            Assert.IsTrue(PersistedEditOperation.StartCreation(editor,
                (out RoomEditor.PendingPersistedEdit value,out string issue)=>{bool started=editor.BeginPackMaterial(args,out id,out amount,out value,out issue,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected material pack publication failure");}});failed=value;return started;},
                ()=>new JObject{["objectId"]=id,["amountLitres"]=amount},out var operation,out error,source:field,
                validate:(out string issue)=>editor.CanPackMaterial(args,out issue)),error);
            try {
                yield return Prepared(failed);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(failed.Accepted);Assert.IsEmpty(operation.Result);
                Assert.AreEqual(count+1,editor.Snapshot().objects.Length);Assert.AreEqual(amount,MaterialAmount(id));
                Assert.AreEqual(amount,before.VolumeLitres-editor.Read(field).heightFields[0].VolumeLitres,.000000001);
                pause.Release();yield return Rejected(failed);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);StringAssert.Contains(id,operation.InterruptionStatus);
                Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.IsFalse(failed.Published);
                Assert.IsFalse(editor.BeginMaterialStore(id,editor.ObjectRevision(id),new RoomMaterialStore(),out _,out _));
            } finally {pause.Release();operation.Stop(false);}
            yield return Drained(failed);
        }
        [UnityTest] public IEnumerator PendingMaterialTransferCapturesQueuedEndpointsAndImmutableMeasuredResult() {
            var(_,field,store)=MaterialTransferSetup();var before=editor.Read(field).heightFields[0];var args=(JObject)MaterialTransferCall(field,"field",store,"store")["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.material.transfer").Module;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"transfer",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(operation.Result);Assert.AreEqual(0,MaterialAmount(store));
                args["amountLitres"]=.1;args["destination"]["target"]=field;blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(operation);
                var result=operation.Result;Assert.AreEqual(.2,(double)result["addedLitres"],.000001);Assert.AreEqual((double)result["addedLitres"],MaterialAmount(store));
                Assert.AreEqual((double)result["removedLitres"],before.VolumeLitres-editor.Read(field).heightFields[0].VolumeLitres,.000000001);
                result["addedLitres"]=99;Assert.AreEqual(.2,(double)operation.Result["addedLitres"],.000001);
                Assert.AreEqual(MaterialAmount(store),new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==store).materialStores[0].amountLitres,.000000001,error);
            } finally {operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingMaterialTransferRejectsEitherChangedParticipantWhileQueued() {
            var(_,field,store)=MaterialTransferSetup();
            foreach(string target in new[]{field,store}){
                var args=(JObject)MaterialTransferCall(field,"field",store,"store")["arguments"];var before=editor.Read(field).heightFields[0].heights;
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try {
                    yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.material.transfer").Module.Start(new CapabilityContext(editor,animations),"waiting",args,out operation,out error),error);
                    Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                    Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);
                    Assert.AreEqual(before,editor.Read(field).heightFields[0].heights);Assert.AreEqual(0,MaterialAmount(store));
                } finally {operation?.Stop(false);blocker.Dispose();}
                editor.Undo();
            }
        }
        [UnityTest] public IEnumerator PendingMaterialTransferAcceptedStopDrainsAndUndoPreservesCarrierMotion() {
            var(_,field,store)=MaterialTransferSetup();var before=editor.Read(field).heightFields[0];var body=editor.Find(store).GetComponent<Rigidbody>();
            physics.SetSurfaces(true,"Synthetic material continuity setup");physics.StartPhysics();Assert.IsFalse(body.isKinematic);
            using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginMaterialTransfer((JObject)MaterialTransferCall(field,"field",store,"store")["arguments"],out var moved,out var pending,out var error,pause.Check),error);
            try {
                yield return Prepared(pending);Assert.AreEqual(0,MaterialAmount(store));body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                Assert.IsFalse(body.isKinematic);Assert.IsTrue(body.useGravity);Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
                Assert.Greater(Vector3.Distance(position,body.position),.001f);Assert.IsTrue(pause.Entered.IsSet);Assert.AreNotEqual(System.Threading.Thread.CurrentThread.ManagedThreadId,pause.Worker);
                Assert.AreEqual(moved.AddedLitres,MaterialAmount(store));Assert.IsFalse(pending.Complete(out _));pending.Dispose();StringAssert.Contains("Saving is still pending",pending.InterruptionStatus);
            } finally {pause.Release();pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            Assert.AreEqual(moved.AddedLitres,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==store).materialStores[0].amountLitres,.000000001,error);
            var now=body.position;var velocity=body.linearVelocity;var spin=body.angularVelocity;editor.Undo();Assert.AreEqual(now,body.position);Assert.AreEqual(velocity,body.linearVelocity);Assert.AreEqual(spin,body.angularVelocity);Assert.IsFalse(body.isKinematic);Assert.AreEqual(0,MaterialAmount(store));Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
        }
        [UnityTest] public IEnumerator PendingMaterialPackingFromFilledTerrainKeepsWaterAndRecomputesCapacity() {
            var(ex,field)=TerrainReservoir();var before=editor.Read(field);double water=before.containers[0].amountMl;
            yield return ContainerAction(ex,PackCall(field));var output=ex.Executions.Observe()["selected"]["output"];string ball=(string)output["objectId"];double packed=(double)output["amountLitres"];
            var after=editor.Read(field);Assert.AreEqual(water,after.containers[0].amountMl);Assert.AreEqual(before.containers[0].capacityMl+packed*1000,after.containers[0].capacityMl,.000001);
            Assert.AreEqual(after.heightFields[0].heights,editor.Find(field).GetComponent<HeightFieldView>().Accepted.heights);Assert.AreEqual(packed,MaterialAmount(ball));
            Assert.IsTrue(editor.Snapshot().Validate(out var error),error);var saved=new RoomStorage(directory).Load(out error);Assert.IsNotNull(saved,error);
            Assert.AreEqual(water,saved.objects.Single(x=>x.id==field).containers[0].amountMl);Assert.AreEqual(after.containers[0].capacityMl,saved.objects.Single(x=>x.id==field).containers[0].capacityMl,.000001);
            yield return ContainerAction(ex,MaterialTransferCall(ball,"store",field,"field",.1));Assert.AreEqual(water,editor.Read(field).containers[0].amountMl);Assert.Less(editor.Read(field).containers[0].capacityMl,after.containers[0].capacityMl);
            editor.Undo();Assert.AreEqual(after.containers[0].capacityMl,editor.Read(field).containers[0].capacityMl);Assert.AreEqual(packed,MaterialAmount(ball));
            editor.Undo();Assert.IsNull(editor.Read(ball));Assert.AreEqual(before.heightFields[0].heights,editor.Read(field).heightFields[0].heights);Assert.AreEqual(before.containers[0].capacityMl,editor.Read(field).containers[0].capacityMl);Assert.AreEqual(water,editor.Read(field).containers[0].amountMl);
            editor.Redo();Assert.AreEqual(packed,MaterialAmount(ball));Assert.AreEqual(after.containers[0].capacityMl,editor.Read(field).containers[0].capacityMl);Assert.AreEqual(water,editor.Read(field).containers[0].amountMl);
        }
        [UnityTest] public IEnumerator PendingMaterialPackedTerrainRoundTripsAfterPartialReturn() {
            var(ex,field)=TerrainReservoir();var bed=new RoomHeightField{cells=16,width=1,depth=1,maxHeight=.25f,heights=Enumerable.Repeat(.08f,289).ToArray()};
            Assert.IsTrue(editor.EditHeightField(field,editor.ObjectRevision(field),bed,out var error),error);
            Assert.IsTrue(editor.EditContainer(field,editor.ObjectRevision(field),new RoomContainer{version=3,height=.3f,amountMl=50000},out error),error);
            var pack=PackCall(field);pack["arguments"]["source"]["radius"]=.2;yield return ContainerAction(ex,pack);string ball=(string)ex.Executions.Observe()["selected"]["output"]["objectId"];
            var store=editor.Read(ball).materialStores[0];store.capacityLitres=1;Assert.IsTrue(editor.EditMaterialStore(ball,editor.ObjectRevision(ball),store,out error),error);
            var call=MaterialTransferCall(ball,"store",field,"field",.1);call["arguments"]["destination"]["radius"]=.2;
            Assert.IsTrue(editor.PrepareMaterialTransfer((JObject)call["arguments"],out var values,out _,out error),error);
            var candidate=editor.Snapshot();candidate.objects=candidate.objects.Select(x=>values.FirstOrDefault(v=>v.id==x.id)??x).ToArray();
            var decoded=JsonUtility.FromJson<RoomDocument>(JsonUtility.ToJson(candidate));RoomStorage.Normalize(decoded);
            var persistedField=decoded.objects.Single(x=>x.id==field);RoomTerrainReservoir.Derive(persistedField,persistedField.containers[0],out _,out double derived,out _);
            Assert.IsTrue(decoded.Validate(out error),error+" | capacity before/after: "+candidate.objects.Single(x=>x.id==field).containers[0].capacityMl.ToString("R")+" / "+decoded.objects.Single(x=>x.id==field).containers[0].capacityMl.ToString("R")+" derived: "+derived.ToString("R"));
            yield return ContainerAction(ex,call);Assert.AreEqual(50000,editor.Read(field).containers[0].amountMl);
        }
        [UnityTest] public IEnumerator PendingMaterialTransferCancellationAndAcceptedFailurePreserveOriginalFiles() {
            var(_,field,store)=MaterialTransferSetup();var args=(JObject)MaterialTransferCall(field,"field",store,"store")["arguments"];var before=editor.Read(field).heightFields[0].heights;
            Assert.IsTrue(editor.BeginMaterialTransfer(args,out _,out var cancelled,out var error),error);yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
            Assert.AreEqual(before,editor.Read(field).heightFields[0].heights);Assert.AreEqual(0,MaterialAmount(store));
            byte[] original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginMaterialTransfer(args,out var moved,out var failed,out error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected material transfer publication failure");}}),error);
            try {
                yield return Prepared(failed);Assert.IsTrue(failed.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(failed.Accepted);
                pause.Release();yield return Rejected(failed);Assert.AreEqual(moved.AddedLitres,MaterialAmount(store));Assert.AreNotEqual(before,editor.Read(field).heightFields[0].heights);
                Assert.AreEqual(editor.Read(field).heightFields[0].heights,editor.Find(field).GetComponent<HeightFieldView>().Accepted.heights);
                Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.IsFalse(failed.Published);
                Assert.IsFalse(editor.BeginMaterialStore(store,editor.ObjectRevision(store),new RoomMaterialStore(),out _,out _));
            } finally {pause.Release();failed.Dispose();}
            yield return Drained(failed);
        }
    }
}
