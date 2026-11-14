// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using NUnit.Framework;
using Unity.Profiling;

namespace Maestro.Quest.Tests
{
    // Synchronous, current-thread allocation events in the Unity Editor, not bytes
    // or device-wide memory. Construct delegates and assert results outside Measure.
    public static class ManagedAllocations
    {
        public static long Measure(Action operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True, "Unity's allocation recorder must be available");

            // Zero samples is meaningful only after this very recorder has detected
            // an allocation. Keep it alive so the control cannot be optimized away.
            recorder.Start();
            GC.KeepAlive(new byte[1024]);
            recorder.Stop();
            Assert.That(recorder.Count, Is.GreaterThan(0), "Allocation positive control was not recorded");
            Assert.That(recorder.GetSample(0).Count, Is.GreaterThan(0), "Allocation positive control was empty");
            recorder.Reset();
            Assert.That(recorder.Count, Is.Zero, "Reset must clear the control samples");

            recorder.Start();
            try { operation(); }
            finally { recorder.Stop(); }
            return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }
    }

}
