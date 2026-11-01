// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class PhysicsWorkAdmissionTests
    {
        [Test]public void PhysicsWorkAdmissionLogicalCostBoundsActualDisplacementAndCandidateLoops()
        {
            Assert.That(PhysicsWorkResources.Demand(1),Is.EqualTo(4352));
            Assert.That(PhysicsWorkResources.Demand(64),Is.EqualTo(8384));
            Assert.That(PhysicsWorkResources.Demand(int.MaxValue),Is.EqualTo(PhysicsWorkResources.Demand(MediumDisplacement.MaximumColliders)));
            Assert.That(PhysicsWorkResources.MissingMediumChecks,Is.GreaterThanOrEqualTo(16));
            Assert.That(PhysicsWorkResources.MaximumReservations,Is.EqualTo(128));
            Assert.That(PhysicsWorkResources.MaximumUnits,Is.EqualTo(524288));
        }
    }
}
