// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class ImportedMotionTimelineTests {
        [Test]public void EmbeddedAndReusableSourceKeepKeysInsideOneDisplayFrame(){
            var bytes=ModelFixture.HipPath(new[]{0f,.001f,.002f,1f},new[]{0f,2f,0f,0f});
            var embedded=ModelInspection.Inspect(bytes).AnimationTimelines.Single();var library=MotionPack.Extract("path.glb",bytes).Single().Timeline;
            foreach(var timeline in new[]{embedded,library}){
                Assert.That(timeline.Interval(0,.003f,1,false,out var points,out var error),Is.True,error);
                Assert.That(points.Select(p=>p.At),Does.Contain(.001f));Assert.That(points.Select(p=>p.At),Does.Contain(.002f));Assert.That(points.Last().At,Is.EqualTo(.003f));
            }
        }
        [Test]public void CubicTurningPointIsRetainedEvenWhenBothKeysAndDisplayTicksMissIt(){
            var bytes=ModelFixture.HipPath(new[]{0f,.001f},new[]{0f,0f,8000f,-8000f,0f,0f},true);
            foreach(var timeline in new[]{ModelInspection.Inspect(bytes).AnimationTimelines.Single(),MotionPack.Extract("cubic.glb",bytes).Single().Timeline}){
                Assert.That(timeline.Interval(0,.001f,.001f,false,out var points,out var error),Is.True,error);
                Assert.That(points.Any(p=>Math.Abs(p.At-.0005f)<.0000001f),Is.True,"Include the actual Hermite maximum, not just the zero-valued endpoints");
            }
        }
        [Test]public void CubicDerivativeFindsBothInteriorTurnsWithoutAddingLinearFalseTurns(){
            var keys=new System.Collections.Generic.HashSet<float>();
            ImportedMotionTimeline.IncludeCubicExtrema(keys,new[]{0f,1f},new[]{0f,0f,1f,1f,0f,0f});
            Assert.That(keys.Count,Is.EqualTo(2));var ordered=keys.OrderBy(x=>x).ToArray();Assert.That(ordered[0],Is.EqualTo((3-Math.Sqrt(3))/6).Within(.000001));Assert.That(ordered[1],Is.EqualTo((3+Math.Sqrt(3))/6).Within(.000001));
            keys.Clear();ImportedMotionTimeline.IncludeCubicExtrema(keys,new[]{0f,1f},new[]{1f,0f,1f,1f,1f,1f});Assert.That(keys,Is.Empty);
        }
        [Test]public void IntervalAddsSubstepsWithoutRoundingAwayAuthoredKeys(){
            var timeline=new ImportedMotionTimeline(new[]{0f,.10123f,.179f,1f});
            Assert.That(timeline.Interval(.1f,.2f,1,false,out var points,out var error),Is.True,error);
            Assert.That(points.Select(p=>p.At),Does.Contain(.10123f));Assert.That(points.Select(p=>p.At),Does.Contain(.179f));
            var times=new[]{.1f}.Concat(points.Select(p=>p.Time)).ToArray();for(int i=1;i<times.Length;i++)Assert.That(times[i]-times[i-1],Is.InRange(0,.008334f));
            Assert.That(points.Last().At,Is.EqualTo(.2f));
        }
        [Test]public void ForwardAndReverseSeamsKeepBothSourcePosesInTraversalOrder(){
            var timeline=new ImportedMotionTimeline(new[]{0f,1f});
            Assert.That(timeline.Interval(.99f,1.01f,1,true,out var forward,out _),Is.True);
            Assert.That(forward.Where(p=>p.Time==1).Select(p=>(p.At,p.Cycle)),Is.EqualTo(new[]{(1f,0),(0f,1)}));
            Assert.That(timeline.Interval(1.01f,.99f,1,true,out var reverse,out _),Is.True);
            Assert.That(reverse.Where(p=>p.Time==1).Select(p=>(p.At,p.Cycle)),Is.EqualTo(new[]{(0f,1),(1f,0)}));
            Assert.That(reverse.Last().Time,Is.EqualTo(.99f));
        }
        [Test]public void FractionalLoopSeamsFinishAtTheExactRequestedPoseInBothDirections(){
            var timeline=new ImportedMotionTimeline(new[]{0f,.1f});
            foreach(float from in new[]{.29f,.31f}){
                Assert.That(timeline.Interval(from,.3f,.1f,true,out var points,out var error),Is.True,error);
                Assert.That(points.Last().At,Is.EqualTo(.3f%.1f));Assert.That(points.Last().Cycle,Is.EqualTo((int)Math.Floor(.3f/.1f)));Assert.That(points.Last().Time,Is.EqualTo(.3f));
            }
        }
        [Test]public void HugeGapsTinyLoopsAndInvalidTimesReturnNoPartialPath(){
            var timeline=new ImportedMotionTimeline(new[]{0f,1f});
            foreach(var pair in new[]{(0f,3f,1f),(0f,.1f,.000001f),(float.NaN,1f,1f),(0f,float.PositiveInfinity,1f)}){
                Assert.That(timeline.Interval(pair.Item1,pair.Item2,pair.Item3,true,out var points,out var error),Is.False);Assert.That(points,Is.Null);Assert.That(error,Does.Contain("budget"));
            }
        }
        [Test]public void HeldFinalPoseAndInitialPoseHaveOneStableSample(){
            var timeline=new ImportedMotionTimeline(new[]{0f,1f});
            Assert.That(timeline.Interval(0,0,1,false,out var initial,out _),Is.True);Assert.That(initial.Length,Is.EqualTo(1));Assert.That(initial[0].At,Is.Zero);
            Assert.That(timeline.Interval(2,3,1,false,out var held,out _),Is.True);Assert.That(held.Length,Is.EqualTo(1));Assert.That(held[0].At,Is.EqualTo(1));
        }
    }
}
