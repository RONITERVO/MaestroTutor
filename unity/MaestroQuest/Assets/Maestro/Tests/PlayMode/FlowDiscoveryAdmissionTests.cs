// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  void CaptureFlowDiscovery(string name){
   string folder=Environment.GetEnvironmentVariable("MAESTRO_FLOW_DISCOVERY_EVIDENCE");if(string.IsNullOrEmpty(folder))return;
   Directory.CreateDirectory(folder);if(!root.GetComponent<RuntimeDiagnostics>())root.AddComponent<RuntimeDiagnostics>();var s=editor.Liquids.LastFlowDiscovery;
   File.WriteAllText(Path.Combine(folder,name+".json"),new JObject{["budget"]=CollisionFact("runtime.flowDiscoveryBudget"),["captures"]=s.Captures,["collisionReads"]=s.CollisionReads,["collisionCandidates"]=s.CollisionCandidates,["cavityReads"]=s.CavityReads,["cavityCandidates"]=s.CavityCandidates,["probes"]=s.Probes,["pathSegments"]=s.PathSegments,["islandQueries"]=s.IslandQueries,["islandMembers"]=s.IslandMembers,["graphEdges"]=s.GraphEdges,["objects"]=editor.SpatialObjectCount,["nativePending"]=editor.NativeActivationPending}.ToString());
  }
  [UnityTest]public IEnumerator FlowDiscoveryPressureDefersGeometryAndServesColdRoomUnderContinuingDemand(){
   var(a,b)=FixedFlowCups();Assert.That(editor.RetireNativeArea(NativeAreaFor(b),out var error),Is.True,error);
   FlowWorkResources.Advance(.05f); // Setup discovery was real spent work, even after its owner paused.
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.DiscoveryUnits=FlowDiscoveryWork.MaximumUnits;
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);physics.StartPhysics();editor.Liquids.Tick(.05f);CaptureFlowDiscovery("waiting");
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.Zero,"Discovery must receive capacity before reading geometry");
    Assert.That(editor.NativeActivationPending,Is.False,"A denied discovery pass must not begin native loading");
    Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowWorkResources.QueuedDiscovery(editor.Liquids),Is.True);Assert.That(FlowWorkResources.Queued(editor.Liquids),Is.True,"Retain conservative member dependencies while discovery waits");
    Assert.That(FlowWorkResources.ExecutionClaims(editor.Liquids).All(claim=>!claim.Granted),Is.True,"A queued dependency cannot execute before discovery");
    long step=FlowWorkResources.Step;
    for(int read=0;read<8;read++){FlowWorkResources.ObserveBudget();editor.Liquids.Observe(a);}
    Assert.That(FlowWorkResources.Step,Is.EqualTo(step));Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.Zero);
    for(int attempt=0;attempt<8&&editor.Liquids.LastFlowDiscovery.Captures==0;attempt++){
     FlowWorkResources.Advance(.05f);SimulationWorkSchedule.TryAdmit(pressure);editor.Liquids.Tick(.05f);
    }
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.EqualTo(1),"A complete fitting request cannot deadlock behind continuing discovery demand");
    Assert.That(editor.NativeActivationPending,Is.True);yield return NavigationLoadingDone();
    for(int attempt=0;attempt<8&&FlowAmount(b)==0;attempt++){
     FlowWorkResources.Advance(.05f);SimulationWorkSchedule.TryAdmit(pressure);editor.Liquids.Tick(.05f);
    }
    CaptureFlowDiscovery("resumed");Assert.That(FlowAmount(b),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));
    Assert.That(FlowAmount(a)+FlowAmount(b),Is.EqualTo(400).Within(1e-8));
   }finally{SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator FlowDiscoveryCapturesCollisionOnceAndRefreshesARealMovedObstacle(){
   var(a,b,c,d)=IndependentFlowCups();
   Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Movable stream obstacle",new(12,2,0),.25f,Color.white,out var obstacle,out var error),Is.True,error);
   Assert.That(editor.SetItemPhysics(obstacle,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   physics.StartPhysics();editor.Liquids.Tick(.05f);CaptureFlowDiscovery("initial");
   var s=editor.Liquids.LastFlowDiscovery;Assert.That(s.Captures,Is.EqualTo(1));Assert.That(s.Probes,Is.GreaterThan(1));
   Assert.That(s.CollisionReads,Is.GreaterThan(0).And.LessThanOrEqualTo(editor.SpatialObjectCount),"Each accepted object's collision extent must be captured at most once per discovery");
   Assert.That(FlowAmount(b),Is.GreaterThan(0));double right=FlowAmount(d);
   var item=editor.Find(obstacle);item.transform.position=editor.Find(b).transform.position+Vector3.up*.4f;item.GetComponent<RigidRoomItem>().Teleported();
   editor.Liquids.Tick(.05f);CaptureFlowDiscovery("moved-obstacle");
   Assert.That(FlowAmount(a,"spilledMl"),Is.GreaterThan(0),"The next discovery must use the obstacle's current native placement");
   Assert.That(FlowAmount(d),Is.GreaterThan(right));Assert.That(editor.Liquids.LastFlowDiscovery.CollisionReads,Is.LessThanOrEqualTo(editor.SpatialObjectCount));
   Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(a,"spilledMl"),Is.EqualTo(400).Within(1e-8));
   physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowDiscoveryInventoryGrowthInvalidatesAndResizesTheWholeGrant(){
   RainCup();var go=new GameObject("Logical liquid owner for inventory freshness");var host=go.AddComponent<LiquidPouring>();host.enabled=false;host.Initialize(editor);host.Synchronize(editor.Snapshot());
   SimulationWorkSchedule.Claim pressure=null;
   try{
    Assert.That(FlowWorkResources.TryAdmit(host,out var error),Is.True,error);var claim=FlowWorkResources.FindDiscoveryClaim(host);int previous=claim.DiscoveryUnits,captures=host.LastFlowDiscovery.Captures;
    pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.DiscoveryUnits=(int)(FlowDiscoveryWork.MaximumUnits-SimulationWorkSchedule.DiscoveryUnits);Assert.That(pressure.DiscoveryUnits,Is.GreaterThan(0));
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Extra accepted discovery source",new(12,1,0),.1f,Color.white,out _,out error),Is.True,error);
    long step=FlowWorkResources.Step;Assert.That(FlowWorkResources.Ready(host,out _),Is.False,"Same vessels with a larger canonical scene need a fresh allowance");
    Assert.That(FlowWorkResources.TryAdmit(host,out _),Is.False);Assert.That(claim.DiscoveryUnits,Is.EqualTo(previous),"Refused growth retains the prior booked charge instead of evicting another owner");
    FlowWorkResources.ObserveDiscovery();Assert.That(FlowWorkResources.Step,Is.EqualTo(step));Assert.That(host.LastFlowDiscovery.Captures,Is.EqualTo(captures),"Refused growth must not perform another geometry capture");
    SimulationWorkSchedule.Remove(pressure);pressure=null;
    Assert.That(FlowWorkResources.TryAdmit(host,out error),Is.True,error);Assert.That(claim.DiscoveryUnits,Is.GreaterThan(previous));Assert.That(FlowWorkResources.Ready(host,out _),Is.True);
   }finally{if(pressure!=null)SimulationWorkSchedule.Remove(pressure);UnityEngine.Object.DestroyImmediate(go);physics.PausePhysics();}
   yield return null;
  }
  [UnityTest]public IEnumerator FlowDiscoveryPhysicsCanPrepareBeforeLiquidCaptureWithoutHalfAdmission(){
   editor.Liquids.enabled=false;var ids=PhysicsIslandChain();var ex=new RoomAgentExecutor(editor);yield return ContainerAction(ex,ContainerCall(ids[0],new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));
   var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.DiscoveryUnits=FlowDiscoveryWork.MaximumUnits;
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);StartIsland();foreach(var body in bodies)body.Refresh();
    Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False);Assert.That(bodies.All(body=>!body.Simulating),Is.True);
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.Zero,"Physics admission can register the complete demand before discovering liquid geometry");
    bool served=false;
    for(int i=0;i<8&&!served;i++){
     FlowWorkResources.Advance(.05f);SimulationWorkSchedule.TryAdmit(pressure);foreach(var body in bodies)body.Refresh();
     bool ready=FlowWorkResources.Ready(editor.Liquids,out _);
     foreach(var body in bodies)Assert.That(PhysicsWorkResources.Ready(body,out _),Is.EqualTo(ready),"Discovery, force and liquid grants must be atomic");
     served=ready&&bodies.All(body=>body.Simulating);if(!served)yield return new WaitForFixedUpdate();
    }
    Assert.That(served,Is.True);editor.Liquids.Tick(.05f);CaptureFlowDiscovery("physics-first");
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.EqualTo(1));Assert.That(editor.Liquids.LastFlowDiscovery.IslandQueries,Is.GreaterThan(0));
   }finally{SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();}
  }
 }
}
