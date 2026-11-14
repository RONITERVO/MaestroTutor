// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator ContactFairSamplingServesLaterRoomWithoutInventingMissedEdges(){
   root.AddComponent<RuntimeDiagnostics>();var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
   ContactPoint(new Vector3(4,2,0));ContactWorkResources.Release(editor.Liquids);
   var held=FillContactWork();foreach(var go in held){var sampler=go.GetComponent<LiquidPouring>();sampler.enabled=false;Assert.That(ContactWorkResources.TryAdmit(sampler,out var error),Is.True,error);}
   var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
   try{
    ContactPoint(new Vector3(4,1.4f,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);
    var trace=new JArray();bool sampled=false;long start=FlowWorkResources.Step;
    for(int frame=0;frame<256&&FlowWorkResources.Step-start<8;frame++){
     foreach(var go in (frame%2==0?held:held.AsEnumerable().Reverse()))ContactWorkResources.TryAdmit(go.GetComponent<LiquidPouring>(),out _);
     ContactPoint(new Vector3(4,1.4f,0));var observed=editor.Liquids.ObserveContact("input:left");sampled=(bool)observed["known"];
     Assert.That(MediumContactFacts.Input().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["side"]="left"},out _),Is.True,"Waiting and resumed observations must both fit the shared program value contract");
     if(trace.Count==0||(long)trace[trace.Count-1]["schedule"]["step"]!=FlowWorkResources.Step||sampled)trace.Add(new JObject{["schedule"]=CollisionFact("runtime.contactWorkSchedule"),["flowStep"]=FlowWorkResources.Step,["physicsStep"]=CollisionFact("runtime.physicsWorkSchedule")["step"],["budget"]=CollisionFact("runtime.contactWorkBudget"),["contact"]=observed,["events"]=events.Count});
     Assert.That(events,Is.Empty,"An unobserved water entry must not be invented when a delayed sample resumes");
     if(sampled){Assert.That((bool)observed["immersed"],Is.True);Assert.That((double)observed["sampleSpeedMetresPerSecond"],Is.Zero);break;}
     yield return new WaitForFixedUpdate();
    }
    Assert.That(held.All(go=>go&&go.GetComponent<LiquidPouring>().ContactWorkEligible),Is.True);
    Assert.That(sampled,Is.True,"A later complete contact pass must eventually sample under continuing demand");
    string output=Environment.GetEnvironmentVariable("MAESTRO_CONTACT_FAIR_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["competitors"]=held.Count,["trace"]=trace}.ToString());
    Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
   }finally{ReleaseContactWork(held);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator ContactFairSamplingBoundsItsQueueWithoutReadOrCallOrderBias(){
   RainCup();ContactWorkResources.Release(editor.Liquids);var hosts=new List<GameObject>();var samplers=new List<LiquidPouring>();
   try{
    for(int i=0;i<ContactWorkResources.MaximumReservations+1;i++){
     var go=new GameObject("Logical competing contact consumer");hosts.Add(go);var sampler=go.AddComponent<LiquidPouring>();sampler.enabled=false;sampler.Initialize(editor);sampler.Synchronize(editor.Snapshot());samplers.Add(sampler);
     Assert.That(ContactWorkResources.TryAdmit(sampler,out _),Is.EqualTo(i<Math.Min(ContactWorkResources.MaximumExecutions,ContactWorkResources.MaximumUnits/sampler.ContactWorkDemand)));Assert.That(ContactWorkResources.Queued(sampler),Is.EqualTo(i<ContactWorkResources.MaximumReservations));
    }
    var seen=new HashSet<LiquidPouring>();
    int perTurn=Math.Min(ContactWorkResources.MaximumExecutions,ContactWorkResources.MaximumUnits/samplers[0].ContactWorkDemand);int turns=(ContactWorkResources.MaximumReservations+perTurn-1)/perTurn+1;
    for(int step=0;step<turns;step++){
     string before=ContactWorkResources.ObserveSchedule().ToString();long epoch=FlowWorkResources.Step;
     for(int repeat=0;repeat<4;repeat++)foreach(var sampler in samplers.AsEnumerable().Reverse()){ContactWorkResources.TryAdmit(sampler,out _);ContactWorkResources.ObserveBudget();ContactWorkResources.ObserveSchedule();}
     Assert.That(ContactWorkResources.ObserveSchedule().ToString(),Is.EqualTo(before));Assert.That(FlowWorkResources.Step,Is.EqualTo(epoch));
     FlowWorkResources.Advance(.05f);foreach(var sampler in samplers)if(ContactWorkResources.Ready(sampler,out _))seen.Add(sampler);
     var facts=ContactWorkResources.ObserveSchedule();Assert.That((int)facts["executions"],Is.LessThanOrEqualTo(32));Assert.That((int)facts["units"],Is.LessThanOrEqualTo(524288));
    }
    Assert.That(seen.Count,Is.EqualTo(ContactWorkResources.MaximumReservations));Assert.That(seen.Contains(samplers.Last()),Is.False);
   }finally{ReleaseContactWork(hosts);physics.PausePhysics();}yield return null;
  }
  [UnityTest]public IEnumerator ContactFairSamplingAutomaticPassRunsOnceAndMissedTurnIsANewBaseline(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
   FlowWorkResources.Advance(.05f);editor.Liquids.SetContactInput(0,new Vector3(4,2,0),true);editor.Liquids.TickAutomaticContacts();Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.True);
   editor.Liquids.SetContactInput(0,new Vector3(4,1.4f,0),true);for(int i=0;i<5;i++)editor.Liquids.TickAutomaticContacts();Assert.That(events,Is.Empty);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.False);
   FlowWorkResources.Advance(.05f);editor.Liquids.TickAutomaticContacts();Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered"}));
   int ripples=editor.Find(pool).GetComponent<ContainerFillView>().RippleCount;Assert.That(ripples,Is.GreaterThan(0));
   FlowWorkResources.Advance(.05f);FlowWorkResources.Advance(.05f);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False,"A newly booked allowance cannot certify a missed observation");
   FlowWorkResources.Advance(.05f);editor.Liquids.SetContactInput(0,new Vector3(4,2,0),true);editor.Liquids.TickAutomaticContacts();Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.True);var resumed=editor.Liquids.ObserveContact("input:left");Assert.That((bool)resumed["known"],Is.True);Assert.That((double)resumed["sampleSpeedMetresPerSecond"],Is.Zero);Assert.That(events.Count,Is.EqualTo(1));Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.EqualTo(ripples));
   FlowWorkResources.Advance(.05f);editor.Liquids.SetContactInput(0,new Vector3(4,1.4f,0),true);editor.Liquids.TickAutomaticContacts();Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered","entered"}));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator ContactFairSamplingWaitDoesNotBlockIndependentPhysicsOrLiquidExecution(){
   var(from,to)=FixedFlowCups();string id=PhysicsInputBall(new Vector3(-8,3,-8));StartIsland();yield return IslandActive(new[]{id});
   ContactWorkResources.Release(editor.Liquids);var held=FillContactWork();
   try{
    ContactTick();Assert.That(ContactWorkResources.Ready(editor.Liquids,out _),Is.False);Assert.That(ContactWorkResources.Queued(editor.Liquids),Is.True);
    var rigid=editor.Find(id).GetComponent<RigidRoomItem>();rigid.Refresh();Assert.That(rigid.Simulating,Is.True);Assert.That(PhysicsWorkResources.Ready(rigid,out var error),Is.True,error);
    editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from),Is.LessThan(400));Assert.That(ContactWorkResources.Ready(editor.Liquids,out _),Is.False);
   }finally{ReleaseContactWork(held);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator ContactFairSamplingNewOpportunityCancelsOldCallbackBatch(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new Vector3(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);FlowWorkResources.Advance(.05f);};
   BothContactPoints(new Vector3(4,1.4f,0));Assert.That(events.Select(e=>e.Participant),Is.EqualTo(new[]{"input:left"}));ContactTick();Assert.That(events.Count,Is.EqualTo(1));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator ContactFairSamplingSpeedUsesAtLeastTheObservedElapsedTime(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);ContactPoint(new Vector3(4,2,0));
   var states=(IDictionary)typeof(LiquidPouring).GetField("contacts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor.Liquids);var state=states["input:left"];
   state.GetType().GetField("At",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(state,Time.realtimeSinceStartupAsDouble-.15);
   ContactPoint(new Vector3(4,1.4f,0));var observed=editor.Liquids.ObserveContact("input:left");Assert.That((bool)observed["immersed"],Is.True);Assert.That((double)observed["sampleSpeedMetresPerSecond"],Is.GreaterThan(0).And.LessThan(5),"The nominal 0.05s step must not turn a 0.15s movement into triple its speed");physics.PausePhysics();yield return null;
  }
 }
}
