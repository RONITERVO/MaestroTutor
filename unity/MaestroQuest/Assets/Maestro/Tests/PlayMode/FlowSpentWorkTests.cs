// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  JObject SharedWorkLedger(){
   var result=new JObject{["committed"]=new JObject(),["consumed"]=new JObject(),["retired"]=new JObject(),["limits"]=new JObject()};
   foreach(string resource in new[]{"discoveryUnits","physicsUnits","liquidUnits","contactUnits","physicsBodies","liquidPasses","contactPasses"}){
    var row=CollisionFact("runtime.simulationWorkLedger",new JObject{["resource"]=resource});
    Assert.That(row,Is.Not.Null);Assert.That((string)row["resource"],Is.EqualTo(resource));
    if(result["step"]!=null)Assert.That((long)row["step"],Is.EqualTo((long)result["step"]));result["step"]=row["step"];
    foreach(string column in new[]{"committed","consumed","retired"})result[column][resource]=row[column];
    result["limits"][resource]=row["limit"];
   }
   return result;
  }
  [UnityTest]public IEnumerator FlowSpentWorkNativeForcesAndContactsKeepConsumedChargesAfterRelease() {
   root.AddComponent<RuntimeDiagnostics>();var(_,prop)=MediumFixture(.05f);FlowWorkResources.Advance(.05f);
   var evidence=new JObject();
   try {
    editor.Liquids.TickMedium(.02f);var medium=editor.Liquids.ObserveMediumBody(prop);
    Assert.That((bool)medium["applied"],Is.True,medium.ToString());
    ContactPoint(new Vector3(4,1.4f,0));var contact=editor.Liquids.ObserveContact("input:left");
    Assert.That((bool)contact["known"],Is.True,contact.ToString());Assert.That((bool)contact["immersed"],Is.True);
    var active=SharedWorkLedger();evidence["active"]=active;evidence["medium"]=medium;evidence["contact"]=contact;
    Assert.That((long)active["consumed"]["physicsUnits"],Is.GreaterThan(0));Assert.That((long)active["consumed"]["contactUnits"],Is.GreaterThan(0));
    PhysicsWorkResources.Release(editor.Find(prop).GetComponent<RigidRoomItem>());ContactWorkResources.Release(editor.Liquids);
    var retired=SharedWorkLedger();evidence["retired"]=retired;
    Assert.That((long)retired["retired"]["physicsUnits"],Is.GreaterThan(0));Assert.That((long)retired["retired"]["contactUnits"],Is.EqualTo((long)active["consumed"]["contactUnits"]));
    var again=SharedWorkLedger();Assert.That(JToken.DeepEquals(retired,again),Is.True,"Shared fact reads cannot consume or refund work");
    FlowWorkResources.Advance(.05f);evidence["next"]=SharedWorkLedger();
    Assert.That((long)evidence["next"]["retired"]["physicsUnits"],Is.Zero);Assert.That((long)evidence["next"]["retired"]["contactUnits"],Is.Zero);
   }finally{physics.PausePhysics();FlowWorkResources.Advance(.05f);}
   string path=Environment.GetEnvironmentVariable("MAESTRO_FLOW_SPENT_EVIDENCE");if(!string.IsNullOrEmpty(path))File.WriteAllText(path+".medium.json",evidence.ToString());
   yield return null;
  }
  [UnityTest]public IEnumerator FlowSpentWorkRemovingAnExecutedPassCannotRefundItsOpportunity() {
   var(from,to)=FixedFlowCups();physics.StartPhysics();FlowWorkResources.Advance(.05f);
   editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(to),Is.GreaterThan(0));double accepted=FlowAmount(to);
   int used=FlowWorkResources.Demand(2);
   var evidence=new JObject{["consumed"]=SimulationWorkSchedule.ObserveLedger()};
   FlowWorkResources.Release(editor.Liquids);
   evidence["retired"]=SimulationWorkSchedule.ObserveLedger();
   var peer=SimulationWorkSchedule.Register(()=>true,0,0);peer.FlowUnits=FlowWorkResources.MaximumUnits-used+1;
   try {
    Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.False,"Removing a pass after native quantity work must not refund spent capacity");
    Assert.That(FlowAmount(to),Is.EqualTo(accepted));
    FlowWorkResources.Advance(.05f);Assert.That(SimulationWorkSchedule.Ready(peer),Is.True);
    evidence["next"]=SimulationWorkSchedule.ObserveLedger();
   }finally{SimulationWorkSchedule.Remove(peer);physics.PausePhysics();FlowWorkResources.Advance(.05f);}
   editor.Undo();Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(to),Is.Zero);
   string path=Environment.GetEnvironmentVariable("MAESTRO_FLOW_SPENT_EVIDENCE");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,evidence.ToString());
   yield return null;
  }
 }
}
