// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class SimulationWorkLedgerTests {
  static SimulationWorkSchedule.Claim Request(string lane,int amount){
   var c=SimulationWorkSchedule.Register(()=>true,0,0);
   switch(lane){
    case "liquidUnits":c.FlowUnits=amount;break;case "physicsUnits":c.ForceUnits=amount;break;
    case "contactUnits":c.ContactUnits=amount;break;case "discoveryUnits":c.DiscoveryUnits=amount;break;
    case "physicsBodies":c.Bodies=amount;break;case "liquidPasses":c.Rooms=amount;break;case "contactPasses":c.Samplers=amount;break;
   }return c;
  }
  [SetUp]public void Before()=>SimulationWorkSchedule.Advance(.05f);
  [TearDown]public void After()=>SimulationWorkSchedule.Advance(.05f);
  [TestCase("liquidUnits",524288)][TestCase("physicsUnits",524288)][TestCase("contactUnits",524288)][TestCase("discoveryUnits",524288)]
  [TestCase("physicsBodies",128)][TestCase("liquidPasses",32)][TestCase("contactPasses",32)]
  public void SimulationWorkLedgerRetainsConsumedLaneAfterRemovalUntilNextOpportunity(string lane,int limit){
   var used=Request(lane,limit);var peer=Request(lane,1);
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(used),Is.True);Assert.That(SimulationWorkSchedule.Consume(used),Is.True);
    Assert.That(SimulationWorkSchedule.Consume(used),Is.True,"Repeated consumption does not book the same allowance twice");
    SimulationWorkSchedule.Remove(used);SimulationWorkSchedule.Remove(used);
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["retired"][lane],Is.EqualTo(limit));
    Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.False);
    SimulationWorkSchedule.Advance(.05f);Assert.That(SimulationWorkSchedule.Ready(peer),Is.True);
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["retired"][lane],Is.Zero);
   }finally{SimulationWorkSchedule.Remove(used);SimulationWorkSchedule.Remove(peer);}
  }
  [Test]public void SimulationWorkLedgerUnusedReservationCanBeReleased(){
   var unused=Request("liquidUnits",FlowWorkResources.MaximumUnits);var peer=Request("liquidUnits",1);
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(unused),Is.True);SimulationWorkSchedule.Remove(unused);
    Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.True);
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["consumed"]["liquidUnits"],Is.Zero);
   }finally{SimulationWorkSchedule.Remove(unused);SimulationWorkSchedule.Remove(peer);}
  }
  [Test]public void SimulationWorkLedgerShrinkAndSplitDoNotRefundConsumedPart(){
   int half=FlowWorkResources.MaximumUnits/2;var a=Request("liquidUnits",half);var b=Request("liquidUnits",half);var peer=Request("liquidUnits",half+1);
   b.Dependencies=new[]{a};SimulationWorkSchedule.Changed();
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(b),Is.True);Assert.That(SimulationWorkSchedule.Consume(a),Is.True);
    b.Dependencies=System.Array.Empty<SimulationWorkSchedule.Claim>();SimulationWorkSchedule.Changed();SimulationWorkSchedule.Remove(b);
    a.FlowUnits=1;Assert.That(SimulationWorkSchedule.TryAdmit(a),Is.True);
    Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.False,"The split cannot refund the already used half");
    peer.FlowUnits=half;Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.True,"The unused half can be released");
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["committed"]["liquidUnits"],Is.EqualTo(FlowWorkResources.MaximumUnits));
    SimulationWorkSchedule.Remove(a);
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["retired"]["liquidUnits"],Is.EqualTo(half));
   }finally{SimulationWorkSchedule.Remove(a);SimulationWorkSchedule.Remove(b);SimulationWorkSchedule.Remove(peer);}
  }
  [Test]public void SimulationWorkLedgerGrowthUsesOnlyAdditionalCapacityAndFailureDoesNotConsume(){
   int half=FlowWorkResources.MaximumUnits/2;var a=Request("liquidUnits",half);var b=Request("liquidUnits",half);
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(a),Is.True);Assert.That(SimulationWorkSchedule.Consume(a),Is.True);
    Assert.That(SimulationWorkSchedule.TryAdmit(b),Is.True);a.FlowUnits=half+1;
    Assert.That(SimulationWorkSchedule.Consume(a),Is.False);a.FlowUnits=half;
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["consumed"]["liquidUnits"],Is.EqualTo(half));
    SimulationWorkSchedule.Remove(b);a.FlowUnits=FlowWorkResources.MaximumUnits;
    Assert.That(SimulationWorkSchedule.TryAdmit(a),Is.True);Assert.That(SimulationWorkSchedule.Consume(a),Is.True);
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["consumed"]["liquidUnits"],Is.EqualTo(FlowWorkResources.MaximumUnits));
   }finally{SimulationWorkSchedule.Remove(a);SimulationWorkSchedule.Remove(b);}
  }
  [Test]public void SimulationWorkLedgerObservationAndUnavailableConsumptionArePassive(){
   var c=Request("liquidUnits",1);c.Prepared=()=>false;
   try{
    var before=SimulationWorkSchedule.ObserveLedger();Assert.That(SimulationWorkSchedule.Consume(c),Is.False);
    Assert.That(JToken.DeepEquals(before,SimulationWorkSchedule.ObserveLedger()),Is.True);
    Assert.That(c.Granted,Is.False);
   }finally{SimulationWorkSchedule.Remove(c);}
  }
 }
}
