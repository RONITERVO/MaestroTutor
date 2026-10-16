// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator FlowFairSchedulingServesLaterCompleteGroupUnderSustainedDemand(){
   var(from,to)=FixedFlowCups();var ex=new RoomAgentExecutor(editor);var position=editor.Find(to).transform.position;
   for(int i=0;i<14;i++){
    string id=(string)TemplateRun(ex,TemplateCall("cup",position+Vector3.right*(.002f*i)))["selected"]["output"]["objectId"];
    Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
    Assert.That(editor.MissingLiquidExtent(id,editor.Read(id).containers[0],out _).Bounds.Intersects(editor.MissingLiquidExtent(to,editor.Read(to).containers[0],out _).Bounds),Is.True,"The complete receiving group must stay coupled");
   }
   Assert.That(editor.Liquids.FlowWorkVessels,Is.EqualTo(16));physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(from),Is.LessThan(400),"The actual native group must be able to flow before adding pressure");physics.PausePhysics();editor.Undo();Assert.That(Liquid(from),Is.EqualTo(400));physics.StartPhysics();FlowWorkResources.Release(editor.Liquids);
   var held=FillFlowWork();foreach(var claim in held)Assert.That(SimulationWorkSchedule.TryAdmit(claim),Is.True);var trace=new JArray();
   try{
    Assert.That(held.Count,Is.LessThan(FlowWorkResources.MaximumReservations),"Exercise work-unit pressure, not the owner-count ceiling");
    var before=FlowWorkResources.ObserveBudget();bool admitted=FlowWorkResources.TryAdmit(editor.Liquids,out var denial);
    Assert.That(admitted,Is.False,$"Incumbents initially own the available work: held={held.Count}, vessels={editor.Liquids.FlowWorkVessels}, before={before}, after={FlowWorkResources.ObserveBudget()}, reason={denial}");
    for(int frame=0;frame<32&&Liquid(from)==400;frame++){
     // Incumbents remain eligible and keep asking; the newcomer cannot win by
     // waiting for them to disappear. Reading facts never advances the queue.
     foreach(var go in (frame%2==0?held:held.AsEnumerable().Reverse()))SimulationWorkSchedule.TryAdmit(go);
     for(int read=0;read<8;read++){FlowWorkResources.ObserveBudget();editor.Liquids.Observe(from);}
     editor.Liquids.Tick(.05f);trace.Add(new JObject{["frame"]=frame,["contents"]=editor.Liquids.Observe(from),["budget"]=FlowWorkResources.ObserveBudget(),["schedule"]=FlowWorkResources.ObserveSchedule()});yield return new WaitForFixedUpdate();
    }
    string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_FAIR_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["incumbents"]=held.Count,["vesselsPerGroup"]=16,["trace"]=trace}.ToString());
    Assert.That(Liquid(from),Is.LessThan(400),"A later complete group must receive a bounded opportunity even while incumbents stay eligible");
   }finally{ReleaseFlowWork(held);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator FlowFairSchedulingBoundedQueueServesAllOwnersWithoutReadOrCallOrderBias(){
   string id=RainCup();Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);FlowWorkResources.Release(editor.Liquids);var hosts=new List<GameObject>();var samplers=new List<LiquidPouring>();
   try{
    for(int i=0;i<FlowWorkResources.MaximumReservations+1;i++){
     var go=new GameObject("Logical competing liquid consumer");hosts.Add(go);var sampler=go.AddComponent<LiquidPouring>();sampler.enabled=false;sampler.Initialize(editor);sampler.Synchronize(editor.Snapshot());samplers.Add(sampler);
     bool granted=FlowWorkResources.TryAdmit(sampler,out _);
     Assert.That(granted,Is.EqualTo(i<FlowWorkResources.MaximumExecutions));Assert.That(FlowWorkResources.Queued(sampler),Is.EqualTo(i<FlowWorkResources.MaximumReservations));
    }
    var seen=new HashSet<LiquidPouring>();
    for(int step=0;step<3;step++){
     var before=FlowWorkResources.ObserveSchedule().ToString();long epoch=FlowWorkResources.Step;
     for(int repeat=0;repeat<5;repeat++)foreach(var sampler in samplers.AsEnumerable().Reverse()){
      FlowWorkResources.TryAdmit(sampler,out _);FlowWorkResources.ObserveBudget();FlowWorkResources.ObserveSchedule();
     }
     Assert.That(FlowWorkResources.Step,Is.EqualTo(epoch));Assert.That(FlowWorkResources.ObserveSchedule().ToString(),Is.EqualTo(before));
     FlowWorkResources.Advance(.05f);
     foreach(var sampler in samplers)if(FlowWorkResources.Ready(sampler,out _))seen.Add(sampler);
     var facts=FlowWorkResources.ObserveSchedule();Assert.That((int)facts["executions"],Is.LessThanOrEqualTo(FlowWorkResources.MaximumExecutions));Assert.That((long)facts["units"],Is.LessThanOrEqualTo(FlowWorkResources.MaximumUnits));
    }
    Assert.That(seen.Count,Is.EqualTo(FlowWorkResources.MaximumReservations));Assert.That(seen.Contains(samplers.Last()),Is.False,"Queue-full is an explicit refusal, not a false fairness promise");Assert.That(Liquid(id),Is.Zero,"Allocation and observation cannot execute liquid work");
   }finally{ReleaseFlowWork(hosts);physics.PausePhysics();}yield return null;
  }
  [UnityTest]public IEnumerator FlowFairSchedulingRetainsPendingGeometryAcrossMissedOpportunity(){
   var(from,to)=FixedFlowCups();Assert.That(editor.RetireNativeArea(NativeAreaFor(to),out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(editor.NativeActivationPending,Is.True);
   FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();
   try{
    editor.Liquids.Tick(.05f);Assert.That(FlowWorkResources.QueuedDiscovery(editor.Liquids),Is.True);Assert.That(FlowWorkResources.ExecutionClaims(editor.Liquids).Any(claim=>claim.Granted),Is.False,"Pending input loading must not reserve quantity execution");Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False);Assert.That(editor.NativeActivationPending,Is.True);
    yield return NavigationLoadingDone();Assert.That(editor.Find(to),Is.Not.Null,"A scheduling delay must not cancel the already-owned native load");Assert.That(Liquid(from),Is.EqualTo(400));Assert.That(Liquid(to),Is.Zero);
   }finally{ReleaseFlowWork(held);}
   editor.Liquids.Tick(.05f);Assert.That(Liquid(to),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowFairSchedulingMotionChangeWhileDeferredCannotReplayOldThrow(){
   editor.Liquids.enabled=false;var ids=PhysicsIslandChain();var ex=new RoomAgentExecutor(editor);ContainerRun(ex,ContainerCall(ids[0],new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));StartIsland();yield return IslandActive(ids);
   FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();
   try{
    foreach(var id in ids){var body=editor.Find(id).GetComponent<Rigidbody>();body.linearVelocity=Vector3.right*3;editor.Find(id).GetComponent<RigidRoomItem>().Refresh();Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.True);}
    editor.Find(ids[0]).GetComponent<RigidRoomItem>().Teleported();
    foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False,"An actual motion edit invalidates the entire connected group's held motion");
   }finally{ReleaseFlowWork(held);}
   yield return IslandActive(ids);foreach(var id in ids)Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));physics.PausePhysics();yield return null;
  }
 }
}
