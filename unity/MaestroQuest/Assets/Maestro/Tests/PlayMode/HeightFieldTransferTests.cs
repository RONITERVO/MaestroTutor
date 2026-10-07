// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
 JObject SurfaceTransfer(string a,string b,double amount=1)=>new(){["id"]="object.field.transfer",["version"]=1,["arguments"]=new JObject{["source"]=SurfaceEndpoint(a),["destination"]=SurfaceEndpoint(b),["amountLitres"]=amount}};
 JObject SurfaceEndpoint(string id)=>new(){["target"]=id,["revision"]=editor.ObjectRevision(id),["centre"]=new JObject{["x"]=0,["z"]=0},["radius"]=.3};
 (RoomAgentExecutor ex,string a,string b) TransferSurfaces(){var ex=new RoomAgentExecutor(editor);string a=Snow(ex);Assert.That(editor.CopyObject(a,editor.ObjectRevision(a),"Snow receiver",new Vector3(6,2,4),out var b,out var error),Is.True,error);return(ex,a,b);}
 [UnityTest] public IEnumerator FieldTransferPersistsBothSurfacesWithOneUndoAndExactReceiptReplay(){
  var(ex,a,b)=TransferSurfaces();var beforeA=editor.Read(a).heightFields[0];var beforeB=editor.Read(b).heightFields[0];double total=beforeA.VolumeLitres+beforeB.VolumeLitres;
  // Different display scales/rotations do not silently redefine local litre units.
  var acceptedPose=editor.Read(b);editor.Find(b).transform.localScale=new Vector3(2,1,.5f);editor.Find(b).transform.localRotation=Quaternion.Euler(0,35,0);
  var request=ContainerRequest(SurfaceTransfer(a,b));Assert.That(ex.Execute(request,out var error,out _),Is.True,error);var receipt=ex.Executions.Observe();var output=(JObject)receipt["selected"]["output"];
  var afterA=editor.Read(a).heightFields[0];var afterB=editor.Read(b).heightFields[0];double rounding=(double)output["roundingLitres"];
  Assert.That(editor.Read(b).scale,Is.EqualTo(acceptedPose.scale),"A content edit cannot flatten an unrepresentable display scale into a saved pose");
  Assert.That(editor.Find(b).transform.localScale,Is.EqualTo(new Vector3(2,1,.5f)),"The content edit must leave the visual transform alone");
  Assert.That(total-afterA.VolumeLitres-afterB.VolumeLitres,Is.EqualTo(-rounding).Within(.00000001));Assert.That(afterA.heights[144],Is.LessThan(beforeA.heights[144]));Assert.That(afterB.heights[144],Is.GreaterThan(beforeB.heights[144]));
  Assert.That(ex.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(afterA.heights));
  var loaded=new RoomStorage(directory).Load(out _);Assert.That(loaded.objects.Single(o=>o.id==a).heightFields[0].heights,Is.EqualTo(afterA.heights));Assert.That(loaded.objects.Single(o=>o.id==b).heightFields[0].heights,Is.EqualTo(afterB.heights));
  Physics.SyncTransforms();foreach(string id in new[]{a,b}){var item=editor.Find(id);var collider=item.GetComponent<HeightFieldView>().Collision;Assert.That(collider.Raycast(new Ray(item.transform.TransformPoint(Vector3.up),item.transform.TransformDirection(Vector3.down)),out var hit,3),Is.True);Assert.That(item.transform.InverseTransformPoint(hit.point).y,Is.EqualTo(editor.Read(id).heightFields[0].HeightAt(Vector2.zero)).Within(.00001));}
  editor.Undo();Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(beforeA.heights));Assert.That(editor.Read(b).heightFields[0].heights,Is.EqualTo(beforeB.heights));editor.Redo();Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(afterA.heights));Assert.That(editor.Read(b).heightFields[0].heights,Is.EqualTo(afterB.heights));yield return null;
 }
 [UnityTest] public IEnumerator FieldTransferRefusesStaleTargetsHumanOwnershipAndSaveFailureWithoutPartialEffects(){
  var(ex,a,b)=TransferSurfaces();var stale=SurfaceTransfer(a,b);ContainerRun(ex,FieldCall(b));var beforeA=editor.Read(a).heightFields[0].heights;var beforeB=editor.Read(b).heightFields[0].heights;
  Assert.That(ex.Execute(ContainerRequest(stale),out _,out _),Is.False);
  Assert.That(editor.Ownership.TryAcquire("human","Your edit",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(b,"wholeTarget")},null,out var lease,out var error),Is.True,error);
  try{Assert.That(ex.Execute(ContainerRequest(SurfaceTransfer(a,b)),out _,out _),Is.False);}finally{lease.Dispose();}
  string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);try{Assert.That(ex.Execute(ContainerRequest(SurfaceTransfer(a,b)),out error,out _),Is.False);}finally{Directory.Delete(pending);}
  yield return null;Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(beforeA));Assert.That(editor.Read(b).heightFields[0].heights,Is.EqualTo(beforeB));
 }
 [UnityTest] public IEnumerator FieldTransferTemporaryDiscardAndMissingOrChangedMaterialRemainAtomic(){
  var(ex,a,b)=TransferSurfaces();var beforeA=editor.Read(a).heightFields[0].heights;var beforeB=editor.Read(b).heightFields[0].heights;
  Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;ContainerRun(ex,SurfaceTransfer(a,b));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(beforeA));Assert.That(editor.Read(b).heightFields[0].heights,Is.EqualTo(beforeB));
  var changed=editor.Read(b).heightFields[0].Copy();changed.material="Sand";Assert.That(editor.EditHeightField(b,editor.ObjectRevision(b),changed,out error),Is.True,error);Assert.That(ex.Execute(ContainerRequest(SurfaceTransfer(a,b)),out _,out _),Is.False);Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(beforeA));
  Assert.That(editor.EditHeightField(b,editor.ObjectRevision(b),null,out error),Is.True,error);Assert.That(ex.Execute(ContainerRequest(SurfaceTransfer(a,b)),out _,out _),Is.False);Assert.That(editor.Read(a).heightFields[0].heights,Is.EqualTo(beforeA));yield return null;
 }
}}
