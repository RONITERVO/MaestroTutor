// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class ManagedAllocationTests
    {
        [Test] public void AllocatingAndEmptyWorkAreDistinguishedWithoutLeakingControlSamples()
        {
            object retained = null;
            long emptyBefore = ManagedAllocations.Measure(() => { });
            long allocated = ManagedAllocations.Measure(() => retained = new byte[4096]);
            long emptyAfter = ManagedAllocations.Measure(() => { });
            TestContext.WriteLine($"Unity allocation events: empty before={emptyBefore}, allocated={allocated}, empty after={emptyAfter}");
            Assert.That(allocated, Is.GreaterThan(0), "A recorder which always returns zero cannot verify allocation-free code");
            Assert.That(retained, Is.Not.Null);
            Assert.That(emptyBefore, Is.Zero);
            Assert.That(emptyAfter, Is.Zero, "Control and prior measurements must not leak into the operation");
        }
    }
}
