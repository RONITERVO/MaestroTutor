// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  (RoomAgentExecutor ex,string pond,string cup,Vector3 lip,Vector3 outward) SpillingTerrain(){
   var(ex,id)=TerrainReservoir();
   var bed=editor.Read(id).heightFields[0];
   for(int z=0;z<=bed.cells;z++)for(int x=0;x<=bed.cells;x++)bed.heights[z*(bed.cells+1)+x]=x==0||x==bed.cells||z==0||z==bed.cells?.4f:.05f;
   bed.heights[(bed.cells/2)*(bed.cells+1)+bed.cells]=.2f;
   Assert.IsTrue(editor.EditHeightField(id,editor.ObjectRevision(id),bed,out var error),error);
   var c=editor.Read(id).containers[0];c.version=4;c.amountMl=c.capacityMl;
   var call=ContainerCall(id,c);call["arguments"]["definition"]["spillAtTerrainEdge"]=true;ContainerFixture(ex,call);
   var source=editor.Find(id);var terrain=RoomTerrainReservoir.Native(c,source.transform);
   var lip=ContainerFlowGeometry.Lip(c,source.transform,Vector3.up,out var outward,terrain);
   string cup=CreateTemplateFixture(TemplateCall("cup",lip+outward*.045f+Vector3.down*.5f));
   Physics.SyncTransforms();return(ex,id,cup,lip,outward);
  }
  [UnityTest]public IEnumerator TerrainSpillConservesReceivedAndLostWaterWithSavedUndoAndReuse(){
   var(ex,pond,cup,_,_)=SpillingTerrain();double amount=Liquid(pond);Assert.That(Liquid(cup),Is.Zero);
   var definition=ContainerCapability.Definition(editor.Read(pond).containers[0],true,true);Assert.IsTrue((bool)definition["spillAtTerrainEdge"]);
   var old=editor.Snapshot();old.version=43;Assert.IsFalse(old.Validate(out _));
   var prototype=CreationPrototype.Capture(editor.Read(pond));Assert.AreEqual(10,prototype.version);Assert.IsTrue(prototype.Validate(out var error),error);
   Assert.IsTrue(CapabilityArguments.Validate(CreationPrototypeSchema.Encode(prototype),CreationPrototypeSchema.Schema(),out error),error);
   prototype.version=9;Assert.IsFalse(prototype.Validate(out _));
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   double received=Liquid(cup),left=Liquid(pond),lost=(double)editor.Liquids.Observe(pond)["contents"]["spilledMl"];
   Assert.Greater(received,0);Assert.Less(left,amount);Assert.That(left+received+lost,Is.EqualTo(amount).Within(1e-6));
   physics.PausePhysics();yield return LiquidSaved();
   Assert.AreEqual(left,editor.Read(pond).containers[0].amountMl);
   var saved=new RoomStorage(directory).Load(out error);Assert.IsNotNull(saved,error);Assert.IsTrue(saved.objects.Single(o=>o.id==pond).containers[0].SpillAtTerrainEdge);
   Assert.AreEqual(left,saved.objects.Single(o=>o.id==pond).containers[0].amountMl);
   editor.Undo();Assert.AreEqual(amount,Liquid(pond));Assert.AreEqual(0,Liquid(cup));
   editor.Redo();Assert.AreEqual(left,Liquid(pond));Assert.AreEqual(received,Liquid(cup));
   yield return null;
  }
  [UnityTest]public IEnumerator TerrainSpillLegacyContentEditsPreservePolicyAndExplicitFalseClosesIt(){
   var(ex,pond,_,_,_)=SpillingTerrain();var c=editor.Read(pond).containers[0];c.amountMl=12345;
   var legacy=ContainerCall(pond,c);Assert.IsNull(legacy["arguments"]["definition"]["spillAtTerrainEdge"]);
   yield return ContainerAction(ex,legacy);
   Assert.IsTrue(editor.Read(pond).containers[0].SpillAtTerrainEdge);Assert.AreEqual(12345,Liquid(pond));
   var close=ContainerCall(pond,editor.Read(pond).containers[0]);close["arguments"]["definition"]["spillAtTerrainEdge"]=false;
   yield return ContainerAction(ex,close);
   Assert.IsFalse(editor.Read(pond).containers[0].SpillAtTerrainEdge);Assert.AreEqual(12345,Liquid(pond));
   editor.Undo();Assert.IsTrue(editor.Read(pond).containers[0].SpillAtTerrainEdge);
  }
  [UnityTest]public IEnumerator TerrainSpillRespectsSolidObstaclesAndPausedPhysics(){
   var(_,pond,cup,lip,outward)=SpillingTerrain();double amount=Liquid(pond);
   editor.Liquids.Tick(.05f);Assert.AreEqual(amount,Liquid(pond));Assert.AreEqual(0,Liquid(cup));
   var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform);blocker.layer=RoomPhysicsLayers.Scanned;
   blocker.transform.position=lip+outward*.03f+Vector3.down*.2f;blocker.transform.localScale=new Vector3(.3f,.02f,.3f);Physics.SyncTransforms();
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(0,Liquid(cup));Assert.Less(Liquid(pond),amount);
   Assert.That(Liquid(pond)+(double)editor.Liquids.Observe(pond)["contents"]["spilledMl"],Is.EqualTo(amount).Within(1e-6));
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator TerrainSpillCannotRunWithUnavailableBedOrBelowItsEdge(){
   var(ex,pond,cup,_,_)=SpillingTerrain();var c=editor.Read(pond).containers[0];
   c.amountMl=ContainerFlowGeometry.RetainedCapacity(c,editor.Find(pond).transform.rotation,Vector3.up,RoomTerrainReservoir.Native(c,editor.Find(pond).transform))*.9;
   yield return ContainerAction(ex,ContainerCall(pond,c));double amount=Liquid(pond);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.AreEqual(amount,Liquid(pond));Assert.AreEqual(0,Liquid(cup));
   physics.PausePhysics();yield return LiquidSaved();
   c.amountMl=c.capacityMl;yield return ContainerAction(ex,ContainerCall(pond,c));amount=Liquid(pond);
   editor.Find(pond).GetComponent<HeightFieldView>().Collision.enabled=false;
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.AreEqual(amount,Liquid(pond));Assert.AreEqual(0,Liquid(cup));
   physics.PausePhysics();yield return LiquidSaved();
  }
 }
}
