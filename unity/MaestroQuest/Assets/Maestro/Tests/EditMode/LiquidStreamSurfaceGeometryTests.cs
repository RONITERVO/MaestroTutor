// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class LiquidStreamSurfaceGeometryTests {
  static RoomContainer FullCup(bool rectangle){
   var value=new RoomContainer{frame=new(){position=new Vector3(0,-.088f,0)},radius=.078f,height=.198f,capacityMl=500,amountMl=500};
   if(rectangle){value.version=2;value.rectangle=new(){width=.156f,depth=.156f};}return value;
  }
  [TestCase(false)][TestCase(true)]
  public void FullMouthOriginRoundoffStopsTheStreamOnItsMeasuredPlane(bool rectangle){
   var go=new GameObject("Full cup contact");try{
    go.transform.position=new Vector3(3,1,0);var item=go.AddComponent<RoomItem>();var cup=FullCup(rectangle);
    var geometry=new LiquidMediumGeometry("cup",item,cup,Vector3.up,null);var mouth=new Vector3(3,geometry.Level,0);
    var inverse=(go.transform.localToWorldMatrix*Matrix4x4.TRS(cup.frame.position,cup.frame.rotation,Vector3.one)).inverse;
    Assert.That(inverse.MultiplyPoint3x4(mouth).y,Is.GreaterThan(cup.height),"This full cup reproduces inverse-transform ceiling roundoff at the measured plane");
    Assert.That(geometry.TryFreeSurfaceContact(mouth,mouth-Vector3.up*.05f,out var point,out float fraction),Is.True);
    Assert.That(fraction,Is.Zero);Assert.That(point,Is.EqualTo(mouth));
    Assert.That(geometry.ContainsAtOrBelowSurface(mouth),Is.False,"Stream tolerance must not broaden ordinary point containment");
    Assert.That(geometry.Sample(mouth,out _),Is.False);Assert.That(geometry.CrossesSurface(mouth,mouth-Vector3.up*.05f,out _),Is.False);
   }finally{Object.DestroyImmediate(go);}
  }
  [TestCase(false)][TestCase(true)]
  public void NearPlaneOriginCannotProjectBeyondATiltedFiniteRim(bool rectangle){
   var go=new GameObject("Tilted rim contact");try{
    go.transform.rotation=Quaternion.Euler(0,0,35);var item=go.AddComponent<RoomItem>();
    var cup=new RoomContainer{radius=.5f,height=1,capacityMl=1000,amountMl=500};
    if(rectangle){cup.version=2;cup.rectangle=new(){width=1,depth=1};}
    var geometry=new LiquidMediumGeometry("cup",item,cup,Vector3.up,null);var normal=Quaternion.Inverse(go.transform.rotation)*Vector3.up;
    float x=-.5f-.0000002f;var outside=go.transform.TransformPoint(new Vector3(x,.5f-normal.x*x/normal.y,0));outside.y=geometry.Level;
    var from=outside+Vector3.up*.0000006f;float distance=Vector3.Dot(from,Vector3.up)-geometry.Level;
    var projected=from-Vector3.up*distance;
    Assert.That(distance,Is.GreaterThan(0).And.LessThan(.000001f));
    Assert.That(cup.ContainsHorizontal(go.transform.InverseTransformPoint(from)),Is.True,"The origin itself is just inside the tilted rim");
    Assert.That(cup.ContainsHorizontal(go.transform.InverseTransformPoint(projected)),Is.False,"Projection onto the water plane would move the returned point outside the finite cavity");
    Assert.That(geometry.TryFreeSurfaceContact(from,from+Vector3.up*.01f,out _,out _),Is.False,"Roundoff tolerance must validate the actual returned plane point");
   }finally{Object.DestroyImmediate(go);}
  }
  [TestCase(false)][TestCase(true)]
  public void PlaneOriginToleranceDoesNotExtendTheFootprintOrAcceptAnAbovePlaneSegment(bool rectangle){
   var go=new GameObject("Finite full cup contact");try{
    go.transform.position=new Vector3(3,1,0);var item=go.AddComponent<RoomItem>();var cup=FullCup(rectangle);
    var geometry=new LiquidMediumGeometry("cup",item,cup,Vector3.up,null);var mouth=new Vector3(3,geometry.Level,0);
    var outside=mouth+Vector3.right*(cup.radius+.00001f);
    Assert.That(geometry.TryFreeSurfaceContact(outside,outside-Vector3.up*.05f,out _,out _),Is.False);
    Assert.That(geometry.TryFreeSurfaceContact(mouth+Vector3.up*.001f,mouth+Vector3.up*.002f,out _,out _),Is.False);
    Assert.That(geometry.TryFreeSurfaceContact(mouth+Vector3.up*.01f,mouth-Vector3.up*.01f,out var point,out float fraction),Is.True);
    Assert.That(point.y,Is.EqualTo(geometry.Level).Within(.000001f));Assert.That(fraction,Is.EqualTo(.5f).Within(.00001f));
   }finally{Object.DestroyImmediate(go);}
  }
 }
}
