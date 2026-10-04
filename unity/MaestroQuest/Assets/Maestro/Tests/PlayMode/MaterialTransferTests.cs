// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.Collections;using System.IO;using System.Linq;
using Maestro.Quest.Creation;using Maestro.Quest.Interaction;using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
 JObject MaterialEndpoint(string id,string kind){var result=kind=="field"?SurfaceEndpoint(id):new JObject{["target"]=id,["revision"]=editor.ObjectRevision(id)};result["kind"]=kind;return result;}
 JObject MaterialTransferCall(string a,string ak,string b,string bk,double amount=.2)=>new(){["id"]="object.material.transfer",["version"]=1,["arguments"]=new JObject{["source"]=MaterialEndpoint(a,ak),["destination"]=MaterialEndpoint(b,bk),["amountLitres"]=amount}};
 (RoomAgentExecutor ex,string field,string store) MaterialTransferSetup(){var ex=new RoomAgentExecutor(editor);string field=Snow(ex);Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Carried material",new Vector3(6,2,4),1,Color.white,out var store,out var error),Is.True,error);Assert.That(editor.EditMaterialStore(store,editor.ObjectRevision(store),new RoomMaterialStore(),out error),Is.True,error);return(ex,field,store);}
 [UnityTest] public IEnumerator MaterialTransferPersistsSurfaceAndStoreWithOneUndoAndNoReceiptReplay(){
  var(ex,field,store)=MaterialTransferSetup();var before=editor.Read(field).heightFields[0];var pose=editor.Find(store).transform.position;
  var request=ContainerRequest(MaterialTransferCall(field,"field",store,"store"));Assert.That(ex.Execute(request,out var error,out _),Is.True,error);var output=ex.Executions.Observe()["selected"]["output"];double amount=editor.Read(store).materialStores[0].amountLitres;var after=editor.Read(field).heightFields[0];
  Assert.That(amount,Is.GreaterThan(0));Assert.That(before.VolumeLitres-after.VolumeLitres,Is.EqualTo((double)output["removedLitres"]));Assert.That(amount,Is.EqualTo((double)output["addedLitres"]));Assert.That(editor.Find(store).transform.position,Is.EqualTo(pose));
  Assert.That(ex.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.EqualTo(amount));var saved=new RoomStorage(directory).Load(out error);// JsonUtility storage round-trips doubles within floating-point precision; live receipts remain exact.
  Assert.That(saved.objects.Single(o=>o.id==store).materialStores[0].amountLitres,Is.EqualTo(amount).Within(1e-12));Assert.That(saved.objects.Single(o=>o.id==field).heightFields[0].heights,Is.EqualTo(after.heights));
  Physics.SyncTransforms();var collider=editor.Find(field).GetComponent<HeightFieldView>().Collision;Assert.That(collider.Raycast(new Ray(editor.Find(field).transform.TransformPoint(Vector3.up),Vector3.down),out var hit,3),Is.True);Assert.That(editor.Find(field).transform.InverseTransformPoint(hit.point).y,Is.EqualTo(after.HeightAt(Vector2.zero)).Within(.00001));
  editor.Undo();Assert.That(editor.Read(field).heightFields[0].heights,Is.EqualTo(before.heights));Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.Zero);editor.Redo();Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.EqualTo(amount));
  ContainerRun(ex,MaterialTransferCall(store,"store",field,"field",.1));Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.LessThan(amount));yield return null;
 }
 [UnityTest] public IEnumerator MaterialTransferStaleOwnerMissingComponentAndSaveFailureLeaveBothBalancesIntact(){
  var(ex,field,store)=MaterialTransferSetup();var stale=MaterialTransferCall(field,"field",store,"store");Assert.That(editor.EditMaterialStore(store,editor.ObjectRevision(store),new RoomMaterialStore{capacityLitres=2},out var error),Is.True,error);Assert.That(ex.Execute(ContainerRequest(stale),out _,out _),Is.False);
  var before=editor.Read(field).heightFields[0].heights;Assert.That(editor.Ownership.TryAcquire("human","Your edit",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(store,"wholeTarget")},null,out var lease,out error),Is.True,error);try{Assert.That(ex.Execute(ContainerRequest(MaterialTransferCall(field,"field",store,"store")),out _,out _),Is.False);}finally{lease.Dispose();}
  string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);try{Assert.That(ex.Execute(ContainerRequest(MaterialTransferCall(field,"field",store,"store")),out _,out _),Is.False);}finally{Directory.Delete(pending);}
  Assert.That(editor.Read(field).heightFields[0].heights,Is.EqualTo(before));Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.Zero);
  Assert.That(editor.EditMaterialStore(store,editor.ObjectRevision(store),null,out error),Is.True,error);Assert.That(ex.Execute(ContainerRequest(MaterialTransferCall(field,"field",store,"store")),out _,out _),Is.False);Assert.That(editor.Read(field).heightFields[0].heights,Is.EqualTo(before));yield return null;
 }
 [UnityTest] public IEnumerator MaterialTransferTemporaryDiscardRestoresBothBalancesAndProgramsUseTheSameAction(){
  var(ex,field,store)=MaterialTransferSetup();var before=editor.Read(field).heightFields[0].heights;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
  var program=BehaviourProgram.FromInvocation(MaterialTransferCall(field,"field",store,"store"));var sequence=new Maestro.Quest.Rules.RuleSequence{id=Guid.NewGuid().ToString("N"),name="Scoop material",program=program};runtime.Scheduler.Configure(new Maestro.Quest.Rules.RuleDocument{sequences=new[]{sequence}});Assert.That(runtime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);yield return null;
  Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.GreaterThan(0));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(field).heightFields[0].heights,Is.EqualTo(before));Assert.That(editor.Read(store).materialStores[0].amountLitres,Is.Zero);
 }
}}
