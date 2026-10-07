// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Book;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class AcousticOutputMonitorTests
    {
        [Test] public void ActivityAndDeviceTailAreSeparateFromPcmCompletion()
        {
            var monitor = new AcousticOutputMonitor();
            Assert.AreEqual((false, false), monitor.Read(10, .05));
            monitor.Arm(10); Assert.AreEqual((true, false), monitor.Read(10, .05));
            // 100 ms at the final stereo listener, not at the PCM sender.
            var signal = new float[4800 * 2]; signal[0] = .02f;
            monitor.Observe(signal, 2, 48000, 10);
            monitor.Observe(new float[960], 2, 48000, 10.2);
            Assert.AreEqual((true, false), monitor.Read(10.299, .05));
            Assert.AreEqual((false, false), monitor.Read(10.301, .05));
            // Another voice or remaining reflection re-arms only the activity.
            monitor.Observe(signal, 2, 48000, 10.31);
            Assert.AreEqual((true, false), monitor.Read(10.32, .05));
        }
        [Test] public void NewSpeechDoesNotClearAnEarlierVoiceTail()
        {
            var monitor = new AcousticOutputMonitor(); monitor.Arm(4);
            monitor.Observe(new float[] { .1f, .1f }, 2, 48000, 4.2);
            monitor.Arm(4.21);
            Assert.IsTrue(monitor.Read(4.3, .05).Suppressed);
            monitor.Observe(new float[2], 2, 48000, 4.5);
            Assert.AreEqual((false, false), monitor.Read(4.5, .05));
        }
        [Test] public void MissingOrStalledDspFailsClosedUsingMonotonicTime()
        {
            var monitor = new AcousticOutputMonitor(); monitor.Arm(20);
            Assert.AreEqual((true, false), monitor.Read(20.6, .05));
            Assert.AreEqual((true, true), monitor.Read(21.51, .05));
            monitor.Observe(new float[2], 2, 48000, 22);
            Assert.AreEqual((false, false), monitor.Read(22, .05));
            Assert.AreEqual((true, false), monitor.Read(22.51, .05));
            Assert.AreEqual((true, true), monitor.Read(23.51, .05));
        }
        [Test] public void InvalidFramesCannotReportQuietAndHealthyFramesCanRecover()
        {
            var monitor = new AcousticOutputMonitor(); monitor.Arm(3);
            monitor.Observe(new float[2], 2, 48000, 3.3);
            foreach (var data in new[] { null, new float[0], new[] { float.NaN, 0f }, new[] { float.PositiveInfinity, 0f }, new float[3] })
            {
                monitor.Observe(data, 2, 48000, 3.4);
                Assert.IsTrue(monitor.Read(3.4, .05).Suppressed);
                monitor.Observe(new float[2], 2, 48000, 3.4);
                Assert.IsFalse(monitor.Read(3.4, .05).Suppressed);
            }
        }
        [Test] public void InvalidClockAndDeviceTailFailClosed()
        {
            var monitor = new AcousticOutputMonitor(); monitor.Arm(5);
            monitor.Observe(new float[2], 2, 48000, 5.3);
            Assert.AreEqual((true, true), monitor.Read(5.2, .05));
            Assert.AreEqual((true, true), monitor.Read(double.NaN, .05));
            foreach (var tail in new[] { -.1, 2.1, double.NaN, double.PositiveInfinity })
                Assert.AreEqual((true, true), monitor.Read(5.3, tail));
            monitor.Observe(new float[2], 2, 48000, 5.2);
            Assert.IsTrue(monitor.Read(5.3, .05).Suppressed);
        }
    }
}
