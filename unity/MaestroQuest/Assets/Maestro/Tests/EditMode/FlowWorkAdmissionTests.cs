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
   Assert.That(FlowWorkResources.Demand(1),Is.EqualTo(2682));Assert.That(FlowWorkResources.Demand(2),Is.EqualTo(9372));
   for(int n=2;n<=64;n++)Assert.That(FlowWorkResources.Demand(n),Is.GreaterThan(FlowWorkResources.Demand(n-1)));
   Assert.That(FlowWorkResources.Demand(16),Is.EqualTo(523872));
   Assert.That(FlowWorkResources.Demand(17),Is.GreaterThan(FlowWorkResources.MaximumUnits));
   Assert.That(FlowWorkResources.Demand(16),Is.LessThan(FlowWorkResources.MaximumUnits));Assert.That(FlowWorkResources.Demand(int.MaxValue),Is.EqualTo(int.MaxValue));
  }
  [Test]public void FlowWorkAdmissionCoversEveryReceiverLockWithoutAnotherRayPass(){
   Assert.That(FlowWorkResources.PourPresentationUnits,Is.EqualTo(38));
   int segments=LiquidPouring.Segments;
   for(int members=2;members<=RoomContainer.MaximumPerRoom;members++)for(int lockedAt=1;lockedAt<=segments;lockedAt++){
    int search=members*lockedAt,deposition=segments-lockedAt+1,surface=segments-lockedAt+1;
    int retainedPath=segments+1,captures=2,finalization=1;
    int observed=search+deposition+surface+retainedPath+captures+finalization;
    Assert.That(observed,Is.LessThanOrEqualTo(members*segments+FlowWorkResources.PourPresentationUnits),
     "Candidate search and both locked suffix passes must fit for every group size and aperture position");
   }
   // Both no-receiver search and an isolated source use only the original ray
   // grid; singleton flow never evaluates deposition against itself.
   Assert.That(segments+(segments+1)+1,Is.LessThanOrEqualTo(segments+FlowWorkResources.PourPresentationUnits));
  }
 }
}
