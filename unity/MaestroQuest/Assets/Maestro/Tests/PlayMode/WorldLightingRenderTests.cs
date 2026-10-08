// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorldLightingRenderTests
    {
        readonly List<UnityEngine.Object> owned=new();
        T Own<T>(T value)where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown]public void Cleanup(){Shader.SetGlobalFloat("_MaestroLightingEnabled",0);for(int i=owned.Count-1;i>=0;i--)if(owned[i])UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();}
        static void Light(Vector3 sun,Vector3 ambient,float energy=1){Shader.SetGlobalFloat("_MaestroLightingEnabled",1);Shader.SetGlobalVector("_MaestroAmbient",ambient);Shader.SetGlobalVector("_MaestroSun",Vector3.one*energy);Shader.SetGlobalVector("_MaestroSunDirection",sun);}
        GameObject Quad(Material m){var q=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));q.layer=31;q.GetComponent<Renderer>().sharedMaterial=m;return q;}
        Camera View(){var c=Own(new GameObject("Lighting pixel acceptance")).AddComponent<Camera>();c.enabled=false;c.cullingMask=1<<31;c.orthographic=true;c.orthographicSize=.6f;c.nearClipPlane=.01f;c.farClipPlane=4;c.transform.position=Vector3.back*2;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.clear;return c;}
        Color Read(Camera camera){
            var rt=new RenderTexture(32,32,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(32,32,TextureFormat.RGBA32,false,true);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,32,32),0,0);image.Apply();var c=image.GetPixel(16,16);return new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a);}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Pixel(Color c,float r,float g,float b,float a){Assert.That(c.r,Is.EqualTo(r).Within(.025f));Assert.That(c.g,Is.EqualTo(g).Within(.025f));Assert.That(c.b,Is.EqualTo(b).Within(.025f));Assert.That(c.a,Is.EqualTo(a).Within(.025f));}
        [Test]public void WorldSpaceSunChangesWhenTheObjectTurnsAndDisabledLightingRestoresOriginalPixels(){
            Assert.That(QualitySettings.activeColorSpace,Is.EqualTo(ColorSpace.Linear));var m=Own(IllustratedMaterials.Create(Color.white,0));var q=Quad(m);var c=View();Shader.SetGlobalFloat("_MaestroLightingEnabled",0);Pixel(Read(c),1,1,1,1);
            Light(Vector3.back,Vector3.one*.1f,.6f);Pixel(Read(c),.7f,.7f,.7f,1);
            q.transform.rotation=Quaternion.Euler(0,180,0);Pixel(Read(c),.1f,.1f,.1f,1);
            // A rigid world placement rotates the light together with authored content.
            Light(Vector3.forward,Vector3.one*.1f,.6f);Pixel(Read(c),.7f,.7f,.7f,1);
            Shader.SetGlobalFloat("_MaestroLightingEnabled",0);Pixel(Read(c),1,1,1,1);
        }
        [Test]public void DarknessDoesNotChangeCompositorOpacityOrCutoutHoles(){
            var m=Own(IllustratedMaterials.Create(Color.white,0));Assert.That(IllustratedSurface.TryCreate("blend",.5f,0,true,out var surface,out var error),Is.True,error);surface.Apply(m,true);Quad(m);var c=View();Light(Vector3.back,Vector3.zero,0);Pixel(Read(c),0,0,0,.5f);
            Light(Vector3.back,new Vector3(.2f,.4f,.6f),0);Pixel(Read(c),.1f,.2f,.3f,.5f);
            var tex=Own(new Texture2D(1,1,TextureFormat.RGBA32,false,true));tex.SetPixel(0,0,new Color(1,1,1,.1f));tex.Apply();m.mainTexture=tex;Assert.That(IllustratedSurface.TryCreate("cutout",1,.5f,true,out surface,out error),Is.True,error);surface.Apply(m,true);Pixel(Read(c),0,0,0,0);
        }
        [Test]public void DailyKeyframesProduceContinuousPixelsAcrossMidnight(){
            var m=Own(IllustratedMaterials.Create(Color.white,0));Quad(m);var camera=View();
            var cycle=new Maestro.Quest.Creation.WorldTimeSettings{cycleEnabled=true,frames=new[]{new Maestro.Quest.Creation.WorldLightKeyframe{second=21600,ambientColor="#0000FF",ambientIntensity=.6f,sunIntensity=0},new Maestro.Quest.Creation.WorldLightKeyframe{second=64800,ambientColor="#FF0000",ambientIntensity=.2f,sunIntensity=0}}};
            var projection=new WorldLightingProjection(new Maestro.Quest.Creation.RoomLighting(),cycle);
            foreach(double second in new[]{0d,86399.999}){var sample=projection.Sample(second);Light(sample.Direction,sample.Ambient,0);Pixel(Read(camera),.1f,0,.3f,1);}
            var morning=projection.Sample(21600);Light(morning.Direction,morning.Ambient,0);Pixel(Read(camera),0,0,.6f,1);
        }
        [Test]public void ActualPalmRecoveryControlRetainsItsColourInAnUnlitWorld(){
            var c=View();var control=Own(new GameObject("Recall acceptance")).AddComponent<Maestro.Quest.Interaction.PalmRecoveryButton>();control.Build(0,null,null,control.transform,c.transform);
            var material=control.GetComponentsInChildren<Renderer>(true).First(x=>x.sharedMaterial.shader.name=="Maestro/Watercolor").sharedMaterial;Quad(material);
            Shader.SetGlobalFloat("_MaestroLightingEnabled",0);var before=Read(c);Light(Vector3.back,Vector3.zero,0);var after=Read(c);Pixel(after,before.r,before.g,before.b,before.a);
        }
        [Test]public void ActualBookPageMaterialStaysReadableInAnUnlitWorld(){
            var book=Own(new GameObject("Book acceptance")).AddComponent<IllustratedBook>();book.Build();var pages=book.GetComponentsInChildren<BookPageTarget>();Assert.That(pages.Length,Is.EqualTo(2));
            Light(Vector3.back,Vector3.zero,0);var c=View();var quad=Quad(pages[0].GetComponent<Renderer>().sharedMaterial);
            foreach(var page in pages){quad.GetComponent<Renderer>().sharedMaterial=page.GetComponent<Renderer>().sharedMaterial;Pixel(Read(c),1,1,1,1);}
        }
    }
}
