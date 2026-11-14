// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  GameObject StreamOriginBlocker(Vector3 point,int layer){
   var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Solid enclosing stream origin";
   blocker.transform.SetParent(root.transform);blocker.transform.position=point;
   blocker.transform.localScale=Vector3.one*.08f;blocker.layer=layer;Physics.SyncTransforms();return blocker;
  }
  Vector3 StreamOrigin(string id)=>ContainerFlowGeometry.Lip(editor.Read(id).containers[0],editor.Find(id).transform,Vector3.up,out _);
  [UnityTest]public IEnumerator StreamOriginInsideScannedSolidCannotTransferAndRecovers(){
   var(_,from,to)=PouringCups();var blocker=StreamOriginBlocker(StreamOrigin(from),RoomPhysicsLayers.Scanned);
   double amount=Liquid(from);editor.Liquids.Tick(.05f);
   Assert.AreEqual(0,Liquid(to),"A ray starting inside a solid cannot establish a clear stream");
   Assert.AreEqual(amount,Liquid(from));Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(1,editor.Liquids.LastFlowExecution.PourOriginQueries);Assert.That(editor.Liquids.LastFlowExecution.PourOriginCandidates,Is.InRange(1,FlowWorkResources.QueryHits));
   blocker.GetComponent<Collider>().enabled=false;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
   Assert.Greater(Liquid(to),0);Assert.That(Liquid(from)+Liquid(to),Is.EqualTo(amount).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator StreamOriginTerrainOutletInsideAuthoredSolidKeepsItsWater(){
   var(_,from,to,lip,_)=SpillingTerrain();var blocker=StreamOriginBlocker(lip,RoomPhysicsLayers.Environment);
   double amount=Liquid(from);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(0,Liquid(to));Assert.AreEqual(amount,Liquid(from));Assert.IsFalse(editor.Liquids.Active);
   blocker.GetComponent<Collider>().enabled=false;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
   Assert.Greater(Liquid(to),0);Assert.Less(Liquid(from),amount);
   Assert.That(Liquid(from)+Liquid(to)+(double)editor.Liquids.Observe(from)["contents"]["spilledMl"],Is.EqualTo(amount).Within(1e-6));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator StreamOriginUsesSourcesIndependentRealRoomPolicy(){
   var(_,from,to)=PouringCups();StreamOriginBlocker(StreamOrigin(from),RoomPhysicsLayers.Scanned);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Authored stream ground";floor.layer=RoomPhysicsLayers.Environment;
   floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(3,-1,0);floor.transform.localScale=new Vector3(4,.1f,4);
   floor.AddComponent<RoomWalkableSurface>().Publish(floor.GetComponent<Collider>());
   var item=editor.Find(from);item.GetComponent<RoomEnvironmentBinding>().Apply(physics,item,false);Physics.SyncTransforms();
   Assert.IsFalse(physics.IncludesRealRoom(item));Assert.IsTrue(physics.CanSimulate(StreamOrigin(from),item));
   double amount=Liquid(from);editor.Liquids.Tick(.05f);Assert.Greater(Liquid(to),0);
   Assert.That(Liquid(from)+Liquid(to),Is.EqualTo(amount).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator StreamOriginOwnCollidersAreAllowedButSaturationPreservesContents(){
   var(_,from,to)=PouringCups();var item=editor.Find(from);var origin=StreamOrigin(from);var owned=new List<Collider>();
   for(int i=0;i<FlowWorkResources.QueryHits;i++){
    var part=new GameObject("Owned outlet rim "+i);part.transform.SetParent(item.transform);part.transform.position=origin;part.layer=RoomPhysicsLayers.Item;
    var collider=part.AddComponent<BoxCollider>();collider.size=Vector3.one*.01f;owned.Add(collider);
   }
   Physics.SyncTransforms();double amount=Liquid(from);editor.Liquids.Tick(.05f);
   Assert.AreEqual(amount,Liquid(from));Assert.AreEqual(0,Liquid(to));Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(1,editor.Liquids.LastFlowExecution.PourOriginQueries);Assert.AreEqual(0,editor.Liquids.LastFlowExecution.PourOriginCandidates,"A saturated buffer cannot establish complete ownership");
   for(int i=1;i<owned.Count;i++)owned[i].enabled=false;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
   Assert.Greater(Liquid(to),0,"A deliberate source-owned lip collider is not an unrelated obstruction");
   Assert.That(Liquid(from)+Liquid(to),Is.EqualTo(amount).Within(1e-8));
   physics.PausePhysics();yield return LiquidSaved();
  }
 }
}
