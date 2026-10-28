// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed class LiquidSegmentTests {
  static RoomHeightField Bed(int cells,float height=.1f){
   var values=new float[(cells+1)*(cells+1)];Array.Fill(values,height);
   return new RoomHeightField{cells=cells,width=4,depth=4,maxHeight=.5f,heights=values};
  }
  static void Span(TerrainLiquidGeometry g,Vector3 from,Vector3 to,Vector3 up,float level,double wet,float enter,float exit){
   Assert.That(g.MeasureSegment(from,to,up,level,out var path),Is.True);
   Assert.That(path.WetFraction,Is.EqualTo(wet).Within(.000002));
   Assert.That(path.Enter,Is.EqualTo(enter).Within(.000002));Assert.That(path.Exit,Is.EqualTo(exit).Within(.000002));
   Assert.That(g.MeasureSegment(to,from,up,level,out var reversed),Is.True);
   Assert.That(reversed.WetFraction,Is.EqualTo(path.WetFraction).Within(.000002));
   Assert.That(reversed.Enter,Is.EqualTo(1-path.Exit).Within(.000002));Assert.That(reversed.Exit,Is.EqualTo(1-path.Enter).Within(.000002));
  }
  [TestCase(4)][TestCase(8)][TestCase(16)]
  public void LongDiagonalUsesOnlyCrossedCellsAndDoesNotCountSharedTrianglesTwice(int cells){
   var g=new TerrainLiquidGeometry(Bed(cells),1);
   var from=new Vector3(-3,.25f,-3);var to=new Vector3(3,.25f,3);
   Span(g,from,to,Vector3.up,.6f,2.0/3,1f/6,5f/6);
   Assert.That(g.MeasureSegment(from,to,Vector3.up,.6f,out var path),Is.True);
   Assert.That(path.Cells,Is.LessThanOrEqualTo(2*cells+3));
   Assert.That(path.Cells,Is.LessThan(cells*cells),"A diagonal must not scan the enclosing rectangle");
  }
  [TestCase(4)][TestCase(8)][TestCase(16)]
  public void AxisAndExactGridLineTraversalRemainFiniteInBothDirections(int cells){
   var g=new TerrainLiquidGeometry(Bed(cells),1);
   for(int edge=0;edge<=cells;edge++){
    float at=(float)edge/cells*4-2;
    Span(g,new Vector3(-3,.25f,at),new Vector3(3,.25f,at),Vector3.up,.6f,2.0/3,1f/6,5f/6);
    Span(g,new Vector3(at,.25f,-3),new Vector3(at,.25f,3),Vector3.up,.6f,2.0/3,1f/6,5f/6);
   }
  }
  [Test]
  public void VerticalRayClipsToTheActualFloorAndMeasuredFreeSurface(){
   var g=new TerrainLiquidGeometry(Bed(16,.2f),1);
   Span(g,new Vector3(0,1,0),new Vector3(0,-1,0),Vector3.up,.6f,.2,.2f,.4f);
   Assert.That(g.MeasureSegment(new Vector3(0,.3f,0),new Vector3(0,.3f,0),Vector3.up,.6f,out var point),Is.True);
   Assert.That(point.Cells,Is.EqualTo(1));Assert.That(point.WetFraction,Is.EqualTo(1));
  }
  [Test]
  public void DryRidgeSeparatesTwoWetSpansInsteadOfFillingTheirEnvelope(){
   var bed=Bed(4,0);
   for(int z=0;z<=4;z++)bed.heights[z*5+2]=.5f;
   var g=new TerrainLiquidGeometry(bed,1);
   Span(g,new Vector3(-3,.25f,0),new Vector3(3,.25f,0),Vector3.up,.4f,.5,1f/6,5f/6);
   Assert.That(g.FirstWetEntry(new Vector3(3,.25f,0),new Vector3(-3,.25f,0),Vector3.up,.4f,out float first),Is.True);
   Assert.That(first,Is.EqualTo(1f/6).Within(.000002));
  }
  [Test]
  public void SlopingBedAndTiltedWaterPlaneUseTheirActualIntersections(){
   var bed=Bed(4,0);
   for(int z=0;z<=4;z++)for(int x=0;x<=4;x++)bed.heights[z*5+x]=x*.125f;
   var g=new TerrainLiquidGeometry(bed,1);
   Span(g,new Vector3(-3,.25f,0),new Vector3(3,.25f,0),Vector3.up,.6f,1.0/3,1f/6,.5f);
   g=new TerrainLiquidGeometry(Bed(4),1);
   Span(g,new Vector3(-3,.25f,0),new Vector3(3,.25f,0),new Vector3(1,1,0).normalized,0,1.75/6,1f/6,2.75f/6);
  }
  [Test]
  public void DryOutsideAndInvalidSegmentsDoNotFabricateWetDistance(){
   var g=new TerrainLiquidGeometry(Bed(16),1);
   Assert.That(g.MeasureSegment(new Vector3(-3,.7f,0),new Vector3(3,.7f,0),Vector3.up,.6f,out _),Is.False);
   Assert.That(g.MeasureSegment(new Vector3(-3,.05f,0),new Vector3(3,.05f,0),Vector3.up,.6f,out _),Is.False);
   Assert.That(g.MeasureSegment(new Vector3(-3,.3f,3),new Vector3(3,.3f,3),Vector3.up,.6f,out _),Is.False);
   Assert.That(g.MeasureSegment(new Vector3(float.NaN,0,0),Vector3.zero,Vector3.up,.6f,out _),Is.False);
   Assert.That(g.MeasureSegment(Vector3.zero,Vector3.one,Vector3.zero,.6f,out _),Is.False);
   Assert.That(g.MeasureSegment(Vector3.zero,Vector3.one,Vector3.up,float.PositiveInfinity,out _),Is.False);
  }
  [TestCase(4,.13f,3.7f)][TestCase(8,2.3f,.17f)][TestCase(16,4,4)][TestCase(16,3.7f,1.3f)]
  public void IrregularTerrainDistancesAgreeWithIndependentPointSampling(int cells,float width,float depth){
   var random=new System.Random(84211);var bed=Bed(cells);bed.width=width;bed.depth=depth;
   for(int i=0;i<bed.heights.Length;i++)bed.heights[i]=(float)random.NextDouble()*.5f;
   var g=new TerrainLiquidGeometry(bed,1);const int samples=8192;
   for(int ray=0;ray<64;ray++){
    var from=new Vector3(-width*.625f,(float)random.NextDouble()*.65f,((float)random.NextDouble()*1.25f-.625f)*depth);
    var to=new Vector3(width*.625f,(float)random.NextDouble()*.65f,((float)random.NextDouble()*1.25f-.625f)*depth);
    var up=(ray%2==0?Vector3.up:new Vector3(.12f,1,-.08f)).normalized;const float level=.45f;
    int wet=0;
    for(int i=0;i<samples;i++){
     var point=Vector3.LerpUnclamped(from,to,(i+.5f)/samples);
     if(Mathf.Abs(point.x)<=width*.5f&&Mathf.Abs(point.z)<=depth*.5f&&point.y>=bed.HeightAt(new Vector2(point.x,point.z))&&point.y<=1&&Vector3.Dot(point,up)<=level)wet++;
    }
    bool hit=g.MeasureSegment(from,to,up,level,out var span);
    bool reverseHit=g.MeasureSegment(to,from,up,level,out var reversed);
    Assert.That(reverseHit,Is.EqualTo(hit),"Reverse ray "+ray);
    Assert.That(hit?span.WetFraction:0,Is.EqualTo((double)wet/samples).Within(.005),"Independent sampled wet distance, ray "+ray);
    if(hit){
     Assert.That(span.Cells,Is.LessThanOrEqualTo(2*cells+3));
     Assert.That(reversed.WetFraction,Is.EqualTo(span.WetFraction).Within(.00002),"Direction must not change water length, ray "+ray);
    }
   }
  }
  [TestCase(false,1,0)][TestCase(true,1,0)][TestCase(false,2,0)][TestCase(true,2,0)]
  [TestCase(false,1,35)][TestCase(true,1,35)][TestCase(false,2,35)][TestCase(true,2,35)]
  public void FiniteCircularAndRectangularSpansRespectScaleRotationAndQuantity(bool rectangle,float scale,float tilt){
   var go=new GameObject("Shared finite liquid ray");try{
    var item=go.AddComponent<RoomItem>();go.transform.SetPositionAndRotation(new Vector3(4,1,2),Quaternion.Euler(0,27,tilt));go.transform.localScale=Vector3.one*scale;
    var contents=new RoomContainer{height=1,radius=.5f,capacityMl=1000,amountMl=500};
    if(rectangle){contents.version=2;contents.rectangle=new(){width=1,depth=1};}
    var g=new LiquidMediumGeometry("pool",item,contents,Vector3.up,null);
    var from=go.transform.TransformPoint(new Vector3(-2,.25f,.3f));var to=go.transform.TransformPoint(new Vector3(2,.25f,.3f));
    Assert.That(g.MeasureSegment(from,to,out var span),Is.True);
    double halfWidth=rectangle?.5:.4;
    double clippedEnd=tilt==0?halfWidth:Math.Min(halfWidth,.25/Math.Tan(tilt*Math.PI/180));
    Assert.That(span.WetFraction*(to-from).magnitude,Is.EqualTo((halfWidth+clippedEnd)*scale).Within(.00001));
    Assert.That(g.MeasureSegment(to,from,out var reverse),Is.True);Assert.That(reverse.WetFraction,Is.EqualTo(span.WetFraction).Within(.000002));
    contents.amountMl=0;g=new LiquidMediumGeometry("pool",item,contents,Vector3.up,null);
    Assert.That(g.MeasureSegment(from,to,out _),Is.False);
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
