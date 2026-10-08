// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class VisibilityLayerTests
    {
        readonly List<UnityEngine.Object> owned=new();
        readonly List<IDisposable> leases=new();
        int variants,appearances;
        T Own<T>(T o)where T:UnityEngine.Object{owned.Add(o);return o;}
        [SetUp]public void Setup(){variants=VisibilityMaterials.LiveVariants;appearances=AppearanceMaterials.LiveVariants;}
        [TearDown]public void Cleanup(){
            for(int i=owned.Count-1;i>=0;i--)if(owned[i])UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();
            foreach(var l in leases)l.Dispose();leases.Clear();
            Assert.That(VisibilityMaterials.LiveVariants,Is.EqualTo(variants),"Visibility variants released");
            Assert.That(AppearanceMaterials.LiveVariants,Is.EqualTo(appearances),"Appearance variants released");
        }
        Material Paint(Color color)=>Own(IllustratedMaterials.Create(color,0));
        VisibilityMaterials.Lease Lease(Material source,VisibilityState state){var l=VisibilityMaterials.Acquire(source,state);leases.Add(l);return l;}
        static void Surface(Material material,string mode,float opacity=1,float cutoff=0) {
            Assert.That(IllustratedSurface.TryCreate(mode,opacity,cutoff,true,out var s,out var e),Is.True,e);s.Apply(material,true);
        }
        GameObject Quad(Material material,float z=0) {
            var obj=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));obj.layer=31;obj.transform.position=new Vector3(0,0,z);obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
        }
        Camera Camera() {
            var camera=Own(new GameObject("Visibility acceptance")).AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.orthographic=true;camera.orthographicSize=.6f;camera.nearClipPlane=.01f;camera.farClipPlane=4;
            camera.transform.position=Vector3.back*2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;return camera;
        }
        Color Read(Camera camera) {
            var render=new RenderTexture(32,32,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false,true);var prior=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);}
            finally{camera.targetTexture=null;RenderTexture.active=prior;render.Release();UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static void Pixel(Color actual,float r,float g,float b,float alpha,string label) {
            Assert.That(actual.r,Is.EqualTo(Mathf.LinearToGammaSpace(r)).Within(.03),label+" red");
            Assert.That(actual.g,Is.EqualTo(Mathf.LinearToGammaSpace(g)).Within(.03),label+" green");
            Assert.That(actual.b,Is.EqualTo(Mathf.LinearToGammaSpace(b)).Within(.03),label+" blue");
            Assert.That(actual.a,Is.EqualTo(alpha).Within(.025),label+" compositor alpha");
        }
        [Test]public void InvalidLayerValuesDoNotChangeAnyConsumer() {
            var state=new VisibilityState();var source=Paint(Color.white);var lease=Lease(source,state);state.Set(.6f,false);
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-.01f,1.01f})Assert.Throws<ArgumentOutOfRangeException>(()=>state.Set(value,true));
            Assert.That(state.Opacity,Is.EqualTo(.6f));Assert.That(state.RealDepth,Is.False);
            Assert.That(lease.Material.GetFloat("_VisibilityOpacity"),Is.EqualTo(.6f));Assert.That(lease.Material.GetFloat("_VisibilityRealDepth"),Is.Zero);
        }
        [Test]public void StateIdentityIsolatesRoomsAndSourcesWhileContinuousChangesReuseMaterials() {
            var source=Paint(Color.red);var state=new VisibilityState();var a=Lease(source,state);var b=Lease(source,state);var other=Lease(source,new VisibilityState());
            Assert.That(a.Material,Is.SameAs(b.Material));Assert.That(a.Material,Is.Not.SameAs(other.Material));
            int count=VisibilityMaterials.LiveVariants;int instance=a.Material.GetInstanceID();
            for(int i=0;i<=1000;i++)state.Set(i/1000f,i%2==0);
            Assert.That(a.Material.GetInstanceID(),Is.EqualTo(instance));Assert.That(VisibilityMaterials.LiveVariants,Is.EqualTo(count));
            state.Set(.2f,false);Assert.That(b.Material.GetFloat("_VisibilityOpacity"),Is.EqualTo(.2f));
            Assert.That(other.Material.GetFloat("_VisibilityOpacity"),Is.EqualTo(1));Assert.That(source.GetFloat("_VisibilityOpacity"),Is.EqualTo(1));
            Assert.That(other.Material.GetFloat("_VisibilityRealDepth"),Is.EqualTo(1));Assert.That(source.GetFloat("_VisibilityRealDepth"),Is.EqualTo(1));
            source.color=Color.blue;var changed=Lease(source,state);Assert.That(changed.Material,Is.Not.SameAs(a.Material));Assert.That(changed.Material.color,Is.EqualTo(Color.blue));Assert.That(a.Material.color,Is.EqualTo(Color.red));
        }
        [TestCase("opaque",1,0)] [TestCase("cutout",.8f,.5f)] [TestCase("blend",.4f,0)]
        public void ReturningToFullVisibilityRestoresExactSourceRenderState(string mode,float opacity,float cutoff) {
            var source=Paint(Color.red);Surface(source,mode,opacity,cutoff);source.renderQueue+=7;
            var state=new VisibilityState();var result=Lease(source,state).Material;
            for(int i=0;i<3;i++) {
                state.Set(.5f,false);Assert.That(result.renderQueue,Is.EqualTo(3000));Assert.That(result.GetInt("_ZWrite"),Is.Zero);Assert.That(result.GetShaderPassEnabled("PENCIL"),Is.False);
                Assert.That(result.GetFloat("_SurfaceMode"),Is.EqualTo(source.GetFloat("_SurfaceMode")));Assert.That(result.GetFloat("_AlphaCutoff"),Is.EqualTo(cutoff));
                state.Set(1,true);Assert.That(result.renderQueue,Is.EqualTo(source.renderQueue));Assert.That(result.GetInt("_ZWrite"),Is.EqualTo(source.GetInt("_ZWrite")));
                Assert.That(result.GetInt("_SrcBlend"),Is.EqualTo(source.GetInt("_SrcBlend")));Assert.That(result.GetInt("_DstBlend"),Is.EqualTo(source.GetInt("_DstBlend")));
                Assert.That(result.GetShaderPassEnabled("PENCIL"),Is.EqualTo(source.GetShaderPassEnabled("PENCIL")));Assert.That(result.GetTag("RenderType",false),Is.EqualTo(source.GetTag("RenderType",false)));
            }
        }
        [UnityTest]public IEnumerator RenderedLayerComposesOpacityAfterSurfaceMaskFromBothEyePositions() {
            Assert.That(QualitySettings.activeColorSpace,Is.EqualTo(ColorSpace.Linear));
            var texture=Own(new Texture2D(1,1,TextureFormat.RGBA32,false,true));texture.SetPixel(0,0,new Color(1,1,1,.75f));texture.Apply();
            var source=Paint(Color.red);source.mainTexture=texture;var quad=Quad(source);var camera=Camera();var state=new VisibilityState();yield return null;
            foreach(float x in new[]{-.032f,.032f}) {
                camera.transform.position=new Vector3(x,0,-2);
                foreach(string mode in new[]{"opaque","cutout","blend"}) {
                    Surface(source,mode,mode=="opaque"?1:.8f,mode=="cutout"?.5f:0);using var lease=VisibilityMaterials.Acquire(source,state);quad.GetComponent<Renderer>().sharedMaterial=lease.Material;
                    foreach(float opacity in new[]{0f,.2f,.5f,1f}) {
                        state.Set(opacity,true);float alpha=(mode=="blend"?.75f*.8f:1)*opacity;
                        Pixel(Read(camera),alpha,0,0,alpha,mode+" "+opacity);
                    }
                    if(mode=="cutout") {
                        texture.SetPixel(0,0,new Color(1,1,1,.2f));texture.Apply();state.Set(.8f,true);
                        Pixel(Read(camera),0,0,0,0,"A texture hole stays a hole while faded");
                        texture.SetPixel(0,0,new Color(1,1,1,.75f));texture.Apply();
                    }
                    quad.GetComponent<Renderer>().sharedMaterial=source;
                }
            }
        }
        [UnityTest]public IEnumerator ZeroLayerDoesNotHideForegroundOrWriteInvisibleDepth() {
            var state=new VisibilityState();state.Set(0,false);var front=Lease(Paint(Color.red),state);Quad(front.Material,-.2f);Quad(Paint(Color.blue),.2f);
            var camera=Camera();yield return null;Pixel(Read(camera),0,0,1,1,"Hidden layer leaves other objects visible");
            state.Set(.5f,false);Pixel(Read(camera),.5f,0,.5f,1,"Layer blends over an opaque object");
        }
        [UnityTest]public IEnumerator BindingPipelineComposesLayerAfterRootAndPartAndRestoresOnDisable() {
            var root=Own(new GameObject("Layered assembly"));var recipe=root.AddComponent<RecipeObject>();
            Assert.That(recipe.Apply(new RoomRecipe{parts=new[]{new RecipePart{id="body",shape="box",size=Vector3.one,color=Color.white},new RecipePart{id="tail",parent="body",shape="box",size=Vector3.one*.2f,color=Color.white}}}),Is.True);
            var page=Quad(Paint(Color.white));page.transform.SetParent(root.transform);page.AddComponent<Maestro.Quest.Book.BookPageTarget>();var pageMaterial=page.GetComponent<Renderer>().sharedMaterial;
            var views=root.GetComponentsInChildren<Renderer>();var originals=views.ToDictionary(r=>r,r=>r.sharedMaterial);
            var view=root.AddComponent<RoomAppearanceView>();var state=new VisibilityState();state.Set(.4f,false);view.ConfigureVisibility(state);
            string a=new string('a',32),b=new string('b',32);
            view.Configure(new RoomObjectData{kind=RoomObjectKind.Assembly,appearanceBindings=new[]{new AppearanceBinding{appearanceId=a},new AppearanceBinding{appearanceId=b,kind="part",partId="tail"}}},new[]{
                new RoomAppearance{id=a,name="Root",style=new(){tint="#FF0000"}},new RoomAppearance{id=b,name="Tail",style=new(){tint="#0000FF",renderMode="blend",opacity=.2f}}});
            var tail=recipe.Part("tail").GetComponentInChildren<Renderer>();var first=tail.sharedMaterial;
            Assert.That(first.color,Is.EqualTo(Color.blue));Assert.That(first.GetFloat("_SurfaceOpacity"),Is.EqualTo(.2f));Assert.That(first.GetFloat("_VisibilityOpacity"),Is.EqualTo(.4f));Assert.That(first.GetFloat("_VisibilityRealDepth"),Is.Zero);
            view.Refresh();Assert.That(tail.sharedMaterial,Is.SameAs(first));Assert.That(page.GetComponent<Renderer>().sharedMaterial,Is.SameAs(pageMaterial));
            view.enabled=false;foreach(var pair in originals)Assert.That(pair.Key.sharedMaterial,Is.SameAs(pair.Value));
            state.Set(.8f,true);view.enabled=true;Assert.That(tail.sharedMaterial.GetFloat("_VisibilityOpacity"),Is.EqualTo(.8f));
            view.ConfigureVisibility(null);Assert.That(tail.sharedMaterial.color,Is.EqualTo(Color.blue));Assert.That(tail.sharedMaterial.GetFloat("_VisibilityOpacity"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator LayerOnlyViewRestoresSourcesAndDoesNotTakeAnotherObjectsRenderers() {
            var source=Paint(Color.white);var obj=Quad(source);var owner=obj.AddComponent<Maestro.Quest.Interaction.RoomItem>();
            var other=Quad(source);other.transform.SetParent(obj.transform);other.AddComponent<Maestro.Quest.Interaction.RoomItem>();
            var view=obj.AddComponent<RoomAppearanceView>();var state=new VisibilityState();state.Set(.2f,false);view.ConfigureVisibility(state);
            Assert.That(obj.GetComponent<Renderer>().sharedMaterial,Is.Not.SameAs(source));Assert.That(other.GetComponent<Renderer>().sharedMaterial,Is.SameAs(source));
            view.Configure(new RoomObjectData{kind=RoomObjectKind.Block},Array.Empty<RoomAppearance>());
            Assert.That(obj.GetComponent<Renderer>().sharedMaterial.GetFloat("_VisibilityOpacity"),Is.EqualTo(.2f));
            view.ConfigureVisibility(null);Assert.That(obj.GetComponent<Renderer>().sharedMaterial,Is.SameAs(source));yield return null;
        }
        [UnityTest]public IEnumerator ImportedTextureSlotsRemainIndependentInsideOneVisibilityLayer() {
            var bytes=ModelFixture.Create(json=>{
                var first=(JObject)json["materials"][0];first["alphaMode"]="BLEND";first["pbrMetallicRoughness"]["baseColorFactor"]=new JArray(1,0,0,.4f);
                var second=(JObject)first.DeepClone();second["alphaMode"]="MASK";second["alphaCutoff"]=.3f;((JArray)json["materials"]).Add(second);
                var extra=(JObject)json["meshes"][0]["primitives"][0].DeepClone();extra["material"]=1;((JArray)json["meshes"][0]["primitives"]).Add(extra);
            });
            var root=Own(new GameObject("Layered import"));var model=root.AddComponent<ImportedModel>();var task=model.LoadAsync(ModelLibrary.Inspect("layer.glb",bytes));
            yield return new WaitUntil(()=>task.IsCompleted);Assert.That(task.Exception,Is.Null);Assert.That(model.Ready,Is.True);
            var sourceSlots=model.Instance.Renderers.SelectMany(r=>r.sharedMaterials).ToArray();var view=root.AddComponent<RoomAppearanceView>();var state=new VisibilityState();state.Set(.3f,false);view.ConfigureVisibility(state);
            var applied=model.Instance.Renderers.SelectMany(r=>r.sharedMaterials).ToArray();Assert.That(applied.Length,Is.EqualTo(sourceSlots.Length));
            for(int i=0;i<applied.Length;i++) {
                Assert.That(applied[i].GetTag("MaestroMaterialIndex",false),Is.EqualTo(sourceSlots[i].GetTag("MaestroMaterialIndex",false)));
                Assert.That(applied[i].GetFloat("_SurfaceMode"),Is.EqualTo(sourceSlots[i].GetFloat("_SurfaceMode")));
                Assert.That(applied[i].GetFloat("_SurfaceOpacity"),Is.EqualTo(sourceSlots[i].GetFloat("_SurfaceOpacity")));
                Assert.That(applied[i].GetFloat("_VisibilityOpacity"),Is.EqualTo(.3f));
            }
            view.Refresh();Assert.That(model.Instance.Renderers.SelectMany(r=>r.sharedMaterials).ToArray(),Is.EqualTo(applied));
            view.ConfigureVisibility(null);Assert.That(model.Instance.Renderers.SelectMany(r=>r.sharedMaterials).ToArray(),Is.EqualTo(sourceSlots));
        }
    }
}
