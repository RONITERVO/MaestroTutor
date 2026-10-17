// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class TerrainLiquidGeometryTests {
        static RoomHeightField Field(Func<float,float,float> height){
            var field=new RoomHeightField{cells=4,width=2,depth=1,maxHeight=.5f,heights=new float[25]};
            for(int z=0;z<=4;z++)for(int x=0;x<=4;x++)field.heights[z*5+x]=height(x*.5f-1,z*.25f-.5f);return field;
        }
        [Test]public void FlatTerrainPreservesExactCapacityLevelAndContainment(){
            var field=Field((_,__)=>.1f);var water=new TerrainLiquidGeometry(field,.5f);
            Assert.That(water.Volume,Is.EqualTo(.8).Within(1e-7));Assert.That(water.VolumeBelow(Vector3.up,.2),Is.EqualTo(.2).Within(1e-7));
            Assert.That(water.Level(Vector3.up,.25),Is.EqualTo(.2).Within(1e-6));Assert.That(water.Contains(new Vector3(0,.15f,0)),Is.True);
            Assert.That(water.Contains(new Vector3(0,.05f,0)),Is.False);Assert.That(water.Contains(new Vector3(0,.6f,0)),Is.False);Assert.That(water.Contains(new Vector3(2,.2f,0)),Is.False);
            field.heights[12]=.5f;Assert.That(water.Contains(new Vector3(0,.15f,0)),Is.True,"Prepared geometry must not alias an editable source array");
        }
        [Test]public void SlopedBedProducesTheAnalyticVolumeAndClippedShoreline(){
            var field=Field((x,_)=>.2f*(x+1));var water=new TerrainLiquidGeometry(field,.5f);
            Assert.That(water.Volume,Is.EqualTo(.6).Within(1e-7));Assert.That(water.VolumeBelow(Vector3.up,.2),Is.EqualTo(.1).Within(1e-7));
            float level=water.Level(Vector3.up,1.0/6);Assert.That(level,Is.EqualTo(.2).Within(1e-6));
            var vertices=new List<Vector3>();var indices=new List<int>();water.Surface(Vector3.up,level,vertices,indices);Assert.That(indices.Count,Is.GreaterThan(0));
            double area=0;foreach(var point in vertices){Assert.That(point.y,Is.EqualTo(level).Within(1e-6));Assert.That(point.x,Is.LessThanOrEqualTo(1e-6));}
            for(int i=0;i<indices.Count;i+=3){var normal=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]);Assert.That(normal.y,Is.GreaterThanOrEqualTo(-1e-8));area+=normal.magnitude*.5;}
            Assert.That(area,Is.EqualTo(1).Within(1e-5));
        }
        [TestCase(0f,1f,0f)][TestCase(.3f,.8f,.5f)][TestCase(1f,0f,0f)][TestCase(0f,-1f,0f)]
        public void TiltedAndInvertedFlatTerrainMatchesIndependentBoxVolume(float x,float y,float z){
            var normal=new Vector3(x,y,z).normalized;var water=new TerrainLiquidGeometry(Field((_,__)=>.1f),.5f);
            double centre=.3*normal.y;
            foreach(double level in new[]{-.5,-.15,0,.12,.5}){
                double expected=ContainerFlowGeometry.BoxFractionBelow(normal,2,.4,1,level)*.8;
                Assert.That(water.VolumeBelow(normal,level+centre),Is.EqualTo(expected).Within(2e-7));
            }
            foreach(double fraction in new[]{.01,.25,.5,.8,.99}){
                float level=water.Level(normal,fraction);double expected=ContainerFlowGeometry.BoxFractionBelow(normal,2,.4,1,level-centre);
                Assert.That(expected,Is.EqualTo(fraction).Within(2e-6));
                var vertices=new List<Vector3>();var indices=new List<int>();water.Surface(normal,level,vertices,indices);
                foreach(var point in vertices)Assert.That(Vector3.Dot(point,normal),Is.EqualTo(level).Within(1e-6));
                for(int i=0;i<indices.Count;i+=3)Assert.That(Vector3.Dot(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]),normal),Is.GreaterThanOrEqualTo(-1e-7));
            }
        }
        [Test]public void IslandVolumeAndSurfaceUseTheSameNonplanarTrianglesAsGround(){
            var field=Field((x,z)=>Mathf.Max(0,.4f*(1-Mathf.Abs(x))*(1-Mathf.Abs(z)*2)));var water=new TerrainLiquidGeometry(field,.5f);
            Assert.That(water.Volume,Is.EqualTo(1-field.VolumeLitres/1000).Within(1e-7));
            const int samples=240;double integral=0;
            for(int z=0;z<samples;z++)for(int x=0;x<samples;x++)integral+=Math.Max(0,.2-field.HeightAt(new Vector2(((x+.5f)/samples-.5f)*2,(z+.5f)/samples-.5f)));
            integral*=2.0/(samples*samples);Assert.That(water.VolumeBelow(Vector3.up,.2),Is.EqualTo(integral).Within(2e-5));
            var vertices=new List<Vector3>();var indices=new List<int>();water.Surface(Vector3.up,.2f,vertices,indices);
            for(int i=0;i<indices.Count;i+=3){var centre=(vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;Assert.That(field.HeightAt(new Vector2(centre.x,centre.z)),Is.LessThanOrEqualTo(.200001f));}
            Assert.That(water.Contains(new Vector3(0,.2f,0)),Is.False,"Water cannot occupy the dry island's solid interior");
        }
        [Test]public void TetrahedronClippingHandlesRepeatedHeightsAndBothHalfspaceOrientations(){
            foreach(double t in new[]{0,.01,.2,.5,.9,1}){
                Assert.That(TerrainLiquidGeometry.ClippedFraction(t,t-1,t,t),Is.EqualTo(1-Math.Pow(1-t,3)).Within(1e-12));
                Assert.That(TerrainLiquidGeometry.ClippedFraction(t,t-1,t-1,t),Is.EqualTo(3*t*t-2*t*t*t).Within(1e-12));
            }
            var random=new System.Random(217);
            for(int i=0;i<200;i++){
                double a=random.NextDouble()*2-1,b=random.NextDouble()*2-1,c=random.NextDouble()*2-1,d=random.NextDouble()*2-1;
                double value=TerrainLiquidGeometry.ClippedFraction(a,b,c,d);
                Assert.That(value+TerrainLiquidGeometry.ClippedFraction(-a,-b,-c,-d),Is.EqualTo(1).Within(1e-12));
                Assert.That(TerrainLiquidGeometry.ClippedFraction(d,c,b,a),Is.EqualTo(value).Within(1e-12));
            }
        }
        [Test]public void DryPlateauHasNoWaterFilmAndSweepsUseTheActualWetTriangles(){
            var field=Field((x,_)=>x>=0?.2f:0);var water=new TerrainLiquidGeometry(field,.5f);
            Assert.That(TerrainLiquidGeometry.Capacity(field,.5f),Is.EqualTo(water.Volume).Within(1e-12));
            var vertices=new List<Vector3>();var indices=new List<int>();water.Surface(Vector3.up,.2f,vertices,indices);
            for(int i=0;i<indices.Count;i+=3){var centre=(vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;Assert.That(centre.x,Is.LessThan(0));}
            bool Sweep(Vector3 a,Vector3 b,float radius,float height)=>water.Sweep(Matrix4x4.identity,a,b,radius,height,Vector3.up,.2f,Vector3.up,.2f,out _,out _);
            Assert.That(Sweep(new Vector3(-.75f,.1f,-.4f),new Vector3(-.75f,.1f,.4f),.01f,.05f),Is.True);
            Assert.That(Sweep(new Vector3(.5f,.1f,-.4f),new Vector3(.5f,.1f,.4f),.01f,.05f),Is.False,"A body entirely inside dry ground is not immersed");
            Assert.That(Sweep(new Vector3(-.75f,-.2f,0),new Vector3(-.75f,-.1f,0),.01f,.05f),Is.False,"Below-bed space is not water");
            Assert.That(Sweep(new Vector3(-2,.1f,0),new Vector3(2,.1f,0),.01f,.05f),Is.True,"A narrow wet strip cannot be skipped by a long movement");
        }
        [Test]public void InvalidOrEmptyCavitiesCannotInventCapacity(){
            Assert.Throws<ArgumentException>(()=>new TerrainLiquidGeometry(Field((_,__)=>.5f),.5f));
            Assert.Throws<ArgumentException>(()=>new TerrainLiquidGeometry(Field((_,__)=>.4f),.3f));
            var water=new TerrainLiquidGeometry(Field((_,__)=>0),.5f);Assert.Throws<ArgumentException>(()=>water.Level(Vector3.zero,.5));Assert.Throws<ArgumentException>(()=>water.Level(Vector3.up,double.NaN));
        }
    }
}
