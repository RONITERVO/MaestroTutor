// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string LiquidAppearanceFixture(){
            var(pool,unused)=MediumFixture(.05f);physics.PausePhysics();
            Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,Array.Empty<RoomObjectData>(),new[]{unused},out var error),error);
            return pool;
        }
        string WaterStyle(float opacity=1,string image=""){
            var value=new RoomAppearance{id=Guid.NewGuid().ToString("N"),name="Water surface",
                style=new(){tint="#FFFFFF",renderMode=opacity<1?"blend":"opaque",opacity=opacity,
                    grain=0,shading=0,patternMode=image==""?"inherit":"image",imageHash=image}};
            Assert.IsTrue(editor.EditAppearance(value,value.id,0,Array.Empty<string>(),out var error),error);
            return value.id;
        }
        AppearanceBinding WaterBinding(string style,string part=NativeAppearanceParts.LiquidSurface)=>
            new(){appearanceId=style,kind="nativePart",partId=part};
        void BindWater(string target,AppearanceBinding binding){
            Assert.IsTrue(editor.BindAppearance(target,editor.ObjectRevision(target),binding,
                editor.AppearanceRevision(binding.appearanceId),false,false,out var error),error);
        }
        ContainerFillView WaterView(string target)=>editor.Find(target).GetComponent<ContainerFillView>();
        [UnityTest] public IEnumerator LiquidAppearanceTargetsShareTheInvocationPathAndPreserveContents(){
            string target=LiquidAppearanceFixture(),style=WaterStyle(.25f);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(NativeAppearanceParts.Feature));
            var before=JsonUtility.ToJson(editor.Read(target).containers[0]);
            var targets=editor.ObserveAppearanceTargets(target,0);
            CollectionAssert.AreEquivalent(new[]{"root","nativePart","nativePart"},
                ((JArray)targets["targets"]).Select(x=>(string)x["kind"]).ToArray());
            Assert.IsTrue(AppearanceFacts.Targets().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,
                new JObject{["target"]=target,["offset"]=0},out _));
            var binding=WaterBinding(style);var cap=new AppearanceBindCapability();var args=cap.Example;
            args["target"]=target;args["revision"]=editor.ObjectRevision(target);
            args["appearanceRevision"]=editor.AppearanceRevision(style);args["binding"]=JObject.Parse(JsonUtility.ToJson(binding));
            var call=new JObject{["id"]=cap.Id,["version"]=1,["arguments"]=args};
            var ex=new RoomAgentExecutor(editor);yield return ContainerAction(ex,call);
            var receipt=(JObject)ex.Executions.Observe()["selected"];Assert.AreEqual("completed",(string)receipt["phase"],receipt.ToString());
            var lease=WaterView(target).ReusableReservation;
            Assert.AreEqual(.25f,lease.Renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));
            Assert.AreEqual(Color.white,lease.Renderer.sharedMaterial.color);
            Assert.AreEqual(1,lease.Line.sharedMaterial.GetFloat("_SurfaceOpacity"));
            Assert.IsTrue(editor.Find(target).GetComponentsInChildren<Renderer>().Where(r=>NativeAppearanceParts.Address(r)==null)
                .All(r=>!r.sharedMaterial.HasProperty("_SurfaceOpacity")||r.sharedMaterial.GetFloat("_SurfaceOpacity")==1));
            Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(target).containers[0]));
            string output=Environment.GetEnvironmentVariable("MAESTRO_LIQUID_APPEARANCE_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"native-liquid-appearance.json"),new JObject{
                    ["call"]=call,["receipt"]=receipt,["targets"]=editor.ObserveAppearanceTargets(target,0),
                    ["bindings"]=editor.ObserveObjectAppearances(target,0)}.ToString());
            }
        }
        [UnityTest] public IEnumerator LiquidAppearanceStreamsAreIndependentAndBindingsSurviveComponentUndo(){
            string target=LiquidAppearanceFixture(),surface=WaterStyle(.25f),stream=WaterStyle(.65f);
            var surfaceBinding=WaterBinding(surface);var streamBinding=WaterBinding(stream,NativeAppearanceParts.LiquidStream);
            BindWater(target,surfaceBinding);BindWater(target,streamBinding);
            var view=WaterView(target);view.ShowStream(new[]{new Vector3(4,2,0),new Vector3(4,1,0)},2,100,Color.red);
            Assert.AreEqual(.25f,view.ReusableReservation.Renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));
            Assert.AreEqual(.65f,view.ReusableReservation.Line.sharedMaterial.GetFloat("_SurfaceOpacity"));
            Assert.AreEqual(Color.white,view.ReusableReservation.Line.startColor,"Source pigment is carried once by the material");
            string oldLease=view.ReusableReservation.Id;
            ContainerFixture(new RoomAgentExecutor(editor),ContainerCall(target,null));
            Assert.AreEqual("missing native part",editor.AppearanceBindingState(target,surfaceBinding));
            Assert.AreEqual(2,editor.Read(target).appearanceBindings.Length);
            Assert.AreEqual(2,new RoomStorage(directory).Load(out var error).objects.Single(x=>x.id==target).appearanceBindings.Length,error);
            Assert.IsFalse(editor.BindAppearance(target,editor.ObjectRevision(target),surfaceBinding,editor.AppearanceRevision(surface),false,false,out _));
            editor.Undo();view=WaterView(target);
            Assert.AreNotEqual(oldLease,view.ReusableReservation.Id);
            Assert.AreEqual("active",editor.AppearanceBindingState(target,surfaceBinding));
            Assert.AreEqual(.25f,view.ReusableReservation.Renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));
            Assert.AreEqual(.65f,view.ReusableReservation.Line.sharedMaterial.GetFloat("_SurfaceOpacity"));
            yield return null;
        }
        [UnityTest] public IEnumerator LiquidAppearanceUvMappingSurvivesRightAngleTiltAndMeasuredLevelChanges(){
            string target=LiquidAppearanceFixture();var view=WaterView(target);
            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(0,0,90),Quaternion.Euler(90,0,0),Quaternion.Euler(31,19,47)}){
                editor.Find(target).transform.rotation=rotation;view.Refresh();
                var mesh=view.ReusableReservation.Mesh;var uv=mesh.uv;
                Assert.AreEqual(mesh.vertexCount,uv.Length);Assert.Greater(uv.Length,2);
                Assert.IsTrue(uv.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&p.x>=-.001f&&p.x<=1.001f&&p.y>=-.001f&&p.y<=1.001f));
                Assert.Greater(uv.Max(p=>p.x)-uv.Min(p=>p.x),.1f);
                Assert.Greater(uv.Max(p=>p.y)-uv.Min(p=>p.y),.1f);
            }
            editor.Find(target).transform.rotation=Quaternion.identity;
            var definition=editor.Read(target).containers[0];definition.amountMl/=2;
            view.ApplyLive(new[]{definition});
            Assert.That(view.ReusableReservation.Mesh.uv.Min(p=>p.x),Is.EqualTo(0).Within(.00001f));
            Assert.That(view.ReusableReservation.Mesh.uv.Max(p=>p.x),Is.EqualTo(1).Within(.00001f));
            yield return null;
        }
        [UnityTest] public IEnumerator LiquidAppearanceTerrainShoreKeepsFootprintUvWhenWaterLevelChanges(){
            var(_,target)=TerrainReservoir(true);var view=WaterView(target);
            var samples=new System.Collections.Generic.Dictionary<Vector3,Vector2>();int matched=0;
            foreach(double amount in new[]{100000d,200000d}){
                var data=editor.Read(target).containers[0];data.amountMl=amount;view.ApplyLive(new[]{data});
                var mesh=view.ReusableReservation.Mesh;var vertices=mesh.vertices;var uv=mesh.uv;var normal=mesh.normals[0];
                Assert.AreEqual(vertices.Length,uv.Length);Assert.Greater(vertices.Length,3);
                Assert.Greater(uv.Max(p=>p.x)-uv.Min(p=>p.x),.1f);Assert.Greater(uv.Max(p=>p.y)-uv.Min(p=>p.y),.1f);
                for(int i=0;i<vertices.Length;i++){
                    var point=Vector3.ProjectOnPlane(vertices[i],normal);
                    var key=new Vector3(Mathf.Round(point.x*10000),Mathf.Round(point.y*10000),Mathf.Round(point.z*10000));
                    if(samples.TryGetValue(key,out var previous)){Assert.Less(Vector2.Distance(previous,uv[i]),.0001f);matched++;}
                    else samples.Add(key,uv[i]);
                }
            }
            Assert.Greater(matched,3,"Shared bed positions keep their UV when the water rises");
            yield return null;
        }
        Camera WaterCamera(Vector3 position,bool below){
            var camera=new GameObject("Native liquid rendering",typeof(Camera)).GetComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=.6f;
            camera.nearClipPlane=.005f;camera.farClipPlane=4;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;camera.transform.position=position;
            camera.transform.rotation=Quaternion.LookRotation(below?Vector3.up:Vector3.down,Vector3.forward);return camera;
        }
        Color[] WaterPixels(Camera camera,string label){
            const int size=128;
            var rt=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
            try{
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,size,size),0,0);image.Apply();
                string output=Environment.GetEnvironmentVariable("MAESTRO_LIQUID_APPEARANCE_EVIDENCE");
                if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,label+".png"),image.EncodeToPNG());}
                return image.GetPixels();
            }finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        [UnityTest] public IEnumerator LiquidAppearanceRendersTransparencyAboveAndBelowTheSameNativeSurface(){
            string target=LiquidAppearanceFixture(),style=WaterStyle();
            BindWater(target,WaterBinding(style));var view=WaterView(target);
            view.ReusableReservation.Surface.layer=31;
            var centre=view.ReusableReservation.Renderer.bounds.center;
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.layer=31;marker.transform.localScale=new Vector3(.5f,.01f,.5f);
            var red=new Material(Shader.Find("Unlit/Color")){color=Color.red};marker.GetComponent<Renderer>().sharedMaterial=red;
            var camera=WaterCamera(centre+Vector3.up,true);
            try{
                yield return null;
                foreach(bool below in new[]{false,true}){
                    marker.transform.position=centre+Vector3.up*(below?.15f:-.04f);
                    camera.transform.position=centre+Vector3.up*(below?-.08f:1);
                    camera.transform.rotation=Quaternion.LookRotation(below?Vector3.up:Vector3.down,Vector3.forward);
                    var definition=editor.ReadAppearance(style);definition.style.renderMode="opaque";definition.style.opacity=1;
                    Assert.IsTrue(editor.EditAppearance(definition,style,editor.AppearanceRevision(style),new[]{target},out var error),error);
                    Assert.AreEqual(1,view.ReusableReservation.Renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));
                    var opaque=WaterPixels(camera,(below?"below":"above")+"-opaque")[64*128+64];
                    definition.style.renderMode="blend";definition.style.opacity=.25f;
                    Assert.IsTrue(editor.EditAppearance(definition,style,editor.AppearanceRevision(style),new[]{target},out error),error);
                    Assert.AreEqual(.25f,view.ReusableReservation.Renderer.sharedMaterial.GetFloat("_SurfaceOpacity"));
                    view.ReusableReservation.Renderer.enabled=false;
                    var baseline=WaterPixels(camera,(below?"below":"above")+"-marker")[64*128+64];
                    view.ReusableReservation.Renderer.enabled=true;
                    Assert.Greater(baseline.r-baseline.g,.8f,"Camera must see the marker before water is drawn");
                    var transparent=WaterPixels(camera,(below?"below":"above")+"-transparent")[64*128+64];
                    Assert.Greater(transparent.r-transparent.g,.4f,"The red marker must be visible through the water");
                    Assert.Less(Mathf.Abs(opaque.r-opaque.g),.1f,"The opaque surface must hide the same marker");
                }
            }finally{UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(marker);UnityEngine.Object.DestroyImmediate(red);}
        }
        [UnityTest] public IEnumerator LiquidAppearanceUsesImportedImageAcrossTheSurface(){
            string target=LiquidAppearanceFixture();var source=new Texture2D(8,8,TextureFormat.RGBA32,false);
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)source.SetPixel(x,y,x<4?Color.red:Color.green);
            source.Apply();var asset=ImageLibrary.Inspect("Water grid",source.EncodeToPNG());UnityEngine.Object.DestroyImmediate(source);
            var saving=editor.Images.SaveAsync(asset);while(!saving.IsCompleted)yield return null;Assert.IsFalse(saving.IsFaulted,saving.Exception?.ToString());
            string style=WaterStyle(1,asset.Hash);var binding=WaterBinding(style);BindWater(target,binding);
            double until=Time.realtimeSinceStartupAsDouble+5;
            while(editor.AppearanceRenderState(target,binding)!="active"&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.AreEqual("active",editor.AppearanceRenderState(target,binding));
            var lease=WaterView(target).ReusableReservation;lease.Surface.layer=31;var camera=WaterCamera(lease.Renderer.bounds.center+Vector3.up,false);
            try{
                var pixels=WaterPixels(camera,"surface-imported-image");
                Assert.Greater(pixels.Count(p=>p.r-p.g>.4f),1000,"Image has a red half");
                Assert.Greater(pixels.Count(p=>p.g-p.r>.4f),1000,"Image has a green half, not a single sampled texel");
                Assert.AreEqual(asset.Hash,editor.ReadAppearance(style).style.imageHash);
            }finally{UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
