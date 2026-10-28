// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  JObject WaterViewCall(string id,bool enabled=true)=>new(){["id"]="object.container.view.configure",["version"]=1,["arguments"]=new JObject{
   ["target"]=id,["revision"]=editor.ObjectRevision(id),["settings"]=JObject.FromObject(new UnderwaterViewSettings{enabled=enabled,tint="#103050",densityPerMetre=.7f})}};
  JObject WaterViewFact(string id){Assert.IsTrue(BehaviourCatalog.TryRead("object.container.view",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact));return (JObject)fact.Value;}
  [UnityTest] public IEnumerator UnderwaterEditsShareReceiptsPersistenceUndoAndPreserveOrdinaryContainerEdits(){
   var(ex,id,_)=Containers();Assert.That(RoomControls.Capabilities(editor),Does.Contain(UnderwaterViewCapability.Feature));var stale=WaterViewCall(id);var request=ContainerRequest(WaterViewCall(id));
   Assert.That((bool)WaterViewFact(id)["settings"]["enabled"],Is.False);
   Assert.IsTrue(ex.Execute(request,out var error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions);
   int revision=editor.ObjectRevision(id);Assert.IsTrue((bool)WaterViewFact(id)["settings"]["enabled"]);
   Assert.IsTrue(ex.Execute(request,out error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions);
   Assert.AreEqual(revision,editor.ObjectRevision(id),"Replaying a completed receipt cannot apply the edit twice");
   Assert.IsFalse(ex.Execute(ContainerRequest(stale),out _,out _));
   Assert.That(new RoomStorage(directory).Load(out error).objects.Single(o=>o.id==id).containers[0].underwater.tint,Is.EqualTo("#103050"),error);
   editor.Undo();Assert.IsFalse((bool)WaterViewFact(id)["settings"]["enabled"]);editor.Redo();Assert.IsTrue((bool)WaterViewFact(id)["settings"]["enabled"]);
   yield return ContainerAction(ex,ContainerCall(id,new RoomContainer{amountMl=35}));
   Assert.AreEqual("#103050",editor.Read(id).containers[0].underwater.tint);Assert.AreEqual(.7f,editor.Read(id).containers[0].underwater.densityPerMetre);
   yield return ContainerAction(ex,ContainerCall(id,null));Assert.IsFalse((bool)WaterViewFact(id)["configured"]);
   Assert.IsFalse(ex.Execute(ContainerRequest(WaterViewCall(id)),out _,out _));editor.Undo();Assert.IsTrue((bool)WaterViewFact(id)["settings"]["enabled"]);
  }
  [UnityTest] public IEnumerator UnderwaterFailedPublicationRetainsSavedAndVisibleSettings(){
   var(ex,id,_)=Containers();var before=editor.Read(id).containers[0].underwater.tint;var pending=System.IO.Path.Combine(directory,RoomStorage.FileName+".pending");System.IO.Directory.CreateDirectory(pending);
   try{Assert.IsTrue(ex.Execute(ContainerRequest(WaterViewCall(id)),out var error,out _),error);yield return SpatialSettingsCompletion.Wait(ex.Executions,"failed");
    Assert.IsFalse((bool)WaterViewFact(id)["settings"]["enabled"]);Assert.AreEqual(before,editor.Read(id).containers[0].underwater.tint);
   }finally{System.IO.Directory.Delete(pending);}
  }
  [UnityTest] public IEnumerator UnderwaterProjectionUsesRealCameraCallbacksAndSurvivesPhysicsPause(){
   var(pool,_) = MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterViewCall(pool));
   var view=root.GetComponent<UnderwaterView>();Assert.IsNotNull(view);
   var go=new GameObject("Underwater callback camera");go.transform.SetParent(root.transform,false);
   var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=0;camera.transform.position=new Vector3(4,1.4f,0);
   var texture=new RenderTexture(16,16,16);camera.targetTexture=texture;
   int inside=-1;Camera.CameraCallback inspect=c=>{if(c==camera)inside=Shader.GetGlobalInt("_MaestroUnderwaterCount");};Camera.onPreRender+=inspect;
   try{
    Shader.SetGlobalInt("_MaestroUnderwaterCount",0);camera.Render();Assert.AreEqual(1,inside);Assert.AreEqual(0,Shader.GetGlobalInt("_MaestroUnderwaterCount"));
    using var projection=new UnderwaterViewProjection();Assert.IsTrue(editor.Liquids.CaptureUnderwater(projection));Assert.AreEqual(pool,projection.Media[0].Id);
    var saved=RoomLiquidCodec.Encode(editor.Snapshot());Assert.IsTrue(editor.Liquids.CaptureUnderwater(projection));Assert.AreEqual(saved,RoomLiquidCodec.Encode(editor.Snapshot()),"Rendering must not advance physics or save quantities");
    view.enabled=false;inside=-1;camera.Render();Assert.AreEqual(0,inside);view.enabled=true;Camera.onPreRender-=inspect;Camera.onPreRender+=inspect;camera.Render();Assert.AreEqual(1,inside);
    yield return ContainerAction(new RoomAgentExecutor(editor),ContainerCall(pool,null));camera.Render();Assert.AreEqual(0,inside);Assert.AreEqual(0,Shader.GetGlobalInt("_MaestroUnderwaterCount"));
   }finally{Camera.onPreRender-=inspect;camera.targetTexture=null;texture.Release();Object.Destroy(texture);Object.Destroy(go);}
  }
  [UnityTest] public IEnumerator UnderwaterNestedCameraOverflowRestoresOuterViewAndClearsAfterRender(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterViewCall(pool));
   var cameras=new Camera[10];var textures=new RenderTexture[10];var before=new int[10];var after=new int[10];
   for(int i=0;i<cameras.Length;i++){var go=new GameObject("Nested water camera "+i);go.transform.SetParent(root.transform,false);cameras[i]=go.AddComponent<Camera>();cameras[i].enabled=false;cameras[i].cullingMask=0;cameras[i].transform.position=new Vector3(4,1.4f,0);textures[i]=new RenderTexture(8,8,16);cameras[i].targetTexture=textures[i];}
   Camera.CameraCallback inspect=c=>{int i=System.Array.IndexOf(cameras,c);if(i<0)return;before[i]=Shader.GetGlobalInt("_MaestroUnderwaterCount");if(i+1<cameras.Length)cameras[i+1].Render();after[i]=Shader.GetGlobalInt("_MaestroUnderwaterCount");};
   Camera.onPreRender+=inspect;
   try{cameras[0].Render();for(int i=0;i<cameras.Length;i++){Assert.AreEqual(i<8?1:0,before[i],"Before nested camera "+i);Assert.AreEqual(before[i],after[i],"Return from nested camera "+i);}Assert.AreEqual(0,Shader.GetGlobalInt("_MaestroUnderwaterCount"));}
   finally{Camera.onPreRender-=inspect;for(int i=0;i<cameras.Length;i++){cameras[i].targetTexture=null;textures[i].Release();Object.Destroy(textures[i]);Object.Destroy(cameras[i].gameObject);}}
  }
  [UnityTest] public IEnumerator UnderwaterNestedSelectionRetainsDisabledInnerCavities(){
   var(pool,_) = MediumFixture(.05f);physics.PausePhysics();yield return ContainerAction(new RoomAgentExecutor(editor),WaterViewCall(pool));
   Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Clear inner water",new Vector3(4,1.3f,0),1,Color.white,out var inner,out var error),error);
   ContainerFixture(new RoomAgentExecutor(editor),ContainerCall(inner,new RoomContainer{height=.2f,radius=.2f,amountMl=250}));
   using var projection=new UnderwaterViewProjection();Assert.IsTrue(editor.Liquids.CaptureUnderwater(projection));Assert.AreEqual(2,projection.Count);
   Assert.AreEqual(inner,projection.Media[0].Id);Assert.IsFalse(projection.Media[0].Contents.underwater.enabled);Assert.AreEqual(pool,projection.Media[1].Id);
   yield return null;
  }
 }
}
