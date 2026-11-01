// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  RoomItem AuthoringPointerOwner(){
   var go=new GameObject("Physical authoring pointer targets");go.transform.SetParent(root.transform,false);
   var item=go.AddComponent<RoomItem>();item.Configure(Array.Empty<Collider>());return item;
  }
  static void AuthoringPointerRegister(RoomItem item,Collider collider){
   collider.transform.SetParent(item.transform,true);Assert.AreEqual(0,collider.gameObject.layer);
   Assert.IsTrue(item.RegisterPointerSurface(collider));Assert.IsTrue(RoomItem.IsPointerOnly(collider));
   Assert.That(item.Grab.colliders,Does.Contain(collider));
  }
  BoxCollider[] PackingPointerTargets(HeightFieldView view,int count){
   var owner=AuthoringPointerOwner();var pointers=new BoxCollider[count];
   for(int i=0;i<count;i++){
    var go=new GameObject("Packing pointer "+i);go.transform.SetPositionAndRotation(SculptContact(view)+view.Surface.up*.07f,view.Surface.rotation);
    var collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one*.09f;AuthoringPointerRegister(owner,collider);pointers[i]=collider;
   }
   Physics.SyncTransforms();return pointers;
  }
  [UnityTest]public IEnumerator AuthoringPointerDrawingKeepsPhysicalInkAndPlainSolidOcclusion(){
   var patch=SurfaceOcclusionStage();var owner=AuthoringPointerOwner();var pointer=SurfaceObstacle(patch);AuthoringPointerRegister(owner,pointer);
   var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();
   pencil.Begin(0,SurfaceRay(patch,-.03f));pencil.Move(0,SurfaceRay(patch,0,.03f));Assert.IsTrue(pencil.IsDrawing,"An XRI pointer target is not a physical drawing obstruction");
   var solid=SurfaceObstacle(patch);Assert.IsFalse(RoomItem.IsPointerOnly(solid));pencil.Move(0,SurfaceRay(patch,.03f));Assert.IsFalse(pencil.IsDrawing);
   Assert.AreEqual(2,editor.Read(editor.Identity(block)).surfaces[0].strokes.Single().points.Length,"The ordinary unregistered solid stops the stroke without a segment through it");
   solid.enabled=false;pointer.transform.position=SurfaceRay(patch).origin;Assert.IsTrue(SurfaceHit(patch),"Pointer targets containing the ray origin are also ignored");
   Assert.That(owner.Grab.colliders,Does.Contain(pointer));yield return null;
  }
  [UnityTest]public IEnumerator AuthoringPointerDrawingSaturatedOriginsAndRaysRemainBlocked(){
   var patch=SurfaceOcclusionStage();var owner=AuthoringPointerOwner();var pointers=new BoxCollider[64];
   for(int i=0;i<pointers.Length;i++){pointers[i]=SurfaceObstacle(patch,-.12f);AuthoringPointerRegister(owner,pointers[i]);}
   Assert.IsFalse(SurfaceHit(patch),"Saturated origin queries cannot prove that only pointer targets are present");
   for(int i=1;i<pointers.Length;i++)pointers[i].enabled=false;Assert.IsTrue(SurfaceHit(patch));
   foreach(var pointer in pointers){pointer.enabled=true;pointer.transform.position=patch.TransformPoint(new Vector3(0,0,-.06f));}
   Assert.IsFalse(SurfaceHit(patch),"Saturated ray queries remain inconclusive before role filtering");
   for(int i=1;i<pointers.Length;i++)pointers[i].enabled=false;Assert.IsTrue(SurfaceHit(patch));yield return null;
  }
  [UnityTest]public IEnumerator AuthoringPointerPackingRetainsOnNewSolidAndRetriesThroughPointers(){
   var(id,view,capture)=PackingStage();var before=editor.Read(id).heightFields[0];int count=editor.Snapshot().objects.Length;var pointers=PackingPointerTargets(view,1);
   var contact=SculptContact(view);capture.Finger(0,contact+view.Surface.up*.3f);capture.Finger(0,contact);
   Assert.IsTrue(capture.Active,editor.Status);Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));
   var position=editor.transform.TransformPoint(JsonUtility.FromJson<Vector3>(SculptFact("material.pack.capture")["position"].ToString()));
   var go=new GameObject("Ordinary unregistered packing obstruction");go.transform.SetParent(root.transform,false);go.transform.position=position;
   var solid=go.AddComponent<BoxCollider>();solid.size=Vector3.one*.02f;Assert.IsFalse(RoomItem.IsPointerOnly(solid));
   capture.End(0);yield return FinishSculptSave(capture);Assert.IsTrue(capture.Retained);Assert.IsFalse(capture.CanResolve(capture.SessionId,false,out var error));StringAssert.Contains("clear space",error);
   Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));
   solid.enabled=false;yield return ContainerAction(new RoomAgentExecutor(editor),SculptResolve(capture));
   Assert.IsFalse(capture.Busy,editor.Status);var ball=editor.Read(PackedId());Assert.IsNotNull(ball);Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
   Assert.That(before.VolumeLitres-editor.Read(id).heightFields[0].VolumeLitres,Is.EqualTo(ball.materialStores[0].amountLitres).Within(.000001));Assert.IsTrue(RoomItem.IsPointerOnly(pointers[0]));
   editor.Undo();Assert.IsNull(editor.Read(ball.id));Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));yield return null;
  }
  [UnityTest]public IEnumerator AuthoringPointerPackingSaturationCannotConsumeMaterialAndRecovers(){
   var(id,view,capture)=PackingStage();var before=editor.Read(id).heightFields[0];int count=editor.Snapshot().objects.Length;var pointers=PackingPointerTargets(view,64);
   var contact=SculptContact(view);capture.Finger(0,contact+view.Surface.up*.3f);capture.Finger(0,contact);
   Assert.IsFalse(capture.Busy);StringAssert.Contains("clear space",editor.Status);Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heights));
   for(int i=1;i<pointers.Length;i++)pointers[i].enabled=false;
   capture.Finger(0,contact+view.Surface.up*.3f);capture.Finger(0,contact);Assert.IsTrue(capture.Active,editor.Status);capture.End(0);yield return FinishSculptSave(capture);
   Assert.IsFalse(capture.Busy,editor.Status);var ball=editor.Read(PackedId());Assert.IsNotNull(ball);Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
   Assert.That(before.VolumeLitres-editor.Read(id).heightFields[0].VolumeLitres,Is.EqualTo(ball.materialStores[0].amountLitres).Within(.000001));yield return null;
  }
 }
}
