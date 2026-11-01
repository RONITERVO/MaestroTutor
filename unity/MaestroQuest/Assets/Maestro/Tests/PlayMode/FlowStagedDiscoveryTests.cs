// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator FlowStagedDiscoveryWaitKeepsMovingPhysicsQueuedAndRetainsMomentum() {
   editor.Liquids.enabled=false;var ids=PhysicsIslandChain();var ex=new RoomAgentExecutor(editor);
   yield return ContainerAction(ex,ContainerCall(ids[0],new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));
   StartIsland();yield return IslandActive(ids);
   FlowWorkResources.Release(editor.Liquids);FlowWorkResources.Advance(.05f);
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.DiscoveryUnits=FlowDiscoveryWork.MaximumUnits;
   try {
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    foreach(var id in ids){var body=editor.Find(id).GetComponent<Rigidbody>();body.linearVelocity=Vector3.right*3;editor.Find(id).GetComponent<RigidRoomItem>().Refresh();}
    foreach(var id in ids) {
     Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);
     Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.True,"A discovery turn must retain motion: "+PhysicsInputFact(id)+", discoveryQueued="+FlowWorkResources.QueuedDiscovery(editor.Liquids)+", executionQueued="+FlowWorkResources.Queued(editor.Liquids)+", bodyQueued="+PhysicsWorkResources.Queued(editor.Find(id).GetComponent<RigidRoomItem>()));
    }
    Assert.That(FlowWorkResources.QueuedDiscovery(editor.Liquids),Is.True);
    Assert.That(FlowWorkResources.Queued(editor.Liquids),Is.True);
    Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False);
   }finally{SimulationWorkSchedule.Remove(pressure);}
   yield return IslandActive(ids);
   foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(.1f));
   physics.PausePhysics();
  }
  [UnityTest]public IEnumerator FlowStagedDiscoveryLoadsColdInputsWhileExecutionWaitsWithoutReservingIt() {
   var(from,to)=FixedFlowCups();
   Assert.That(editor.RetireNativeArea(NativeAreaFor(to),out var error),Is.True,error);
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.FlowUnits=FlowWorkResources.MaximumUnits;
   var evidence=new JObject();
   JObject Capture()=>new(){["discovery"]=FlowWorkResources.ObserveDiscovery(),["execution"]=FlowWorkResources.ObserveSchedule(),["pending"]=editor.NativeActivationPending,["captures"]=editor.Liquids.LastFlowDiscovery.Captures,["source"]=editor.Liquids.Observe(from),["receiver"]=editor.Liquids.Observe(to)};
   try {
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    physics.StartPhysics();editor.Liquids.Tick(.05f);evidence["loading"]=Capture();
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.EqualTo(1),"Input discovery must not wait for a quantity execution grant");
    Assert.That(editor.NativeActivationPending,Is.True,"Actual cold receiver loading should start under execution pressure");
    Assert.That(FlowWorkResources.ExecutionClaims(editor.Liquids).Any(claim=>claim.Granted),Is.False);
    Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(to),Is.Zero);
    yield return NavigationLoadingDone();
    Assert.That(editor.Find(to),Is.Not.Null);
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    editor.Liquids.Tick(.05f);evidence["preparedWaiting"]=Capture();
    Assert.That(editor.Liquids.LastFlowDiscovery.Captures,Is.EqualTo(1),"Loading completion requires a fresh synchronous capture");
    Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False);
    Assert.That(FlowWorkResources.Queued(editor.Liquids),Is.True);
    Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(to),Is.Zero);Assert.That(editor.Liquids.Active,Is.False);
    SimulationWorkSchedule.Remove(pressure);pressure=null;
    editor.Liquids.Tick(.05f);evidence["flowing"]=Capture();
    Assert.That(FlowAmount(to),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));
    Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));
    physics.PausePhysics();editor.Undo();
    Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(to),Is.Zero);
    string folder=Environment.GetEnvironmentVariable("MAESTRO_FLOW_STAGED_EVIDENCE");
    if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"cold-execution-wait.json"),evidence.ToString());}
   }finally{if(pressure!=null)SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();}
  }
 }
}
