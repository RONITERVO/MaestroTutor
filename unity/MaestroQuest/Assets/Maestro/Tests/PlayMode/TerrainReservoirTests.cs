// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  (RoomAgentExecutor executor,string id) TerrainReservoir(bool slope=false){
   editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);string id=Snow(ex);
   var bed=new RoomHeightField{cells=8,width=2,depth=1,maxHeight=.5f,frame=new(){position=Vector3.up*.2f},material="Earth",color=new(.5f,.32f,.15f,1),heights=new float[81]};
   for(int z=0;z<=8;z++)for(int x=0;x<=8;x++)bed.heights[z*9+x]=slope?x*.05f:.1f;
   Assert.That(editor.EditHeightField(id,editor.ObjectRevision(id),bed,out var e),Is.True,e);
   // The existing generic action derives capacity/footprint/frame, ignoring the
   // editable ordinary-cavity defaults. The amount is deliberately not derived.
   ContainerFixture(ex,ContainerCall(id,new RoomContainer{version=3,rectangle=new(){width=.1f,depth=.1f},height=.5f,capacityMl=1,amountMl=slope?100000:200000}));
   physics.SetSurfaces(true,"Synthetic aligned terrain room");return(ex,id);
  }
  Vector3 TerrainPoint(string id,float x,float y,float z=0){var item=editor.Find(id);var bed=editor.Read(id).heightFields[0];return item.transform.TransformPoint(bed.frame.position+bed.frame.rotation*new Vector3(x,y,z));}
  [UnityTest]public IEnumerator TerrainWaterSharedAuthoringSculptAndUndoPreserveOneInventory(){
   var(ex,id)=TerrainReservoir(true);var before=editor.Read(id);var water=before.containers[0];Assert.That(water.version,Is.EqualTo(3));Assert.That(water.capacityMl,Is.EqualTo(600000).Within(.1));Assert.That(water.frame.position,Is.EqualTo(before.heightFields[0].frame.position));Assert.That(water.rectangle.width,Is.EqualTo(2));
   var wet=editor.Liquids.ObserveMedium(TerrainPoint(id,-.75f,.15f),"maestro");Assert.That((bool)wet["found"],Is.True,wet.ToString());Assert.That((double)wet["depthMetres"],Is.EqualTo(.05).Within(.0001));
   Assert.That((bool)editor.Liquids.ObserveMedium(TerrainPoint(id,.75f,.15f),"maestro")["found"],Is.False,"The hill is not water");
   var call=FieldCall(id,"lower",.1f);yield return ContainerAction(ex,call);var after=editor.Read(id);Assert.That(after.containers[0].amountMl,Is.EqualTo(water.amountMl));Assert.That(after.containers[0].capacityMl,Is.GreaterThan(water.capacityMl));Assert.That(editor.Find(id).GetComponent<HeightFieldView>().Accepted.heights,Is.EqualTo(after.heightFields[0].heights));
   var loaded=new RoomStorage(directory).Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.Single(o=>o.id==id).containers[0].amountMl,Is.EqualTo(water.amountMl));
   editor.Undo();Assert.That(editor.Read(id).heightFields[0].heights,Is.EqualTo(before.heightFields[0].heights));Assert.That(editor.Read(id).containers[0].capacityMl,Is.EqualTo(water.capacityMl));editor.Redo();Assert.That(editor.Read(id).containers[0].capacityMl,Is.EqualTo(after.containers[0].capacityMl));
   var prototype=CreationPrototype.Capture(editor.Read(id));Assert.That(prototype.version,Is.EqualTo(6));Assert.That(prototype.Validate(out error),Is.True,error);
   var encoded=CreationPrototypeSchema.Encode(prototype);Assert.That(CapabilityArguments.Validate(encoded,CreationPrototypeSchema.Schema(),out error),Is.True,error);
   string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"terrain-reservoir-prototype.json"),encoded.ToString());}
   prototype.version=5;Assert.That(prototype.Validate(out _),Is.False);
   Assert.That(editor.CopyObject(id,editor.ObjectRevision(id),"Independent pond",new Vector3(7,2,4),out var copy,out error),Is.True,error);Assert.That(editor.Read(copy).containers[0].amountMl,Is.EqualTo(water.amountMl));
   yield return null;
  }
  [UnityTest]public IEnumerator TerrainWaterRejectsOverflowMissingBedAndFailedPublicationAtomically(){
   var(ex,id)=TerrainReservoir();var c=editor.Read(id).containers[0];c.amountMl=c.capacityMl;yield return ContainerAction(ex,ContainerCall(id,c));var before=JsonUtility.ToJson(editor.Read(id));
   Assert.That(ex.Execute(ContainerRequest(FieldCall(id,"raise",.1f)),out var error,out _),Is.False);StringAssert.Contains("overflow",error);Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(before));
   Assert.That(editor.EditHeightField(id,editor.ObjectRevision(id),null,out _),Is.False);Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="box",mass=1}),Is.False);
   string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
   try{Assert.That(ex.Execute(ContainerRequest(FieldCall(id,"lower",.05f)),out _,out _),Is.True);yield return SpatialSettingsCompletion.Wait(ex.Executions,"failed");Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(before));}finally{Directory.Delete(pending);}
   var forged=editor.Snapshot();forged.objects.Single(o=>o.id==id).containers[0].capacityMl+=1;Assert.That(forged.Validate(out _),Is.False,"Saved metadata cannot claim a different bed capacity");
   forged=editor.Snapshot();forged.version=36;Assert.That(forged.Validate(out _),Is.False,"Old readers must not flatten a terrain cavity");
   yield return null;
  }
  [UnityTest]public IEnumerator TerrainWaterDippingConservesContentsAndCannotScoopThroughTheBed(){
   var(ex,id)=TerrainReservoir();var c=editor.Read(id).containers[0];c.amountMl=700000;yield return ContainerAction(ex,ContainerCall(id,c));
   string cup=CreateTemplateFixture(TemplateCall("cup",TerrainPoint(id,-.5f,.25f)));var target=editor.Find(cup);
   Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(cup),Is.GreaterThan(0));Assert.That(Liquid(id)+Liquid(cup),Is.EqualTo(700000).Within(.0001));physics.PausePhysics();
   double moved=editor.Read(cup).containers[0].amountMl;Assert.That(moved,Is.GreaterThan(0));editor.Undo();Assert.That(editor.Read(cup).containers[0].amountMl,Is.Zero);
   target=editor.Find(cup);target.transform.position=TerrainPoint(id,-.5f,.04f);Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(cup),Is.Zero,"The receiving cavity penetrates the bed");physics.PausePhysics();
   yield return null;
  }
  [UnityTest]public IEnumerator TerrainWaterRainBuoyancyAndUnavailableBedUseSharedMediumReadiness(){
   var(_,id)=TerrainReservoir();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Terrain water float",TerrainPoint(id,-.5f,.15f),.3f,Color.red,out var prop,out var error),Is.True,error);
   Assert.That(editor.SetItemPhysics(prop,new(){mode="solid",shape="box",mass=.05f}),Is.True);Physics.SyncTransforms();physics.StartPhysics();
   editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.True);
   Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=120},0,out error),Is.True,error);editor.Liquids.Tick(.1f);Assert.That(Liquid(id),Is.GreaterThan(200000));physics.PausePhysics();double accepted=editor.Read(id).containers[0].amountMl;
   var view=editor.Find(id).GetComponent<HeightFieldView>();view.Collision.enabled=false;
   var observation=editor.Liquids.ObserveMedium(TerrainPoint(id,-.5f,.15f),prop);Assert.That((string)observation["availability"],Is.Not.EqualTo("ready"),observation.ToString());
   physics.StartPhysics();editor.Liquids.Tick(.1f);editor.Liquids.TickMedium(.02f);Assert.That((bool)editor.Liquids.ObserveMediumBody(prop)["applied"],Is.False);Assert.That(editor.Read(id).containers[0].amountMl,Is.EqualTo(accepted));physics.PausePhysics();view.Collision.enabled=true;
   yield return null;
  }
  [UnityTest]public IEnumerator TerrainWaterHandContactsAndRipplesRespectTheDryShore(){
   var(_,id)=TerrainReservoir(true);var events=new System.Collections.Generic.List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};physics.StartPhysics();
   ContactPoint(TerrainPoint(id,.75f,.45f));ContactPoint(TerrainPoint(id,.75f,.15f));Assert.That(events,Is.Empty,"Crossing the water plane inside a dry hill must not invent a splash");Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.False);
   ContactPoint(null);ContactPoint(TerrainPoint(id,-.75f,.45f));ContactPoint(TerrainPoint(id,-.75f,.15f));Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered"}));Assert.That(events[0].BodyId,Is.EqualTo(id));Assert.That(editor.Find(id).GetComponent<ContainerFillView>().RippleCount,Is.GreaterThan(0));Assert.That(editor.Read(id).containers[0].amountMl,Is.EqualTo(100000));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator TerrainWaterRenderedSurfaceClipsDryGroundAndRemainsHorizontalWhenTilted(){
   var(_,id)=TerrainReservoir(true);var item=editor.Find(id);var surface=item.transform.Find("Measured liquid surface");Assert.That(surface,Is.Not.Null);var fill=item.GetComponent<ContainerFillView>();
   foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(12,30,20)}){
    item.transform.rotation=rotation;fill.Refresh();var mesh=surface.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh.vertexCount,Is.GreaterThan(4));var vertices=mesh.vertices.Select(surface.TransformPoint).ToArray();Assert.That(vertices.Max(p=>p.y)-vertices.Min(p=>p.y),Is.LessThan(.00001));
    var bed=editor.Read(id).heightFields[0];var inverse=(item.transform.localToWorldMatrix*Matrix4x4.TRS(bed.frame.position,bed.frame.rotation,Vector3.one)).inverse;
    foreach(var point in vertices){var local=inverse.MultiplyPoint3x4(point);Assert.That(local.y+.00001,Is.GreaterThanOrEqualTo(bed.HeightAt(new Vector2(local.x,local.z))));}
   }
   item.transform.rotation=Quaternion.identity;fill.Refresh();
   string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");if(!string.IsNullOrEmpty(output)){
    Directory.CreateDirectory(output);var camera=new GameObject("Terrain water evidence",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=7;camera.fieldOfView=42;camera.transform.position=item.transform.position+new Vector3(1.6f,2.5f,-2.3f);camera.transform.LookAt(item.transform.position+Vector3.up*.3f);
    var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
    try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"terrain-water.png"),pixels.EncodeToPNG());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+40&&p.b>p.g+20),Is.GreaterThan(2000));}
    finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(camera.gameObject);}
   }
   yield return null;
  }
 }
}
