// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class FlowWorkAdmissionTests {
  [Test]public void FlowWorkAdmissionInvalidOpportunityCannotAdvanceClock(){
   long before=FlowWorkResources.Step;
   foreach(float seconds in new[]{0,-1,float.NaN,float.PositiveInfinity})FlowWorkResources.Advance(seconds);
   Assert.That(FlowWorkResources.Step,Is.EqualTo(before));
  }
  [Test]public void FlowWorkAdmissionBoundsCompletePotentialDemand(){
   Assert.That(FlowWorkResources.Demand(-1),Is.Zero);Assert.That(FlowWorkResources.Demand(0),Is.Zero);
   Assert.That(FlowWorkResources.Demand(1),Is.EqualTo(2644));Assert.That(FlowWorkResources.Demand(2),Is.EqualTo(9296));
   for(int n=2;n<=64;n++)Assert.That(FlowWorkResources.Demand(n),Is.GreaterThan(FlowWorkResources.Demand(n-1)));
   Assert.That(FlowWorkResources.Demand(16),Is.EqualTo(523264));
   Assert.That(FlowWorkResources.Demand(17),Is.GreaterThan(FlowWorkResources.MaximumUnits));
   Assert.That(FlowWorkResources.Demand(16),Is.LessThan(FlowWorkResources.MaximumUnits));Assert.That(FlowWorkResources.Demand(int.MaxValue),Is.EqualTo(int.MaxValue));
  }
 }
}
