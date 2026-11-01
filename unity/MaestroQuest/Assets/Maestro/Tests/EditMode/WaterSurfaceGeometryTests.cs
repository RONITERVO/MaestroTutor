// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class WaterSurfaceGeometryTests {
        static RoomContainer Contents()=>new(){version=2,rectangle=new(){width=2,depth=2},height=2,capacityMl=100,amountMl=75};
        [Test]public void SurfaceAdmissionClipsAirAboveTheRimButRetainsTheWetBody(){
            var go=new GameObject("Surface habitat");
            try{
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),Contents(),Vector3.up,null);
                var from=new Vector3(-.4f,1.6f,0);var to=new Vector3(.4f,1.6f,0);var extents=new Vector3(.2f,.7f,.2f);
                Assert.That(water.ContainsBoxSweep(from,to,extents,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(from,to,extents,out var error),Is.True,error);
                Assert.That(water.ContainsSurfaceBoxSweep(from,new(.9f,1.6f,0),extents,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(from,new(.4f,2.3f,0),extents,out _),Is.False,"No dry-gap travel");
            }finally{Object.DestroyImmediate(go);}
        }
        [Test]public void SurfaceClipChecksNewPlaneIntersectionsNotJustTheWetCorners(){
            var go=new GameObject("Oblique surface");
            try{
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),Contents(),new Vector3(1,1,0).normalized,null);
                float bottom=water.Level*Mathf.Sqrt(2)-1.1f;Assert.That(bottom,Is.InRange(0,.9f));
                var point=new Vector3(1,bottom+.5f,0);var extents=new Vector3(.5f,.5f,.1f);
                // Only the x=.5 lower corners are wet and they fit. The cut edge
                // crosses the free surface at x=1.1, outside the cavity wall x=1.
                Assert.That(water.Sample(new(.5f,bottom,0),out _),Is.True);
                Assert.That(water.ContainsSurfaceBoxSweep(point,point,extents,out var error),Is.False);
                Assert.That(error,Does.Contain("immersed"));
            }finally{Object.DestroyImmediate(go);}
        }
        [Test]public void SurfacePositionUsesLiveQuantityAndCollisionOffsetInsteadOfModelPivot(){
            var go=new GameObject("Live surface");
            try{
                var item=go.AddComponent<RoomItem>();var c=Contents();var water=new LiquidMediumGeometry("water",item,c,Vector3.up,null);
                Assert.That(water.SurfacePosition(new(.3f,100,.4f),new(0,.6f,0),new(.2f,.5f,.2f),.6f,out var p,out var error),Is.True,error);
                Assert.That(p.x,Is.EqualTo(.3f));Assert.That(p.z,Is.EqualTo(.4f));Assert.That(p.y,Is.EqualTo(.8f).Within(.0001f));
                c.amountMl=90;water=new("water",item,c,Vector3.up,null);
                Assert.That(water.SurfacePosition(p,new(0,.6f,0),new(.2f,.5f,.2f),.6f,out var raised,out error),Is.True,error);
                Assert.That(raised.y-p.y,Is.EqualTo(.3f).Within(.0001f));
                c.amountMl=0;water=new("water",item,c,Vector3.up,null);
                Assert.That(water.SurfacePosition(p,Vector3.zero,Vector3.one,.6f,out _,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(p,p,Vector3.one*.1f,out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test]public void SurfaceAdmissionRejectsDegenerateBodyAndPlacementRejectsNonverticalGravity(){
            var go=new GameObject("Invalid surface");
            try{
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),Contents(),new Vector3(1,1,0).normalized,null);
                Assert.That(water.SurfacePosition(Vector3.zero,Vector3.zero,Vector3.one,.6f,out _,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(Vector3.zero,Vector3.zero,Vector3.zero,out _),Is.False);
                Assert.That(water.ContainsSurfaceBoxSweep(new(float.NaN,0,0),Vector3.zero,Vector3.one,out _),Is.False);
                Assert.That(default(LiquidMediumGeometry).ContainsSurfaceBoxSweep(Vector3.zero,Vector3.zero,Vector3.one,out _),Is.False);
            }finally{Object.DestroyImmediate(go);}
        }
        [Test]public void SurfacePlannerDetoursInPlaneAndYawPreservesTilt(){
            bool Clear(Pose a,Pose b,out string error){
                error=null;if(Mathf.Abs(a.position.y-2)>.0001f||Mathf.Abs(b.position.y-2)>.0001f)return false;
                var obstacle=new Bounds(new(.7f,2,0),new(.2f,3,.5f));var d=b.position-a.position;
                return !obstacle.Contains(a.position)&&!obstacle.Contains(b.position)&&(d.sqrMagnitude<1e-10f||!obstacle.IntersectRay(new Ray(a.position,d.normalized),out float t)||t>d.magnitude);
            }
            var tilt=Quaternion.Euler(20,0,15);var planner=new RoomAquaticRoute(new(new(0,2,0),tilt),new(1.4f,2,0),true,Clear,2);
            for(int i=0;i<600&&!planner.Done;i++)planner.Tick();
            Assert.That(planner.Path,Is.Not.Null,planner.Error);bool detour=false;
            for(int i=0;i<planner.Path.Length;i++){
                var p=planner.Path[i];Assert.That(p.position.y,Is.EqualTo(2).Within(.0001f));detour|=Mathf.Abs(p.position.z)>.3f;
                Assert.That((p.rotation*Vector3.up).y,Is.EqualTo((tilt*Vector3.up).y).Within(.0001f));
                if(i>0)Assert.That(Clear(planner.Path[i-1],p,out _),Is.True);
            }
            Assert.That(detour,Is.True);
        }
    }
}
