// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class FlowDiscoveryAdmissionTests {
  [Test]public void FlowDiscoveryBoundsEveryCurrentlyAcceptedInventoryWithoutOverflow(){
   Assert.That(FlowDiscoveryWork.Demand(0,2),Is.Zero);
   for(int objects=1;objects<=FlowDiscoveryWork.MaximumObjects;objects++)
    for(int vessels=1;vessels<=objects;vessels++){
     int cost=FlowDiscoveryWork.Demand(vessels,objects);
     Assert.That(cost,Is.GreaterThan(0).And.LessThanOrEqualTo(FlowDiscoveryWork.MaximumUnits));
     if(vessels>1)Assert.That(cost,Is.GreaterThan(FlowDiscoveryWork.Demand(vessels-1,objects)));
     if(objects>vessels)Assert.That(cost,Is.GreaterThan(FlowDiscoveryWork.Demand(vessels,objects-1)));
    }
   Assert.That(FlowDiscoveryWork.Demand(-1,2),Is.EqualTo(int.MaxValue));Assert.That(FlowDiscoveryWork.Demand(2,1),Is.EqualTo(int.MaxValue));
   Assert.That(FlowDiscoveryWork.Demand(1,int.MaxValue),Is.EqualTo(int.MaxValue));Assert.That(FlowDiscoveryWork.Demand(int.MaxValue,int.MaxValue),Is.EqualTo(int.MaxValue));
  }
  [Test]public void FlowDiscoveryPreparationGateLeavesExecutionQueuedUntilReady() {
   var discovery=SimulationWorkSchedule.Register(()=>true,0,0);discovery.DiscoveryUnits=1;
   var execution=SimulationWorkSchedule.Register(()=>true,0,1);execution.FlowUnits=FlowWorkResources.MaximumUnits;
   execution.Prepared=()=>SimulationWorkSchedule.Ready(discovery);
   try {
    Assert.That(SimulationWorkSchedule.Queued(execution),Is.True);
    Assert.That(SimulationWorkSchedule.TryAdmit(execution),Is.False);Assert.That(execution.Granted,Is.False);
    SimulationWorkSchedule.Advance(.05f);
    Assert.That(discovery.Granted,Is.True);Assert.That(execution.Granted,Is.True,"Discovery grants must be selected before dependent execution eligibility");
   }finally{SimulationWorkSchedule.Remove(execution);SimulationWorkSchedule.Remove(discovery);}
  }
  [Test]public void FlowDiscoveryLostPreparationDoesNotRefundAlreadyBookedExecution() {
   bool prepared=true;
   var execution=SimulationWorkSchedule.Register(()=>true,0,1);execution.FlowUnits=FlowWorkResources.MaximumUnits;execution.Prepared=()=>prepared;
   var peer=SimulationWorkSchedule.Register(()=>true,0,1);peer.FlowUnits=1;
   try {
    Assert.That(SimulationWorkSchedule.TryAdmit(execution),Is.True);prepared=false;
    Assert.That(SimulationWorkSchedule.Queued(execution),Is.True);
    Assert.That(SimulationWorkSchedule.Ready(execution),Is.False);
    Assert.That(SimulationWorkSchedule.TryAdmit(peer),Is.False,"The old grant still occupies this opportunity");
    long age=execution.Served;SimulationWorkSchedule.Advance(.05f);
    Assert.That(peer.Granted,Is.True);Assert.That(execution.Served,Is.EqualTo(age),"Input waiting cannot pretend to serve the execution request");
    prepared=true;SimulationWorkSchedule.Advance(.05f);
    Assert.That(execution.Granted,Is.True);Assert.That(peer.Granted,Is.False);
   }finally{SimulationWorkSchedule.Remove(peer);SimulationWorkSchedule.Remove(execution);}
  }
  [Test]public void FlowDiscoveryCannotDependOnExecutionCapacityEvenWhenBothWouldFit(){
   var discovery=SimulationWorkSchedule.Register(()=>true,0,0);var execution=SimulationWorkSchedule.Register(()=>true,0,0);
   try{
    discovery.DiscoveryUnits=1;execution.FlowUnits=1;execution.Dependencies=new[]{discovery};SimulationWorkSchedule.Changed();
    Assert.That(SimulationWorkSchedule.TryAdmit(execution),Is.False);Assert.That(SimulationWorkSchedule.Ready(discovery),Is.False);Assert.That(SimulationWorkSchedule.Queued(execution),Is.False);
    StringAssert.Contains("independent",SimulationWorkSchedule.Refusal(execution,"waiting"));
    Assert.That(discovery.Granted||execution.Granted,Is.False);
   }finally{SimulationWorkSchedule.Remove(execution);SimulationWorkSchedule.Remove(discovery);}
  }
 }
}
