// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class CapabilityOptionalPatternTests {
        [TestCase("")][TestCase("The route is blocked")]
        public void UnrestrictedTextDoesNotTreatJsonNullAsARegex(string value){Assert.That(CapabilityArguments.Validate(new JValue(value),CapabilitySchema.Text(null,32),out var error),Is.True,error);}
        [Test]public void OptionalPatternStillEnforcesTextAndLengthBounds(){
            Assert.That(CapabilityArguments.Validate(new JValue("line\nbreak"),CapabilitySchema.Text(null,32),out _),Is.False);
            Assert.That(CapabilityArguments.Validate(new JValue(new string('x',33)),CapabilitySchema.Text(null,32),out _),Is.False);
            Assert.That(CapabilityArguments.Validate(new JValue("wrong"),CapabilitySchema.Text("^right$",32),out _),Is.False);
        }
    }
}
