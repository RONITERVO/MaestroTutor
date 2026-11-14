// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class ContactWorkAdmissionTests {
        [Test]public void ContactWorkAdmissionBoundsPotentialContactDemand(){
            Assert.That(ContactWorkResources.BaseDemand,Is.EqualTo(1252));
            Assert.That(ContactWorkResources.BodyDemand(1),Is.EqualTo(8512));
            Assert.That(ContactWorkResources.BodyDemand(-1),Is.EqualTo(ContactWorkResources.BodyDemand(0)));
            Assert.That(ContactWorkResources.BodyDemand(100),Is.EqualTo(ContactWorkResources.BodyDemand(64)));
            Assert.That(ContactWorkResources.MaximumUnits,Is.GreaterThan(ContactWorkResources.BaseDemand+32*ContactWorkResources.BodyDemand(1)));
        }
    }
}
