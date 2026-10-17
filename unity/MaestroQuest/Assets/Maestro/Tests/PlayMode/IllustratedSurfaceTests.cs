// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class IllustratedSurfaceTests
    {
        readonly List<UnityEngine.Object> owned=new();
        T Own<T>(T item) where T:UnityEngine.Object {owned.Add(item);return item;}
        Material Paint(Color color)=>Own(IllustratedMaterials.Create(color,0));
        static IllustratedSurface Surface(string mode,float opacity=1,float cutoff=.5f,bool twoSided=true) {
            Assert.That(IllustratedSurface.TryCreate(mode,opacity,cutoff,twoSided,out var value,out var error),Is.True,error);return value;
        }
        [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i])UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();}
        [Test]public void SurfaceValidationDoesNotAcceptOpaqueTransparencyOrNonfiniteValues() {
            foreach(var mode in new[]{"", "water", "Blend"})Assert.That(IllustratedSurface.TryCreate(mode,1,.5f,true,out _,out _),Is.False);
            foreach(var opacity in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f})Assert.That(IllustratedSurface.TryCreate("blend",opacity,.5f,true,out _,out _),Is.False);
            Assert.That(IllustratedSurface.TryCreate("cutout",1,float.NaN,true,out _,out _),Is.False);
            Assert.That(IllustratedSurface.TryCreate("opaque",.5f,0,true,out _,out _),Is.False);
            Assert.That(Surface("blend",.5f,1).Equals(Surface("blend",.5f,0)),Is.True,"Inactive cutoff is canonical");
            Assert.That(Surface("blend",.5f).Equals(Surface("blend",.6f)),Is.False);
        }
        [Test]public void ModeChangesHaveExplicitDepthQueueCullingAndOutlineRules() {
            var material=Paint(Color.white);
            Surface("blend",.4f).Apply(material,true);
            Assert.That(material.renderQueue,Is.EqualTo(3000));Assert.That(material.GetFloat("_ZWrite"),Is.Zero);
            Assert.That(material.GetTag("RenderType",false),Is.EqualTo("Transparent"));Assert.That(material.GetShaderPassEnabled("PENCIL"),Is.False);
            Surface("cutout",.8f,.35f,false).Apply(material,true);
            Assert.That(material.renderQueue,Is.EqualTo(2450));Assert.That(material.GetFloat("_ZWrite"),Is.EqualTo(1));
            Assert.That(material.GetFloat("_AlphaCutoff"),Is.EqualTo(.35f));Assert.That(material.GetInt("_SurfaceCull"),Is.EqualTo(2));Assert.That(material.GetShaderPassEnabled("PENCIL"),Is.True);
            IllustratedSurface.Opaque.Apply(material,false);Assert.That(material.renderQueue,Is.EqualTo(2000));Assert.That(material.GetFloat("_AlphaCutoff"),Is.Zero);
        }
        GameObject Quad(Material material,float z=0,float size=1) {
            var obj=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));obj.layer=31;obj.transform.position=new Vector3(0,0,z);obj.transform.localScale=Vector3.one*size;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
        }
        Camera Camera() {
            var camera=Own(new GameObject("Appearance acceptance")).AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.orthographic=true;camera.orthographicSize=.6f;camera.nearClipPlane=.01f;camera.farClipPlane=4;
            camera.transform.position=Vector3.back*2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.blue;return camera;
        }
        Color[] Read(Camera camera,string name) {
            var render=new RenderTexture(96,96,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(96,96,TextureFormat.RGBA32,false,true);var prior=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,96,96),0,0);pixels.Apply();
                string output=Environment.GetEnvironmentVariable("MAESTRO_SURFACE_PREVIEW");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());}
                return pixels.GetPixels();
            }finally{camera.targetTexture=null;RenderTexture.active=prior;render.Release();UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static void Pixel(Color actual,Color expected,string where) {
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(.025f),where+" red");
            Assert.That(actual.g,Is.EqualTo(expected.g).Within(.025f),where+" green");
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(.025f),where+" blue");
            Assert.That(actual.a,Is.EqualTo(expected.a).Within(.025f),where+" compositor alpha");
        }
        static Color Linear(float r,float g,float b,float a=1)=>new Color(Mathf.LinearToGammaSpace(r),Mathf.LinearToGammaSpace(g),Mathf.LinearToGammaSpace(b),a);
        [UnityTest]public IEnumerator RenderedBlendPreservesBackgroundAndCompositorCoverageFromBothEyePositions() {
            Assert.That(QualitySettings.activeColorSpace,Is.EqualTo(ColorSpace.Linear));
            var material=Paint(Color.red);Quad(material);var camera=Camera();yield return null;
            foreach(float x in new[]{-.032f,.032f}) {
                camera.transform.position=new Vector3(x,0,-2);string eye=x<0?"left":"right";
                Surface("blend",.5f).Apply(material,true);Pixel(Read(camera,eye+"-half")[48*96+48],Linear(.5f,0,.5f),eye);
                Surface("blend",0).Apply(material,true);Pixel(Read(camera,eye+"-zero")[48*96+48],Color.blue,eye);
                material.color=new Color(1,0,0,0);IllustratedSurface.Opaque.Apply(material,false);
                Pixel(Read(camera,eye+"-opaque")[48*96+48],Color.red,eye+" opaque ignores incidental color alpha");
                camera.backgroundColor=Color.clear;material.color=Color.red;Surface("blend",.5f).Apply(material,true);
                Pixel(Read(camera,eye+"-passthrough-alpha")[48*96+48],Linear(.5f,0,0,.5f),eye+" alpha must not be squared");
                camera.backgroundColor=Color.blue;
            }
        }
        [UnityTest]public IEnumerator CutoutUsesTextureFactorAndVertexAlphaAndKeepsAcceptedDepthOpaque() {
            var texture=Own(new Texture2D(1,1,TextureFormat.RGBA32,false,true));texture.SetPixel(0,0,new Color(1,1,1,.75f));texture.Apply();
            var material=Paint(Color.red);material.mainTexture=texture;var quad=Quad(material);var camera=Camera();
            var mesh=Own(UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh));quad.GetComponent<MeshFilter>().sharedMesh=mesh;
            mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();yield return null;
            Surface("cutout",1,.5f).Apply(material,true);Pixel(Read(camera,"cutout-present")[48*96+48],Color.red,"Accepted cutout");
            Surface("cutout",.5f,.5f).Apply(material,true);Pixel(Read(camera,"cutout-factor")[48*96+48],Color.blue,"Factor is part of mask");
            Surface("cutout",1,.5f).Apply(material,true);mesh.colors=Enumerable.Repeat(new Color(1,1,1,.5f),mesh.vertexCount).ToArray();
            Pixel(Read(camera,"cutout-vertex")[48*96+48],Color.blue,"Vertex alpha is part of mask");
        }
        [UnityTest]public IEnumerator TransparentLayersSortBehindOpaqueForegroundWithoutWritingInvisibleDepth() {
            var red=Paint(Color.red);Surface("blend",.5f).Apply(red,true);Quad(red,0);
            var green=Paint(Color.green);Surface("blend",.5f).Apply(green,true);Quad(green,.2f);
            var foreground=Quad(Paint(Color.white),-.2f,.25f);var camera=Camera();yield return null;
            foreach(float x in new[]{-.032f,.032f}) {
                camera.transform.position=new Vector3(x,0,-2);var pixels=Read(camera,x<0?"layers-left":"layers-right");
                Pixel(pixels[48*96+48],Color.white,"Opaque foreground");
                Pixel(pixels[48*96+70],Linear(.5f,.25f,.25f),"Sorted translucent layers");
            }
            foreground.SetActive(false);Surface("blend",0).Apply(red,true);Surface("blend",0).Apply(green,true);
            Pixel(Read(camera,"layers-hidden")[48*96+48],Color.blue,"Zero-opacity surfaces neither shade nor hide background");
        }
        [UnityTest]public IEnumerator StylingRetainsTextureUvsSharedSlotsAndImportedOpacityThroughTint() {
            var original=Own(new Material(Shader.Find("Standard")));original.SetFloat("_Mode",2);original.color=new Color(.7f,.4f,.2f,.35f);
            var texture=Own(new Texture2D(2,2));original.mainTexture=texture;original.mainTextureScale=new Vector2(2,3);original.mainTextureOffset=new Vector2(.2f,.4f);
            var root=Own(new GameObject("Imported styled model"));var first=Quad(original);first.transform.SetParent(root.transform);
            var second=Quad(original,.2f);second.transform.SetParent(root.transform);
            var style=root.AddComponent<PencilModelStyle>();style.Apply();yield return null;
            var shared=first.GetComponent<Renderer>().sharedMaterial;
            Assert.That(second.GetComponent<Renderer>().sharedMaterial,Is.SameAs(shared));Assert.That(shared.mainTexture,Is.SameAs(texture));
            Assert.That(shared.mainTextureScale,Is.EqualTo(original.mainTextureScale));Assert.That(shared.mainTextureOffset,Is.EqualTo(original.mainTextureOffset));
            Assert.That(shared.GetFloat("_SurfaceOpacity"),Is.EqualTo(.35f));Assert.That(shared.renderQueue,Is.EqualTo(3000));Assert.That(shared.GetFloat("_AlphaCutoff"),Is.Zero);
            style.Tint(Color.green);Assert.That(shared.GetFloat("_SurfaceOpacity"),Is.EqualTo(.35f));
            Assert.That(original.color.a,Is.EqualTo(.35f));Assert.That(original.color.r,Is.EqualTo(.7f),"Source material stays untouched");
            Assert.That(IllustratedSurface.Imported(shared,shared.color).Equals(Surface("blend",.35f,0,false)),Is.True,"Restyling cannot erase alpha");
        }
        [UnityTest]public IEnumerator ActualGltfRetainsSourceSlotIdentityAndTwoSidedThinSurfaces() {
            var bytes=ModelFixture.Create(json=>{
                var first=(JObject)json["materials"][0];first["name"]="Same name";first["doubleSided"]=true;first["alphaMode"]="BLEND";
                first["pbrMetallicRoughness"]["baseColorFactor"]=new JArray(1,0,0,.5f);
                var second=(JObject)first.DeepClone();second["doubleSided"]=false;second["alphaMode"]="MASK";
                ((JArray)json["materials"]).Add(second);
                var extra=(JObject)json["meshes"][0]["primitives"][0].DeepClone();extra["material"]=1;
                ((JArray)json["meshes"][0]["primitives"]).Add(extra);
            });
            var root=Own(new GameObject("Two sided import"));var model=root.AddComponent<ImportedModel>();var task=model.LoadAsync(ModelLibrary.Inspect("slots.glb",bytes));
            yield return new WaitUntil(()=>task.IsCompleted);Assert.That(task.Exception,Is.Null);
            var style=model.Instance.GetComponent<PencilModelStyle>();var front=style.ImportedMaterial(0);var other=style.ImportedMaterial(1);
            Assert.That(front,Is.Not.Null);Assert.That(other,Is.Not.Null);Assert.That(front,Is.Not.SameAs(other));
            Assert.That(front.GetInt("_SurfaceCull"),Is.Zero);Assert.That(other.GetInt("_SurfaceCull"),Is.EqualTo(2));
            Assert.That(front.GetInt("_SurfaceMode"),Is.EqualTo(2));Assert.That(other.GetInt("_SurfaceMode"),Is.EqualTo(1));
            // Both duplicate-named slots are addressable by source index; names never bind edits.
            foreach(var renderer in model.Instance.Renderers) {
                renderer.gameObject.layer=31;
                // Render only the two-sided source; the second slot is checked above.
                var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=front;renderer.sharedMaterials=materials;
            }
            var camera=Camera();camera.orthographicSize=.23f;
            camera.transform.position=Vector3.back;camera.transform.rotation=Quaternion.identity;
            var a=Read(camera,"import-front");
            camera.transform.position=Vector3.forward;camera.transform.rotation=Quaternion.Euler(0,180,0);
            var b=Read(camera,"import-back");
            Assert.That(a.Count(c=>c.r>.2f),Is.GreaterThan(200));Assert.That(b.Count(c=>c.r>.2f),Is.GreaterThan(200),"Authored back of thin surface must remain visible");
        }
        [Test]public void BookBrowserPagesAreExcludedFromModelRestyling() {
            var material=Paint(Color.white);var page=Quad(material);page.AddComponent<Maestro.Quest.Book.BookPageTarget>();
            var texture=Own(new Texture2D(2,2));material.mainTexture=texture;material.SetFloat("_DecodeBrowserSrgb",1);
            var mesh=page.GetComponent<MeshFilter>().sharedMesh;page.AddComponent<PencilModelStyle>().Apply();
            Assert.That(page.GetComponent<Renderer>().sharedMaterial,Is.SameAs(material));Assert.That(page.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
            Assert.That(material.GetFloat("_DecodeBrowserSrgb"),Is.EqualTo(1));Assert.That(material.GetFloat("_SurfaceMode"),Is.Zero);
        }
        [UnityTest]public IEnumerator ActualGltfImportKeepsOpaqueMaskAndBlendDistinct() {
            foreach(string mode in new[]{"OPAQUE","MASK","BLEND"}) {
                var bytes=ModelFixture.Create(json=>{
                    var material=json["materials"][0];material["alphaMode"]=mode;material["alphaCutoff"]=.25f;
                    material["pbrMetallicRoughness"]["baseColorFactor"]=new JArray(1,0,0,.4f);
                });
                var root=Own(new GameObject("Actual "+mode));var model=root.AddComponent<ImportedModel>();
                var task=model.LoadAsync(ModelLibrary.Inspect("alpha.glb",bytes));yield return new WaitUntil(()=>task.IsCompleted);
                Assert.That(task.Exception,Is.Null);Assert.That(model.Ready,Is.True);
                var material=model.Instance.Renderers[0].sharedMaterial;int expected=mode=="BLEND"?2:mode=="MASK"?1:0;
                Assert.That(material.GetInt("_SurfaceMode"),Is.EqualTo(expected),mode);Assert.That(material.GetFloat("_SurfaceOpacity"),Is.EqualTo(mode=="OPAQUE"?1:.4f).Within(.0001),mode);
                Assert.That(material.GetFloat("_AlphaCutoff"),Is.EqualTo(mode=="MASK"?.25f:0),mode);
                UnityEngine.Object.Destroy(root);yield return null;yield return null;
            }
        }
    }
}
