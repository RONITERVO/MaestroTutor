// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
 public sealed partial class WorldLightingRenderTests {
  LiquidMediumGeometry OpticalBody(bool rectangle,Vector3 position,Quaternion rotation,float scale=1){
   var go=Own(new GameObject("Finite optical body"));go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=Vector3.one*scale;
   var item=go.AddComponent<RoomItem>();var data=new RoomContainer{height=1,radius=.5f,capacityMl=1000,amountMl=1000,underwater=new(){enabled=true,tint="#000000",densityPerMetre=1}};
   if(rectangle){data.version=2;data.rectangle=new(){width=1,depth=1};}
   return new("pool",item,data,Vector3.up,null);
  }
  void UploadOptical(UnderwaterViewProjection projection,params LiquidMediumGeometry[] media){
   for(int i=0;i<media.Length;i++)projection.Media[i]=media[i];projection.Upload(media.Length);Shader.SetGlobalInt("_MaestroUnderwaterCount",projection.Count);
  }
  [TestCase(false)][TestCase(true)]
  public void UnderwaterPixelsUseFiniteWaterDistanceAndLeaveOutsideViewsUnchanged(bool rectangle){
   using var projection=new UnderwaterViewProjection();Assert.That(projection.Supported,Is.True);
   try{
    var m=Own(IllustratedMaterials.Create(Color.white,0));var q=Quad(m);q.transform.position=new Vector3(0,.5f,2);
    var camera=View();camera.transform.position=new Vector3(0,.5f,0);camera.farClipPlane=8;
    var water=OpticalBody(rectangle,Vector3.zero,Quaternion.identity);
    UploadOptical(projection,water);Pixel(Read(camera),Mathf.Exp(-.5f),Mathf.Exp(-.5f),Mathf.Exp(-.5f),1);
    q.transform.position=new Vector3(0,.5f,4);Pixel(Read(camera),Mathf.Exp(-.5f),Mathf.Exp(-.5f),Mathf.Exp(-.5f),1);
    camera.transform.position=new Vector3(0,.5f,-1);Pixel(Read(camera),1,1,1,1);
    camera.transform.position=new Vector3(0,.5f,0);water.Contents.underwater.enabled=false;UploadOptical(projection,water);Pixel(Read(camera),1,1,1,1);
   }finally{Shader.SetGlobalInt("_MaestroUnderwaterCount",0);}
  }
  [TestCase(4)][TestCase(8)][TestCase(16)]public void UnderwaterTerrainPixelsMatchIndependentNativeRayLengthAcrossADryRidge(int cells){
   using var projection=new UnderwaterViewProjection();
   try{
    var m=Own(IllustratedMaterials.Create(Color.white,0));var q=Quad(m);q.transform.position=new Vector3(3,.25f,0);q.transform.rotation=Quaternion.Euler(0,-90,0);
    var camera=View();camera.transform.position=new Vector3(-1.5f,.25f,0);camera.transform.rotation=Quaternion.Euler(0,90,0);camera.farClipPlane=8;
    var go=Own(new GameObject("Terrain optical body"));var item=go.AddComponent<RoomItem>();
    var values=new float[(cells+1)*(cells+1)];for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)values[z*(cells+1)+x]=Mathf.Max(0,.5f-Mathf.Abs(x*4f/cells-2)*.5f);
    var field=new RoomHeightField{cells=cells,width=4,depth=1.5f,maxHeight=.5f,heights=values};
    var view=go.AddComponent<HeightFieldView>();view.Apply(new[]{field});
    var data=new RoomContainer{version=3,height=1,rectangle=new(){width=4,depth=1.5f},capacityMl=1000,amountMl=800,underwater=new(){enabled=true,tint="#000000",densityPerMetre=1}};
    var water=new LiquidMediumGeometry("terrain",item,data,Vector3.up,null);Assert.That(water.Terrain,Is.Not.Null);
    UploadOptical(projection,water);
    Assert.That(water.MeasureSegment(camera.transform.position,q.transform.position,out var span),Is.True);
    float expected=Mathf.Exp(-(float)span.WetFraction*4.5f);Assert.That(span.WetFraction*4.5,Is.EqualTo(2.5).Within(.00002));
    Pixel(Read(camera),expected,expected,expected,1);
   }finally{Shader.SetGlobalInt("_MaestroUnderwaterCount",0);}
  }
  [TestCase(false, false)][TestCase(true, false)][TestCase(false, true)][TestCase(true, true)]
  public void UnderwaterTransformedPixelsMatchNativeGeometry(bool rectangle,bool perspective){
   using var projection=new UnderwaterViewProjection();
   try{
    var water=OpticalBody(rectangle,new Vector3(3,2,-4),Quaternion.Euler(20,37,11),1.7f);
    water.Contents.frame.position=new Vector3(.1f,-.2f,.3f);water.Contents.frame.rotation=Quaternion.Euler(-8,23,4);
    water=new LiquidMediumGeometry("tilted",water.Item,water.Contents,Vector3.up,null);
    var camera=View();camera.orthographic=!perspective;camera.fieldOfView=2;camera.orthographicSize=.01f;camera.farClipPlane=10;
    camera.transform.position=water.ToLocal.inverse.MultiplyPoint3x4(new Vector3(0,.25f,0));camera.transform.rotation=Quaternion.Euler(0,22,0);
    var q=Quad(Own(IllustratedMaterials.Create(Color.white,0)));q.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*3,camera.transform.rotation);
    Assert.That(water.MeasureSegment(camera.transform.position,q.transform.position,out var span),Is.True);
    var expected=Mathf.Exp(-3*(float)span.WetFraction);Assert.That(expected,Is.InRange(.1f,.95f));
    UploadOptical(projection,water);Pixel(Read(camera),expected,expected,expected,1);
   }finally{Shader.SetGlobalInt("_MaestroUnderwaterCount",0);}
  }
  [Test]public void UnderwaterTintPreservesAlphaAndReadableControlMaterials(){
   using var projection=new UnderwaterViewProjection();
   try{
    var water=OpticalBody(true,Vector3.zero,Quaternion.identity);water.Contents.underwater.tint="#FF0000";
    var m=Own(IllustratedMaterials.Create(Color.white,0));var q=Quad(m);q.transform.position=new Vector3(0,.5f,2);
    var camera=View();camera.transform.position=new Vector3(0,.5f,0);
    UploadOptical(projection,water);
    Assert.That(IllustratedSurface.TryCreate("blend",.5f,0,true,out var surface,out var error),Is.True,error);surface.Apply(m,true);
    Pixel(Read(camera),.5f,.5f*Mathf.Exp(-.5f),.5f*Mathf.Exp(-.5f),.5f);
    q.GetComponent<Renderer>().sharedMaterial=Own(IllustratedMaterials.CreateControl(Color.white));var wet=Read(camera);Shader.SetGlobalInt("_MaestroUnderwaterCount",0);var dry=Read(camera);Pixel(wet,dry.r,dry.g,dry.b,dry.a);
    var book=Own(new GameObject("Underwater book readability")).AddComponent<Maestro.Quest.Book.IllustratedBook>();book.Build();
    foreach(var page in book.GetComponentsInChildren<Maestro.Quest.Book.BookPageTarget>()){q.GetComponent<Renderer>().sharedMaterial=page.GetComponent<Renderer>().sharedMaterial;Shader.SetGlobalInt("_MaestroUnderwaterCount",0);dry=Read(camera);UploadOptical(projection,water);wet=Read(camera);Pixel(wet,dry.r,dry.g,dry.b,dry.a);}
   }finally{Shader.SetGlobalInt("_MaestroUnderwaterCount",0);}
  }
 }
}
