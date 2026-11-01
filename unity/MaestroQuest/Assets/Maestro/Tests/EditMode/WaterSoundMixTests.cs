// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class WaterSoundMixTests {
  [Test]public void NestedDisabledWaterKeepsItsIntervalsAndSoundUsesPhysicalMetres(){
   var go=new GameObject("Finite audio geometry");try{
    var item=go.AddComponent<RoomItem>();var outer=new RoomContainer{radius=2,height=1,capacityMl=1000,amountMl=1000,waterAudio=new(){enabled=true,attenuationDbPerMetre=6,mufflingPerMetre=2}};
    var inner=new RoomContainer{radius=1,height=1,capacityMl=1000,amountMl=1000};
    var path=new LiquidPath();Assert.IsTrue(path.Measure(new[]{new LiquidMediumGeometry("outer",item,outer,Vector3.up,null),new LiquidMediumGeometry("inner",item,inner,Vector3.up,null)},2,new(-3,.5f,0),new(3,.5f,0)));
    Assert.AreEqual(4,path.WetMetres,.00001);Assert.AreEqual(2,path.MetresIn("outer"),.00001);
    var mix=WaterSoundMix.From(path);Assert.That(mix.Gain,Is.EqualTo(Math.Pow(10,-12.0/20)).Within(.000001));Assert.That(mix.Pole,Is.EqualTo(1-Math.Exp(-4)).Within(.000001));
    outer.waterAudio.attenuationDbPerMetre=24;outer.waterAudio.mufflingPerMetre=8;inner.waterAudio=outer.waterAudio.Copy();
    Assert.IsTrue(path.Measure(new[]{new LiquidMediumGeometry("outer",item,outer,Vector3.up,null),new LiquidMediumGeometry("inner",item,inner,Vector3.up,null)},2,new(-3,.5f,0),new(3,.5f,0)));
    mix=WaterSoundMix.From(path);Assert.That(mix.Gain,Is.EqualTo(Math.Pow(10,-48.0/20)).Within(.000001));Assert.That(mix.Pole,Is.EqualTo(Math.Exp(-2*Math.PI*120/48000)).Within(.000001));
    path.Clear();Assert.IsFalse(WaterSoundMix.From(path).Applied);Assert.IsFalse(WaterSoundMix.From(null).Applied);
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
