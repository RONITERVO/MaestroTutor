// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Reflection;
using System.Threading;
using Maestro.Quest.Book;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class AcousticOutputMonitorTests
    {
        [Test] public void CurrentReadSamplesTimeAfterConcurrentAudioObservation()
        {
            double now=5.1;
            var monitor=new AcousticOutputMonitor(()=>now);monitor.Arm(5);
            // Hold the real observation lock until the reader is waiting for it.
            // A clock sampled before this lock would be older than the callback.
            var sync=typeof(AcousticOutputMonitor).GetField("sync",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(monitor);
            using var started=new ManualResetEventSlim(false);
            (bool Suppressed,bool Failed) result=default;Exception failure=null;
            var reader=new Thread(()=>{started.Set();try{result=monitor.ReadCurrent(.05);}catch(Exception error){failure=error;}}){IsBackground=true};
            bool entered,waiting;
            lock(sync){
                reader.Start();entered=started.Wait(TimeSpan.FromSeconds(3));
                waiting=SpinWait.SpinUntil(()=>(reader.ThreadState&ThreadState.WaitSleepJoin)!=0,3000);
                now=5.3;monitor.Observe(new[]{.1f,.1f},2,48000,now);
            }
            Assert.IsTrue(reader.Join(3000),"Reader must finish after the audio callback releases its lock");
            Assert.IsTrue(entered);Assert.IsTrue(waiting);Assert.IsNull(failure);
            Assert.AreEqual((true,false),result,"A healthy newer callback must not be mistaken for a backwards clock");
            now=5.6;monitor.Observe(new float[2],2,48000,now);Assert.AreEqual((false,false),monitor.ReadCurrent(.05));
            now=7.2;Assert.AreEqual((true,true),monitor.ReadCurrent(.05),"Real DSP stalls must still stop capture");
        }
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
