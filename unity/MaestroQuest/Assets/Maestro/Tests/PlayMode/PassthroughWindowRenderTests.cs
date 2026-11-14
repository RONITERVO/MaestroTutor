// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Maestro.Quest.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class PassthroughWindowRenderTests
    {
        readonly List<UnityEngine.Object> owned=new();
        T Own<T>(T x)where T:UnityEngine.Object{owned.Add(x);return x;}
        [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i])UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();}
        Material Paint(Color color,float opacity=1){var m=Own(IllustratedMaterials.Create(color,0));Assert.That(IllustratedSurface.TryCreate(opacity==1?"opaque":"blend",opacity,0,true,out var surface,out var error),Is.True,error);surface.Apply(m,true);return m;}
        GameObject Quad(Material m,float z=0,float size=1){var g=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));g.layer=31;g.transform.position=new Vector3(0,0,z);g.transform.localScale=Vector3.one*size;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
        Camera View(){var c=Own(new GameObject("Window render acceptance")).AddComponent<Camera>();c.enabled=false;c.cullingMask=1<<31;c.orthographic=true;c.orthographicSize=.6f;c.nearClipPlane=.01f;c.farClipPlane=4;c.transform.position=Vector3.back*2;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.blue;return c;}
        Material Mask(float reveal=1){var shader=Shader.Find("Maestro/PassthroughWindow");Assert.That(shader,Is.Not.Null);var m=Own(new Material(shader));m.SetFloat("_Reveal",reveal);return m;}
        Color[] Read(Camera c,string name){
            var rt=new RenderTexture(96,96,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var pixels=new Texture2D(96,96,TextureFormat.RGBA32,false,true);var prior=RenderTexture.active;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,96,96),0,0);pixels.Apply();string output=Environment.GetEnvironmentVariable("MAESTRO_WINDOW_PREVIEW");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());}return pixels.GetPixels();}
            finally{c.targetTexture=null;RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static Color Linear(float r,float g,float b,float a)=>new(Mathf.LinearToGammaSpace(r),Mathf.LinearToGammaSpace(g),Mathf.LinearToGammaSpace(b),a);
        static void Pixel(Color a,Color e,string context){Assert.That(a.r,Is.EqualTo(e.r).Within(.03f),context+" R");Assert.That(a.g,Is.EqualTo(e.g).Within(.03f),context+" G");Assert.That(a.b,Is.EqualTo(e.b).Within(.03f),context+" B");Assert.That(a.a,Is.EqualTo(e.a).Within(.03f),context+" compositor alpha");}
        [UnityTest]public IEnumerator WindowRevealEndpointsAndPartialCoverageMatchFromBothEyePositions(){
            var material=Mask();Quad(material);var camera=View();yield return null;
            foreach(float x in new[]{-.032f,.032f}){camera.transform.position=new Vector3(x,0,-2);foreach(float amount in new[]{0,.25f,.5f,1}){material.SetFloat("_Reveal",amount);Pixel(Read(camera,"window-"+x+"-"+amount)[48*96+48],Linear(0,0,1-amount,1-amount),"Eye "+x);}}
        }
        [UnityTest]public IEnumerator WindowRespectsOpaqueAndTransparentForegroundAndErasesOnlyWhatIsBehind(){
            var mask=Mask();Quad(mask);var back=Quad(Paint(Color.green,.5f),.2f);var front=Quad(Paint(Color.red,.5f),-.2f);var camera=View();yield return null;
            foreach(float x in new[]{-.032f,.032f}){camera.transform.position=new Vector3(x,0,-2);Pixel(Read(camera,"window-front-transparent-"+x)[48*96+48],Linear(.5f,0,0,.5f),"Transparent foreground must survive");}
            front.SetActive(false);Pixel(Read(camera,"window-clear-behind")[48*96+48],Color.clear,"Background transparent object is behind opening");
            front=Quad(Paint(Color.red),-.2f);Pixel(Read(camera,"window-front-opaque")[48*96+48],Color.red,"Opaque foreground depth");
            front.SetActive(false);back.SetActive(false);Quad(Mask(.5f),-.1f);mask.SetFloat("_Reveal",.5f);Pixel(Read(camera,"window-overlap")[48*96+48],Linear(0,0,.25f,.25f),"Overlapping partial masks multiply");
        }
        [UnityTest]public IEnumerator EllipseLeavesCornersAndBothSidesUsable(){
            var mask=Mask();mask.SetFloat("_Ellipse",1);Quad(mask);var camera=View();yield return null;
            var pixels=Read(camera,"window-ellipse");Pixel(pixels[48*96+48],Color.clear,"Ellipse centre");Pixel(pixels[15*96+15],Color.blue,"Ellipse corner");
            camera.transform.SetPositionAndRotation(Vector3.forward*2,Quaternion.Euler(0,180,0));Pixel(Read(camera,"window-reverse-side")[48*96+48],Color.clear,"Reverse side");
        }
    }
}
