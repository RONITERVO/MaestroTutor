// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class LiquidPathPartitionTests {
  static LiquidIntervals Spans(params float[] edges){var result=new LiquidIntervals();for(int i=0;i<edges.Length;i+=2)result.Add(edges[i],edges[i+1]);return result;}
  [Test]
  public void OverlapIsCountedOnceAndUnoccupiedGapsStayDry(){
   var p=new LiquidPathPartition();p.Begin(2);p.Add(1,Spans(.1f,.3f,.7f,.9f));p.Add(0,Spans(.2f,.4f));
   Assert.That(p.Finish(),Is.True);Assert.That(p.Fraction(0),Is.EqualTo(.2).Within(.000001));Assert.That(p.Fraction(1),Is.EqualTo(.3).Within(.000001));
  }
  [Test]
  public void InnerPriorityKeepsItsOwnIntervalsEvenWhenPresentationIsDisabled(){
   var p=new LiquidPathPartition();p.Begin(3);p.Add(2,Spans(0,1));p.Add(0,Spans(.4f,.6f));p.Add(1,Spans(.2f,.8f));
   Assert.That(p.Finish(),Is.True);
   Assert.That(p.Fraction(0),Is.EqualTo(.2).Within(.000001));Assert.That(p.Fraction(1),Is.EqualTo(.4).Within(.000001));Assert.That(p.Fraction(2),Is.EqualTo(.4).Within(.000001));
  }
  [Test]
  public void TouchingBoundariesMergeAndEqualOwnerEdgesStayIndependent(){
   var water=Spans(0,.2f,.2f,.4f);Assert.That(water.Count,Is.EqualTo(1));
   var p=new LiquidPathPartition();p.Begin(2);p.Add(0,water);p.Add(1,Spans(.4f,1));Assert.That(p.Finish(),Is.True);
   Assert.That(p.Fraction(0),Is.EqualTo(.4).Within(.000001));Assert.That(p.Fraction(1),Is.EqualTo(.6).Within(.000001));
  }
  [Test]
  public void MissingDuplicateInvalidOrOverflowInputsNeverExposePartialWater(){
   var p=new LiquidPathPartition();p.Begin(2);p.Add(0,Spans(0,1));Assert.That(p.Complete,Is.False);Assert.That(p.Finish(),Is.False);Assert.That(p.Fraction(0),Is.Zero);
   p.Begin(1);p.Add(0,Spans(0,1));p.Add(0,Spans(0,1));Assert.That(p.Finish(),Is.False);
   p.Begin(1);p.Add(0,Spans(float.NaN,1));Assert.That(p.Finish(),Is.False);
   var tooMany=new LiquidIntervals();for(int i=0;i<=LiquidIntervals.Maximum;i++)tooMany.Add(i/100f,(i+.5f)/100f);
   Assert.That(tooMany.Complete,Is.False);p.Begin(1);p.Add(0,tooMany);Assert.That(p.Finish(),Is.False);
   p.Begin(17);Assert.That(p.Finish(),Is.False);p.Begin(-1);Assert.That(p.Finish(),Is.False);
  }
  [Test]
  public void FinalizationCannotDoubleCountAndReuseCannotLeakPreviousFractions(){
   var p=new LiquidPathPartition();p.Begin(1);p.Add(0,Spans(0,1));Assert.That(p.Finish(),Is.True);Assert.That(p.Finish(),Is.True);Assert.That(p.Fraction(0),Is.EqualTo(1));
   p.Add(0,Spans(0,1));Assert.That(p.Complete,Is.False);Assert.That(p.Finish(),Is.False);
   p.Begin(1);p.Add(0,Spans());Assert.That(p.Finish(),Is.True);Assert.That(p.Fraction(0),Is.Zero);
   p.Begin(0);Assert.That(p.Finish(),Is.True);
  }
  [Test]
  public void RandomPartitionAgreesWithIndependentSampledPriorityUnion(){
   var random=new Random(49173);
   for(int iteration=0;iteration<40;iteration++){
    int owners=1+random.Next(16);var all=new LiquidIntervals[owners];var p=new LiquidPathPartition();p.Begin(owners);
    for(int owner=0;owner<owners;owner++){
     var edges=new float[16];for(int i=0;i<edges.Length;i++)edges[i]=(float)random.NextDouble();Array.Sort(edges);
     all[owner]=Spans(edges);p.Add(owner,all[owner]);
    }
    Assert.That(p.Finish(),Is.True);const int samples=16384;var observed=new int[owners];
    for(int sample=0;sample<samples;sample++){
     float t=(sample+.5f)/samples;bool found=false;
     for(int owner=0;owner<owners&&!found;owner++)for(int j=0;j<all[owner].Count;j++)if(t>=all[owner][j].x&&t<=all[owner][j].y){observed[owner]++;found=true;break;}
    }
    for(int owner=0;owner<owners;owner++)Assert.That(p.Fraction(owner),Is.EqualTo((double)observed[owner]/samples).Within(.001));
   }
  }
 }
}
