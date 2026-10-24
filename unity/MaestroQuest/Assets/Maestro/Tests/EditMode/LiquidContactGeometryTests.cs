// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;using Maestro.Quest.Interaction;using NUnit.Framework;using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class LiquidContactGeometryTests {
  [TestCase(false,0,1)][TestCase(true,0,1)][TestCase(false,35,2)][TestCase(true,35,2)]
  public void SurfaceCrossingUsesFiniteRotatedScaledCavity(bool rectangle,float tilt,float scale){
   var go=new GameObject("Contact geometry");try{var item=go.AddComponent<RoomItem>();go.transform.SetPositionAndRotation(new Vector3(4,1,0),Quaternion.Euler(0,20,tilt));go.transform.localScale=Vector3.one*scale;
    var data=new RoomContainer{height=1,radius=.5f,capacityMl=1000,amountMl=500};if(rectangle){data.version=2;data.rectangle=new(){width=1,depth=1};}
    var geometry=new LiquidMediumGeometry("pool",item,data,Vector3.up,null);var centre=go.transform.TransformPoint(Vector3.up*.5f);centre.y=geometry.Level;
    Assert.That(geometry.CrossesSurface(centre+Vector3.up*2,centre-Vector3.up*2,out var point),Is.True);Assert.That(point.y,Is.EqualTo(geometry.Level).Within(.00001));
    Assert.That(geometry.CrossesSurface(centre+Vector3.right*10+Vector3.up,centre+Vector3.right*10-Vector3.up,out _),Is.False);
    Assert.That(geometry.CrossesSurface(centre,centre-Vector3.up,out _),Is.False,"A point leaving the plane must not duplicate a crossing");
    Assert.That(geometry.CrossesSurface(centre+Vector3.up,centre,out _),Is.False,"Merely ending on the plane is not a through-crossing");
   }finally{Object.DestroyImmediate(go);}
  }
 }
}
