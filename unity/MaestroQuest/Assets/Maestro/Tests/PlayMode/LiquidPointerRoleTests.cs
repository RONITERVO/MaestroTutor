// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  (RoomItem owner,BoxCollider solid,BoxCollider[] pointers) LiquidPointerSurfaces(Bounds volume,int count=1){
   var go=new GameObject("Liquid pointer fixture");go.transform.SetParent(root.transform,false);
   var handle=new GameObject("Configured solid handle");handle.transform.SetParent(go.transform,false);handle.transform.position=new Vector3(-5,5,5);
   var solid=handle.AddComponent<BoxCollider>();solid.size=Vector3.one*.04f;
   var owner=go.AddComponent<RoomItem>();owner.Configure(new Collider[]{solid});
   Assert.IsFalse(owner.RegisterPointerSurface(solid),"A configured solid cannot be reclassified by pointer registration");
   var pointers=new BoxCollider[count];
   for(int i=0;i<count;i++){
    var target=new GameObject("Pointer target "+i);target.transform.SetParent(go.transform,false);target.transform.position=volume.center;
    var pointer=target.AddComponent<BoxCollider>();pointer.size=volume.size;pointers[i]=pointer;
    Assert.IsTrue(owner.RegisterPointerSurface(pointer));Assert.IsTrue(RoomItem.IsPointerOnly(pointer));
    Assert.That(owner.Grab.colliders,Does.Contain(pointer),"The exact-query exemption retains XRI registration");
   }
   Assert.AreEqual(1,owner.SolidColliderCount);Assert.IsFalse(RoomItem.IsPointerOnly(solid));Physics.SyncTransforms();return(owner,solid,pointers);
  }
  Bounds LiquidScoopPaths(string basin,string bucket,out Vector3 from,out Vector3 to){
   Assert.IsTrue(ContainerScoopingGeometry.TryContact(editor.Read(basin).containers[0],editor.Find(basin).transform,editor.Read(bucket).containers[0],editor.Find(bucket).transform,Vector3.up,out var contact));
   Assert.IsTrue(contact.Path(0,out from,out to));var bounds=new Bounds(from,Vector3.zero);bounds.Encapsulate(to);
   for(int i=1;i<FlowWorkResources.ScoopSamples;i++)if(contact.Path(i,out var a,out var b)){bounds.Encapsulate(a);bounds.Encapsulate(b);}
   bounds.Expand(.02f);return bounds;
  }
  static void LiquidSolidAt(BoxCollider solid,Bounds bounds){solid.transform.position=bounds.center;solid.size=bounds.size;Physics.SyncTransforms();}
  static void LiquidLeaveOnePointer(BoxCollider[] pointers){for(int i=1;i<pointers.Length;i++)pointers[i].enabled=false;Physics.SyncTransforms();}
  void AssertLiquidPointerWorkBounded(){
   var work=editor.Liquids.LastFlowExecution;
   Assert.That(work.PourHitCandidates,Is.LessThanOrEqualTo(work.PourRayQueries*(FlowWorkResources.QueryHits-1)));
   Assert.That(work.ScoopPathCandidates,Is.LessThanOrEqualTo(work.ScoopPathQueries*(FlowWorkResources.QueryHits-1)));
   Assert.That(work.ScoopPathQueries,Is.LessThanOrEqualTo(2*2*FlowWorkResources.ScoopSamples*3),"Both phases have two ordered distinct-vessel pairs and at most three queries per sample");
  }
  [UnityTest]public IEnumerator LiquidPointerOutletIgnoresTargetsButNotConfiguredSolids(){
   var(_,from,to)=PouringCups();var volume=new Bounds(StreamOrigin(from),Vector3.one*.08f);var(_,solid,_)=LiquidPointerSurfaces(volume);
   editor.Liquids.Tick(.05f);double received=Liquid(to),remaining=Liquid(from);
   Assert.Greater(received,0);Assert.That(remaining+received,Is.EqualTo(400).Within(1e-8));
   Assert.Greater(editor.Liquids.LastFlowExecution.PourOriginCandidates,0);
   LiquidSolidAt(solid,volume);editor.Liquids.Tick(.05f);
   Assert.AreEqual(received,Liquid(to));Assert.AreEqual(remaining,Liquid(from));AssertLiquidPointerWorkBounded();
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerStreamIgnoresTargetsButNotConfiguredSolids(){
   var(_,from,to)=PouringCups();var volume=new Bounds(new Vector3(editor.Find(to).transform.position.x,1.4f,0),new Vector3(.5f,.02f,.5f));var(_,solid,_)=LiquidPointerSurfaces(volume);
   editor.Liquids.Tick(.05f);double received=Liquid(to);Assert.Greater(received,0);
   Assert.Greater(editor.Liquids.LastFlowExecution.PourHitCandidates,0);AssertLiquidPointerWorkBounded();
   LiquidSolidAt(solid,volume);editor.Liquids.Tick(.05f);
   Assert.AreEqual(received,Liquid(to));double spilled=(double)editor.Liquids.Observe(from)["contents"]["spilledMl"];
   Assert.Greater(spilled,0);Assert.That(Liquid(from)+received+spilled,Is.EqualTo(400).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerOutletSaturationPreservesContentsAndRecovers(){
   var(_,from,to)=PouringCups();var(_,_,pointers)=LiquidPointerSurfaces(new Bounds(StreamOrigin(from),Vector3.one*.08f),FlowWorkResources.QueryHits);
   editor.Liquids.Tick(.05f);Assert.AreEqual(400,Liquid(from));Assert.AreEqual(0,Liquid(to));Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(1,editor.Liquids.LastFlowExecution.PourOriginQueries);Assert.AreEqual(0,editor.Liquids.LastFlowExecution.PourOriginCandidates,"A full buffer is rejected before classifying any pointer hit");
   LiquidLeaveOnePointer(pointers);editor.Liquids.Tick(.05f);Assert.Greater(Liquid(to),0);Assert.That(Liquid(from)+Liquid(to),Is.EqualTo(400).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerStreamSaturationPreservesContentsAndRecovers(){
   var(_,from,to)=PouringCups();var(_,_,pointers)=LiquidPointerSurfaces(new Bounds(new Vector3(editor.Find(to).transform.position.x,1.4f,0),new Vector3(.5f,.02f,.5f)),FlowWorkResources.QueryHits);
   editor.Liquids.Tick(.05f);Assert.AreEqual(400,Liquid(from));Assert.AreEqual(0,Liquid(to));Assert.IsFalse(editor.Liquids.Active);
   Assert.Greater(editor.Liquids.LastFlowExecution.PourRayQueries,0);AssertLiquidPointerWorkBounded();
   LiquidLeaveOnePointer(pointers);editor.Liquids.Tick(.05f);Assert.Greater(Liquid(to),0);Assert.That(Liquid(from)+Liquid(to),Is.EqualTo(400).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerScoopIgnoresTargetsAlongPathAndAtEndpointsButSolidsBlock(){
   var(_,basin,bucket)=ScoopingVessels();var paths=LiquidScoopPaths(basin,bucket,out var from,out var to);
   var lid=new Bounds((from+to)*.5f,new Vector3(paths.size.x,.001f,paths.size.z));var(_,solid,pointers)=LiquidPointerSurfaces(lid);
   editor.Liquids.Tick(.05f);double first=Liquid(bucket);Assert.Greater(first,0,"The five scoop rays may cross pointer targets");
   Assert.Greater(editor.Liquids.LastFlowExecution.ScoopPathCandidates,0);AssertLiquidPointerWorkBounded();
   pointers[0].transform.position=from;pointers[0].size=new Vector3(paths.size.x,.02f,paths.size.z);Physics.SyncTransforms();
   editor.Liquids.Tick(.05f);double second=Liquid(bucket);Assert.Greater(second,first,"Pointer targets may also contain an exact-query endpoint");
   LiquidSolidAt(solid,new Bounds(from,pointers[0].size));editor.Liquids.Tick(.05f);
   Assert.AreEqual(second,Liquid(bucket));Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));AssertLiquidPointerWorkBounded();
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerScoopSaturationPreservesContentsAndRecovers(){
   var(_,basin,bucket)=ScoopingVessels();var bounds=LiquidScoopPaths(basin,bucket,out _,out _);var(_,_,pointers)=LiquidPointerSurfaces(bounds,FlowWorkResources.QueryHits);
   editor.Liquids.Tick(.05f);Assert.AreEqual(0,Liquid(bucket));Assert.AreEqual(32000,Liquid(basin));Assert.IsFalse(editor.Liquids.Active);
   Assert.Greater(editor.Liquids.LastFlowExecution.ScoopPathQueries,0);Assert.AreEqual(0,editor.Liquids.LastFlowExecution.ScoopPathCandidates,"Saturated endpoint buffers cannot establish a pointer-only path");AssertLiquidPointerWorkBounded();
   LiquidLeaveOnePointer(pointers);editor.Liquids.Tick(.05f);Assert.Greater(Liquid(bucket),0);Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerScoopRaySaturationPreservesContentsAndRecovers(){
   var(_,basin,bucket)=ScoopingVessels();var paths=LiquidScoopPaths(basin,bucket,out var from,out var to);
   var lid=new Bounds((from+to)*.5f,new Vector3(paths.size.x,.001f,paths.size.z));var(_,_,pointers)=LiquidPointerSurfaces(lid,FlowWorkResources.QueryHits);
   var endpointHits=new Collider[64];int mask=physics.CollisionMask(~0,editor.Find(basin));Physics.SyncTransforms();
   Assert.AreEqual(0,Physics.OverlapSphereNonAlloc(from,.0005f,endpointHits,mask,QueryTriggerInteraction.Ignore),"The source endpoint is clear; this must exercise the ray buffer");
   Assert.AreEqual(0,Physics.OverlapSphereNonAlloc(to,.0005f,endpointHits,mask,QueryTriggerInteraction.Ignore),"The receiving endpoint is clear");
   editor.Liquids.Tick(.05f);Assert.AreEqual(0,Liquid(bucket));Assert.AreEqual(32000,Liquid(basin));Assert.IsFalse(editor.Liquids.Active);
   Assert.Greater(editor.Liquids.LastFlowExecution.ScoopPathQueries,0);Assert.AreEqual(0,editor.Liquids.LastFlowExecution.ScoopPathCandidates,"Saturated ray buffers are rejected before classifying pointer hits");AssertLiquidPointerWorkBounded();
   LiquidLeaveOnePointer(pointers);editor.Liquids.Tick(.05f);Assert.Greater(Liquid(bucket),0);Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator LiquidPointerTargetsPreserveFullSubmergedVesselImmersion(){
   var(_,basin,bucket)=ScoopingVessels();var paths=LiquidScoopPaths(basin,bucket,out _,out _);paths.Expand(.05f);LiquidPointerSurfaces(paths);
   var item=editor.Find(bucket);item.transform.rotation=Quaternion.Euler(.5f,0,.1f);item.GetComponent<RigidRoomItem>().Teleported();double spilled=0;
   editor.ContainerPoured+=(_,_,amount,_,_)=>spilled+=amount;
   for(int i=0;i<100;i++)editor.Liquids.Tick(.05f);
   Assert.Greater(editor.Liquids.LastFlowExecution.ImmersionChecks,0);Assert.Greater(editor.Liquids.LastFlowExecution.ScoopPathCandidates,0);AssertLiquidPointerWorkBounded();
   physics.PausePhysics();yield return LiquidSaved();Assert.That(Liquid(bucket),Is.EqualTo(2000).Within(1e-8));
   Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));Assert.AreEqual(0,spilled,"Pointer targets cannot make a submerged bucket drain through repeated spill and refill");
  }
 }
}
