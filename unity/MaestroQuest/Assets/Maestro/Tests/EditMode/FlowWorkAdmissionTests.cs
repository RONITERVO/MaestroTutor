// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class FlowWorkAdmissionTests {
  [Test]public void FlowWorkAdmissionBoundsCompletePotentialDemand(){
   Assert.That(FlowWorkResources.Demand(-1),Is.Zero);Assert.That(FlowWorkResources.Demand(0),Is.Zero);
   Assert.That(FlowWorkResources.Demand(1),Is.EqualTo(2579));Assert.That(FlowWorkResources.Demand(2),Is.EqualTo(5326));
   for(int n=2;n<=64;n++)Assert.That(FlowWorkResources.Demand(n),Is.GreaterThan(FlowWorkResources.Demand(n-1)));
   Assert.That(FlowWorkResources.Demand(16),Is.LessThan(FlowWorkResources.MaximumUnits));Assert.That(FlowWorkResources.Demand(int.MaxValue),Is.EqualTo(int.MaxValue));
  }
 }
}
