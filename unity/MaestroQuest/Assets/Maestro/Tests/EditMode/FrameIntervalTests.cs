// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Diagnostics;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class FrameIntervalTests
    {
        [Test] public void SmoothFramesUseRealIntervalsAndAnEmptyWindowIsExplicit()
        {
            var window=new FrameIntervalWindow();Assert.That(window.Read(0).Samples,Is.Zero);window.Sample(0);
            for(int i=1;i<=720;i++)window.Sample(i/72.0);
            var value=window.Read(10);Assert.That(value.Samples,Is.EqualTo(720));Assert.That(value.Seconds,Is.EqualTo(10).Within(1e-9));
            Assert.That(value.MeanMs,Is.EqualTo(1000.0/72).Within(1e-8));Assert.That(value.P95Ms,Is.EqualTo(value.MeanMs).Within(1e-8));
            Assert.That(value.MaxMs,Is.EqualTo(value.MeanMs).Within(1e-8));Assert.That(value.CapacityLimited,Is.False);
            window.Clear();window.Sample(100);Assert.That(window.Read(100).Samples,Is.Zero,"The background gap is not a sample");
        }
        [Test] public void NearestRankTailAndLongStallsAreNotHiddenByTheWindow()
        {
            var window=new FrameIntervalWindow();double now=0;window.Sample(now);
            for(int i=0;i<100;i++){now+=i<95?.01:.1;window.Sample(now);}
            var value=window.Read(now);Assert.That(value.P95Ms,Is.EqualTo(10).Within(1e-8));Assert.That(value.MaxMs,Is.EqualTo(100).Within(1e-8));
            window.Sample(now+31);value=window.Read(now+31);Assert.That(value.Samples,Is.EqualTo(1));Assert.That(value.MaxMs,Is.EqualTo(31000).Within(1e-8));
            Assert.That(value.Seconds,Is.EqualTo(31));Assert.That(window.Read(now+61).Samples,Is.Zero);
        }
        [Test] public void CapacityAndAgeAreReportedWithoutUnboundedStorage()
        {
            var window=new FrameIntervalWindow();window.Sample(0);for(int i=1;i<=5000;i++)window.Sample(i*.001);
            var value=window.Read(5.5);Assert.That(value.Samples,Is.EqualTo(FrameIntervalWindow.Capacity));Assert.That(value.CapacityLimited,Is.True);
            Assert.That(value.Seconds,Is.EqualTo(4.096).Within(1e-8));Assert.That(value.AgeSeconds,Is.EqualTo(.5).Within(1e-8));
            value=window.Read(31);Assert.That(value.CapacityLimited,Is.False);Assert.That(value.Samples,Is.LessThan(FrameIntervalWindow.Capacity));
            Assert.That(window.Read(36).Samples,Is.Zero);
        }
        [Test] public void RepeatedTicksDoNotInventSamplesAndInvalidClocksReset()
        {
            var window=new FrameIntervalWindow();window.Sample(1);window.Sample(1);Assert.That(window.Read(1).Samples,Is.Zero);
            window.Sample(2);window.Sample(.5);Assert.That(window.Read(.5).Samples,Is.Zero);window.Sample(.6);Assert.That(window.Read(.6).Samples,Is.EqualTo(1));
            window.Sample(1002);Assert.That(window.Read(1002).Samples,Is.Zero);
            foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1}){window.Sample(invalid);window.Sample(2);Assert.That(window.Read(2).Samples,Is.Zero);window.Sample(3);Assert.That(window.Read(invalid).Samples,Is.Zero);}
        }
        [Test] public void RollingStatisticsUseOnlyUnexpiredSamples()
        {
            var window=new FrameIntervalWindow();window.Sample(0);window.Sample(1);for(int i=1;i<=3100;i++)window.Sample(1+i*.01);
            var value=window.Read(32);Assert.That(value.Samples,Is.InRange(2999,3001));Assert.That(value.MaxMs,Is.EqualTo(10).Within(1e-8));Assert.That(value.CapacityLimited,Is.False);
        }
    }
}
