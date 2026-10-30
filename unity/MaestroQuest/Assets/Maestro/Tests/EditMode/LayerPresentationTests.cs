// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class LayerPresentationTests
    {
        [Test]public void SmoothBlendComposesAuthoredAlphaAndHasStableGuardBetweenFrames(){
            var p=new LayerPresentation();p.Synchronize(1,.4f,false);p.Set(0,true,2);string id=p.StateId;var visual=p.Visual;
            p.Tick(.5f);Assert.That(p.CurrentOpacity,Is.EqualTo(.84375f).Within(.000001));Assert.That(p.Visual.Opacity,Is.EqualTo(.3375f).Within(.000001));
            p.Tick(.5f);Assert.That(p.CurrentOpacity,Is.EqualTo(.5f));Assert.That(p.Visual.RealDepth,Is.False);Assert.That(p.StateId,Is.EqualTo(id));
            p.Tick(100);Assert.That(p.Blending,Is.False);Assert.That(p.CurrentOpacity,Is.Zero);Assert.That(p.Remaining,Is.Zero);Assert.That(p.StateId,Is.EqualTo(id));Assert.That(p.Visual,Is.SameAs(visual));
        }
        [Test]public void ReplacementBeginsAtCurrentOpacityAndIdenticalRequestDoesNotRestart(){
            var p=new LayerPresentation();p.Synchronize(1,1,true);p.Set(0,true,2);p.Tick(1);string id=p.StateId;
            p.Set(0,true,2);Assert.That(p.StateId,Is.EqualTo(id));Assert.That(p.Remaining,Is.EqualTo(1));
            p.Set(1,false,2);Assert.That(p.CurrentOpacity,Is.EqualTo(.5f));Assert.That(p.Visual.RealDepth,Is.False);Assert.That(p.StateId,Is.Not.EqualTo(id));
            p.Tick(1);Assert.That(p.CurrentOpacity,Is.EqualTo(.75f));p.Set(1,true,0);Assert.That(p.CurrentOpacity,Is.EqualTo(1));Assert.That(p.Blending,Is.False);Assert.That(p.Visual.RealDepth,Is.True);
        }
        [Test]public void SavedEditResetsOnlyThatLayerAndDoesNotReplaceItsVisualIdentity(){
            var a=new LayerPresentation();var b=new LayerPresentation();a.Synchronize(1,.8f,true);b.Synchronize(1,.5f,true);a.Set(.2f,false,1);b.Set(.3f,false,0);
            var visual=a.Visual;var id=a.StateId;a.Synchronize(1,.8f,true);Assert.That(a.StateId,Is.EqualTo(id));Assert.That(a.Blending,Is.True);
            a.Synchronize(2,.6f,false);Assert.That(a.CurrentOpacity,Is.EqualTo(1));Assert.That(a.Visual.Opacity,Is.EqualTo(.6f));Assert.That(a.Visual.RealDepth,Is.False);Assert.That(a.StateId,Is.Not.EqualTo(id));Assert.That(a.Visual,Is.SameAs(visual));Assert.That(b.CurrentOpacity,Is.EqualTo(.3f));
        }
        [Test]public void RecoveryInvalidatesEvenAnAlreadyNormalViewWhenRequested(){
            var p=new LayerPresentation();string id=p.StateId;Assert.That(p.Reset(),Is.False);Assert.That(p.StateId,Is.EqualTo(id));p.Reset(true);Assert.That(p.StateId,Is.Not.EqualTo(id));
            p.Synchronize(1,.5f,false);p.Set(0,false,2);p.Tick(.2f);id=p.StateId;Assert.That(p.Reset(),Is.True);p.Tick(2);Assert.That(p.Visual.Opacity,Is.EqualTo(.5f));Assert.That(p.Visual.RealDepth,Is.False);Assert.That(p.StateId,Is.Not.EqualTo(id));
        }
        [Test]public void InvalidControlsNeverChangeStateAndInvalidTicksNeverAdvance(){
            var p=new LayerPresentation();p.Set(0,false,2);string id=p.StateId;
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1,31}){
                Assert.Throws<ArgumentOutOfRangeException>(()=>p.Set(.5f,true,value));Assert.Throws<ArgumentOutOfRangeException>(()=>p.Set(value,true,1));
            }
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1,0})p.Tick(value);
            Assert.That(p.StateId,Is.EqualTo(id));Assert.That(p.CurrentOpacity,Is.EqualTo(1));Assert.That(p.Remaining,Is.EqualTo(2));
        }
    }
}
