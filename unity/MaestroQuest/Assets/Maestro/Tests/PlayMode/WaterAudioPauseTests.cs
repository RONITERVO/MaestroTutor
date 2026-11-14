// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class WaterAudioPauseTests {
  [TestCase(false)]
  [TestCase(true)]
  public void PausingWithoutCallbacksClearsHistoryButRepeatedResumeDoesNot(bool transition){
   var parent=new GameObject("Owned pause test");
   PcmOutputLease output=null;bool listenerPaused=AudioListener.pause;
   AudioListener.pause=true; // Freeze engine callbacks while this fixture drives its own clock.
   try{
    var stream=new SpeechPcmStream(0,.05);
    output=PcmOutputLease.Open(parent.transform,"Paused wet PCM",stream,true,1,0,1,15);
    // Do not start the engine source: this test owns its callback clock.
    var filter=output.Source.GetComponent<SpeechPcmFilter>();filter.SetWater(new WaterSoundMix(.5f,.98f),1000);
    Assert.IsTrue(stream.TryWrite(1,Enumerable.Repeat((short)16384,2400).ToArray(),0,out var error),error);
    var signal=new float[4800];filter.Render(signal,1,48000,0);Assert.AreEqual(.25f,signal[4700],.00001);
    if(transition)output.SetPaused(true);
    output.SetPaused(false); // No render occurred during the pause.
    Assert.IsTrue(stream.TryWrite(2,new short[480],10,out error),error);Assert.IsTrue(stream.TryComplete(2,out error),error);
    var resumed=new float[960];filter.Render(resumed,1,48000,10);
    if(transition)Assert.That(resumed,Is.All.EqualTo(0),"A paused source must not replay its old filter history");
    else Assert.Greater(resumed[0],.2f,"Idempotent Resume must retain uninterrupted filter state");
    Assert.IsTrue(output.IsDrained(10.1));Assert.AreEqual(2880,stream.Read(10.1).Played);Assert.AreEqual(1,output.Source.volume);
   }finally{output?.Dispose();UnityEngine.Object.DestroyImmediate(parent);AudioListener.pause=listenerPaused;}
  }
 }
}
