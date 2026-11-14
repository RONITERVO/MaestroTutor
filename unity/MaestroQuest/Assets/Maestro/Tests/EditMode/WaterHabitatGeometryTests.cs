// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class WaterHabitatGeometryTests {
        static RoomContainer Container(bool cylinder=false)=>cylinder?new(){radius=1,height=2,capacityMl=100,amountMl=75}:new(){version=2,rectangle=new(){width=2,depth=2},height=2,capacityMl=100,amountMl=75};
        static RoomHeightField Field(Func<float,float,float> height){
            var field=new RoomHeightField{cells=4,width=2,depth=2,maxHeight=.5f,heights=new float[25]};
            for(int z=0;z<=4;z++)for(int x=0;x<=4;x++)field.heights[z*5+x]=height(x*.5f-1,z*.5f-1);return field;
        }
        [Test]public void HabitatContainsWholeTranslatedBoxAndRejectsWetCentresWithDryEdges(){
            var go=new GameObject("Habitat");
            try{
                var item=go.AddComponent<RoomItem>();var contents=Container();var water=new LiquidMediumGeometry("water",item,contents,Vector3.up,null);
                Assert.That(water.ContainsBoxSweep(new(-.5f,.5f,0),new(.5f,1,0),Vector3.one*.1f,out var error),Is.True,error);
                var high=new Vector3(0,1.45f,0);Assert.That(water.Sample(high,out _),Is.True);
                Assert.That(water.ContainsBoxSweep(high,high,Vector3.one*.1f,out error),Is.False);Assert.That(error,Does.Contain("surface"));
                var edge=new Vector3(.95f,.5f,0);Assert.That(water.Sample(edge,out _),Is.True);
                Assert.That(water.ContainsBoxSweep(edge,edge,Vector3.one*.1f,out error),Is.False);Assert.That(error,Does.Contain("cavity"));
                Assert.That(water.ContainsBoxSweep(new(0,.05f,0),new(0,.5f,0),Vector3.one*.1f,out _),Is.False);
                contents.amountMl=0;water=new("water",item,contents,Vector3.up,null);
                Assert.That(water.ContainsBoxSweep(new(0,.5f,0),new(0,.7f,0),Vector3.one*.1f,out error),Is.False);Assert.That(error,Does.Contain("no liquid"));
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void CylindricalHabitatChecksCornersInsteadOfOnlyAxisFaces(){
            var go=new GameObject("Cylinder habitat");
            try{
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),Container(true),Vector3.up,null);var point=new Vector3(0,.5f,0);
                Assert.That(water.Sample(point,out _),Is.True);
                Assert.That(water.ContainsBoxSweep(point,point,new(.75f,.1f,.75f),out _),Is.False,"The four side centres fit, but corners are outside the circle");
                Assert.That(water.ContainsBoxSweep(point,point,new(.6f,.1f,.6f),out var error),Is.True,error);
                Assert.That(water.ContainsBoxSweep(point,new(.6f,.5f,.6f),new(.2f,.1f,.2f),out _),Is.False);
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [TestCase(.5f)][TestCase(2f)]public void TiltedScaledHabitatKeepsGravityLevelAndContainerFrame(float scale){
            var go=new GameObject("Transformed habitat");
            try{
                go.transform.SetPositionAndRotation(new(3,-2,1),Quaternion.Euler(15,30,10));go.transform.localScale=Vector3.one*scale;
                var contents=Container();contents.frame.position=new(.1f,.2f,0);contents.frame.rotation=Quaternion.Euler(0,20,0);
                Vector3 Point(Vector3 p)=>go.transform.TransformPoint(contents.frame.position+contents.frame.rotation*p);
                var item=go.AddComponent<RoomItem>();var water=new LiquidMediumGeometry("water",item,contents,Vector3.up,null);
                var from=Point(new(-.1f,.5f,0));var to=Point(new(.1f,.6f,0));var extents=Vector3.one*(.025f*scale);
                Assert.That(water.ContainsBoxSweep(from,to,extents,out var error),Is.True,error);
                contents.amountMl=5;water=new("water",item,contents,Vector3.up,null);
                Assert.That(water.ContainsBoxSweep(from,to,extents,out _),Is.False,"Fresh queries must use changed live quantity");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void InvalidOrDegenerateEnvelopesNeverProveHabitatAdmission(){
            var go=new GameObject("Invalid habitat query");
            try{
                var water=new LiquidMediumGeometry("water",go.AddComponent<RoomItem>(),Container(),Vector3.up,null);var point=new Vector3(0,.5f,0);
                Assert.That(water.ContainsBoxSweep(new(float.NaN,0,0),point,Vector3.one*.1f,out _),Is.False);
                Assert.That(water.ContainsBoxSweep(point,point,Vector3.zero,out _),Is.False);
                Assert.That(water.ContainsBoxSweep(point,point,new(.1f,-.1f,.1f),out _),Is.False);
                Assert.That(default(LiquidMediumGeometry).ContainsBoxSweep(point,point,Vector3.one*.1f,out _),Is.False);
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]public void SweptHabitatCannotSkipTerrainBetweenTwoClearWetEndpoints(){
            var field=Field((x,_)=>Mathf.Abs(x)<.01f?.4f:0);var terrain=new TerrainLiquidGeometry(field,.5f);
            var from=new Vector3(-.8f,.2f,0);var to=new Vector3(.8f,.2f,0);var extents=Vector3.one*.02f;
            Assert.That(terrain.Contains(from)&&terrain.Contains(to),Is.True);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,from,from,extents),Is.False);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,to,to,extents),Is.False);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,from,to,extents),Is.True);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,from+Vector3.up*.25f,to+Vector3.up*.25f,extents),Is.False);
        }
        [Test]public void TerrainChecksRealTrianglesWithoutTurningAClearCornerIntoARidge(){
            var field=Field((x,z)=>Mathf.Abs(x)<.01f&&Mathf.Abs(z)<.01f?.4f:0);var terrain=new TerrainLiquidGeometry(field,.5f);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,new(.4f,.15f,.4f),new(.45f,.15f,.4f),Vector3.one*.01f),Is.False,"The same cell contains a high vertex, but these actual triangles are clear here");
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,new(.1f,.15f,.1f),new(.15f,.15f,.1f),Vector3.one*.01f),Is.True);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,new(.4f,.2f,.4f),new(.45f,.2f,.4f),new(.4f,.02f,.4f)),Is.True,"A clear centre is not a clear whole body");
        }
        [TestCase(.5f)][TestCase(2f)]public void SweptTerrainAdmissionUsesTheSameRotatedScaledFrame(float scale){
            var terrain=new TerrainLiquidGeometry(Field((x,_)=>Mathf.Abs(x)<.01f?.4f:0),.5f);
            var mapping=Matrix4x4.TRS(new(3,-2,1),Quaternion.Euler(15,30,10),Vector3.one*scale);
            Assert.That(terrain.IntersectsSweptBox(mapping,mapping.MultiplyPoint3x4(new(-.8f,.2f,0)),mapping.MultiplyPoint3x4(new(.8f,.2f,0)),Vector3.one*(.01f*scale)),Is.True);
            Assert.That(terrain.IntersectsSweptBox(mapping,mapping.MultiplyPoint3x4(new(-.8f,.46f,0)),mapping.MultiplyPoint3x4(new(.8f,.46f,0)),Vector3.one*(.01f*scale)),Is.False);
        }
        [Test]public void BodyBelowTheBedIsBlockedEvenWithoutCrossingItsTop(){
            var terrain=new TerrainLiquidGeometry(Field((_,__)=>.3f),.5f);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,new(-.5f,.1f,0),new(.5f,.1f,0),Vector3.one*.02f),Is.True);
            Assert.That(terrain.IntersectsSweptBox(Matrix4x4.identity,new(-.5f,.32f,0),new(.5f,.32f,0),Vector3.one*.02f),Is.False,"Touching the bed is not penetration");
        }
    }
}
