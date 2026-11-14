// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class LiquidPathTests {
  [Test]
  public void EqualCavitiesChooseStableIdentityRegardlessOfInputOrderAndReadNoStaleResults(){
   var go=new GameObject("Path geometry");try{
    var item=go.AddComponent<RoomItem>();var contents=new RoomContainer{radius=.5f,height=1,capacityMl=1000,amountMl=1000};
    go.transform.position=new(10,2,3);go.transform.localScale=Vector3.one*2;
    var a=new LiquidMediumGeometry("a",item,contents,Vector3.up,null);var b=new LiquidMediumGeometry("b",item,contents,Vector3.up,null);
    var from=new Vector3(8,3,3);var to=new Vector3(12,3,3);var p=new LiquidPath();
    Assert.IsTrue(p.Measure(new[]{b,a},2,from,to));Assert.AreEqual("a",p.Medium(0).Id);Assert.That(p.WetMetres,Is.EqualTo(2).Within(.00001));Assert.That(p.MetresIn("a"),Is.EqualTo(2).Within(.00001));Assert.That(p.MetresIn("b"),Is.Zero);
    Assert.IsTrue(p.Measure(new[]{a,b},2,to,from));Assert.That(p.MetresIn("a"),Is.EqualTo(2).Within(.00001));
    Assert.IsFalse(p.Measure(new[]{a,a},2,from,to));Assert.IsFalse(p.Complete);Assert.AreEqual(0,p.Count);Assert.That(p.MetresIn("a"),Is.Zero);
    Assert.IsFalse(p.Measure(new[]{a},1,new(float.NaN,0,0),to));Assert.That(p.WetMetres,Is.Zero);
    Assert.IsTrue(p.Measure(System.Array.Empty<LiquidMediumGeometry>(),0,from,to));Assert.That(p.WetMetres,Is.Zero);Assert.That(p.Metres,Is.EqualTo(4));
   }finally{Object.DestroyImmediate(go);}
  }
 }
}
