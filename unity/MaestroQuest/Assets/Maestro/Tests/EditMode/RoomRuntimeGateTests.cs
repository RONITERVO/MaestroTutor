// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RoomRuntimeGateTests
    {
        [Test] public void IndependentHoldsRequireTheirOwnReleaseAndRejectUnboundedReasons()
        {
            var gate=new RoomRuntimeGate();var review=gate.Hold("Review imported behaviours");var switching=gate.Hold("Saving previous workspace");
            review.Dispose();review.Dispose();Assert.That(gate.Held,Is.True);Assert.That(gate.Reason,Is.EqualTo("Saving previous workspace"));
            switching.Dispose();Assert.That(gate.Held,Is.False);Assert.That(gate.Reason,Is.Null);
            Assert.Throws<ArgumentException>(()=>gate.Hold("bad\nreason"));Assert.Throws<ArgumentException>(()=>gate.Hold(new string('x',121)));
        }
        [Test] public void CleanupFailuresKeepAllConsumersHeldEvenWhenFailureOccursDuringRelease()
        {
            var gate=new RoomRuntimeGate();bool first=false,last=false;gate.Changed+=()=>first=gate.Held;
            gate.Changed+=()=>{if(!gate.Held)throw new InvalidOperationException("Cleanup failed");};gate.Changed+=()=>last=gate.Held;
            var hold=gate.Hold("Review workspace");Assert.That(first&&last,Is.True);hold.Dispose();
            Assert.That(gate.Held,Is.True);Assert.That(first&&last,Is.True);StringAssert.Contains("cleanup failed",gate.Reason);
            using(var another=gate.Hold("Another owner")){}Assert.That(gate.Held,Is.True);
        }
    }
}
