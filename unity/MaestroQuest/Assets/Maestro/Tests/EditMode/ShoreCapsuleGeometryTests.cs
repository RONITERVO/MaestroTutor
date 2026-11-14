// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class ShoreCapsuleGeometryTests {
        [Test]public void FloatFootUsesTheActualCapsuleBottomAndHeight(){
            var body=new RoomUprightBody(.1f,.5f);Assert.That(body.FloatFoot(1,.6f,out var foot),Is.True);
            body.Points(new Vector3(0,foot,0),out var lower,out var upper);
            float bottom=lower.y-body.Radius,top=upper.y+body.Radius;
            Assert.That((1-bottom)/(top-bottom),Is.EqualTo(.6f).Within(1e-6f));
            Assert.That(body.Bounds(new(0,foot,0)).min.y,Is.EqualTo(bottom).Within(1e-6f));
            Assert.That(new RoomUprightBody(.2f,.3f).FloatFoot(1,.6f,out _),Is.False);
            Assert.That(body.FloatFoot(float.NaN,.6f,out _),Is.False);Assert.That(body.FloatFoot(1,1,out _),Is.False);
        }
        [Test]public void WetCapRadiusUsesTheRoundedBottomInsteadOfAnExpandedBox(){
            var body=new RoomUprightBody(.4f,1.2f);
            Assert.That(body.WetRadius(0,body.Bottom),Is.Zero);
            Assert.That(body.WetRadius(0,body.Bottom+.1f),Is.EqualTo(Mathf.Sqrt(.07f)).Within(1e-6f));
            Assert.That(body.WetRadius(0,body.Bottom+.5f),Is.EqualTo(.4f));
        }
        [Test]public void CircularCavityAdmitsARealCapsuleWhereItsBoxCornersWouldNotFit(){
            var go=new GameObject("Circular capsule habitat");
            try{
                var contents=new RoomContainer{radius=1,height=2,capacityMl=100,amountMl=75};
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),contents,Vector3.up,null);
                var body=new RoomUprightBody(.29f,1.2f);Assert.That(water.SurfaceCapsuleFoot(body,.6f,out var y,out var error),Is.True,error);
                Assert.That(water.SurfaceCapsuleCavity(new(.5f,y,.5f),new(.49f,y,.49f),body,out error),Is.True,error);
                Assert.That(water.SurfaceCapsuleCavity(new(.51f,y,.51f),new(.51f,y,.51f),body,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(new(.5f,y+.62f,.5f),new(.5f,y+.62f,.5f),new(.29f,.58f,.29f),out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test]public void CavityAdmissionRejectsDryGapsMissingLiquidAndBelowCavityBodies(){
            var go=new GameObject("Finite capsule habitat");
            try{
                var contents=new RoomContainer{version=2,rectangle=new(){width=2,depth=2},height=2,capacityMl=100,amountMl=75};
                var item=go.AddComponent<RoomItem>();var water=new LiquidMediumGeometry("water",item,contents,Vector3.up,null);var body=new RoomUprightBody(.1f,.5f);
                Assert.That(water.SurfaceCapsuleCavity(new(0,1,0),new(.4f,1,0),body,out var error),Is.True,error);
                Assert.That(water.SurfaceCapsuleCavity(new(0,2,0),new(0,1,0),body,out _),Is.False);
                Assert.That(water.SurfaceCapsuleCavity(new(0,-.1f,0),new(0,1,0),body,out _),Is.False);
                Assert.That(water.SurfaceCapsuleCavity(new(0,1,0),new(1,1,0),body,out _),Is.False);
                contents.amountMl=0;water=new("water",item,contents,Vector3.up,null);
                Assert.That(water.SurfaceCapsuleCavity(new(0,1,0),new(0,1,0),body,out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
        [TestCase(.5f)][TestCase(2f)]public void UprightYawAndScaleUseTheSameLiveSurface(float scale){
            var go=new GameObject("Transformed capsule habitat");
            try{
                go.transform.SetPositionAndRotation(new(3,-2,4),Quaternion.Euler(0,31,0));go.transform.localScale=Vector3.one*scale;
                var contents=new RoomContainer{version=2,rectangle=new(){width=2,depth=2},height=2,capacityMl=100,amountMl=75};
                var item=go.AddComponent<RoomItem>();var water=new LiquidMediumGeometry("water",item,contents,Vector3.up,null);
                var body=new RoomUprightBody(.1f*scale,.5f*scale+RoomGroundQuery.Skin);
                Assert.That(water.SurfaceCapsuleFoot(body,.6f,out var y,out var error),Is.True,error);
                Assert.That(water.SurfaceCapsuleCavity(new(3,y,4),new(3+.2f*scale,y,4),body,out error),Is.True,error);
                go.transform.rotation=Quaternion.Euler(5,31,0);water=new("water",item,contents,Vector3.up,null);
                Assert.That(water.SurfaceCapsuleFoot(body,.6f,out _,out _),Is.False,"The upright motor cannot pretend a tilted vessel is upright");
                go.transform.rotation=Quaternion.identity;go.transform.localScale=new(1,2,1);water=new("water",item,contents,Vector3.up,null);
                Assert.That(water.SurfaceCapsuleFoot(body,.6f,out _,out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
