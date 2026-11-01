// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
 public sealed class WaterPcmTreatmentTests {
  [Test]
  public void DisabledOutputIsBitExactAndChannelsDoNotBorrowHistory(){
   var p=new WaterPcmTreatment();var data=new float[4096];var random=new Random(11);for(int i=0;i<data.Length;i++)data[i]=(float)random.NextDouble()*2-1;
   var original=(float[])data.Clone();p.Process(data,2,48000,0);Assert.That(data,Is.EqualTo(original));
   p.Reset();p.Publish(new WaterSoundMix(.5f,.85f),10);
   Array.Clear(data,0,data.Length);for(int i=0;i<data.Length;i+=2)data[i]=1;
   p.Process(data,2,48000,.1);for(int i=1;i<data.Length;i+=2)Assert.That(data[i],Is.EqualTo(0).Within(.0000001),"No left-to-right leakage");
   Assert.That(data[^2],Is.EqualTo(.5f).Within(.00001));
  }
  static double ToneRms(int rate,int frequency,float pole){
   var p=new WaterPcmTreatment();p.Publish(new WaterSoundMix(.5f,pole),10);
   var samples=new float[rate/4];for(int i=0;i<samples.Length;i++)samples[i]=(float)Math.Sin(2*Math.PI*frequency*i/rate);
   p.Process(samples,1,rate,0);double sum=0;int start=rate/10;
   for(int i=start;i<samples.Length;i++)sum+=(double)samples[i]*samples[i];
   return Math.Sqrt(sum/(samples.Length-start));
  }
  [TestCase(24000)][TestCase(48000)][TestCase(96000)]
  public void TreatmentLowersHighFrequenciesAndRetainsBoundedLowFrequencyGainAcrossDeviceRates(int rate){
   double low=ToneRms(rate,200,.85f),high=ToneRms(rate,4000,.85f);
   Assert.That(low,Is.InRange(.32,.36));Assert.That(high,Is.LessThan(low*.4));Assert.That(high,Is.GreaterThan(.07));
  }
  [Test]
  public void ExpiredOrExplicitlyClearedTargetsRecoverDrySamplesAndClearOldTails(){
   var p=new WaterPcmTreatment();p.Publish(new WaterSoundMix(.1f,.98f),.2);
   var samples=new float[4800];Array.Fill(samples,1);p.Process(samples,1,48000,0);Assert.That(samples[^1],Is.EqualTo(.1f).Within(.00001));
   Assert.That(p.Read(.21).Applied,Is.False);Array.Fill(samples,1);p.Process(samples,1,48000,.21);Assert.That(samples[^1],Is.EqualTo(1));
   p.Publish(new WaterSoundMix(.2f,.9f),1);Array.Fill(samples,1);p.Process(samples,1,48000,.3);
   p.Reset();Array.Clear(samples,0,samples.Length);p.Process(samples,1,48000,.4);Assert.That(samples,Is.All.EqualTo(0));
   p.Publish(WaterSoundMix.Dry,double.PositiveInfinity);Array.Fill(samples,-.5f);p.Process(samples,1,48000,.5);Assert.That(samples[^1],Is.EqualTo(-.5f));
  }
  [Test]
  public void SampleRateAndChannelChangesResetHistoryAndInvalidBlocksRemainSilent(){
   var p=new WaterPcmTreatment();p.Publish(new WaterSoundMix(.5f,.95f),10);var a=new float[4096];Array.Fill(a,1);p.Process(a,1,48000,0);
   Array.Clear(a,0,a.Length);p.Process(a,2,96000,.1);Assert.That(a,Is.All.EqualTo(0));
   Array.Fill(a,1);p.Process(a,0,48000,.2);Assert.That(a,Is.All.EqualTo(0));
   Assert.Throws<ArgumentException>(()=>p.Publish(new WaterSoundMix(float.NaN,0),1));
   Assert.Throws<ArgumentException>(()=>p.Publish(new WaterSoundMix(1,1),1));
  }
  [Test]
  public void RepeatedAudioBlocksDoNotAllocate(){
   var p=new WaterPcmTreatment();var data=new float[960];p.Publish(new WaterSoundMix(.5f,.95f),10);p.Process(data,2,48000,0);
   long before=GC.GetAllocatedBytesForCurrentThread();
   for(int i=0;i<1000;i++)p.Process(data,2,48000,.01);
   Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.EqualTo(0));
  }
 }
}
