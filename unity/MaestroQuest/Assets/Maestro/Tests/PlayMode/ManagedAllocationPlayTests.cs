// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ManagedAllocationPlayTests
    {
        [Test] public void PlayModeRecorderDetectsAllocationsAndResetsForEmptyWork()
        {
            long allocated=ManagedAllocations.Measure(() => GC.KeepAlive(new byte[4096]));
            long empty=ManagedAllocations.Measure(() => { });
            TestContext.WriteLine($"PlayMode allocation events: allocated={allocated}, empty={empty}");
            Assert.That(allocated,Is.GreaterThan(0));Assert.That(empty,Is.Zero);
        }
    }
}
