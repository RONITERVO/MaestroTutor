// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using NUnit.Framework;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorldLightingRenderTests
    {
        void CaptureLocalLight(Camera camera,string name){
            string directory=System.Environment.GetEnvironmentVariable("MAESTRO_LOCAL_LIGHT_EVIDENCE");if(string.IsNullOrEmpty(directory))return;
            System.IO.Directory.CreateDirectory(directory);var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var image=new Texture2D(768,768,TextureFormat.RGBA32,false);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        Light Lamp(LightType type,Vector3 position) {
            var lamp=Own(new GameObject("Authored light rendering check")).AddComponent<Light>();
            lamp.type=type;lamp.color=Color.white;lamp.intensity=1;lamp.range=4;lamp.spotAngle=100;
            lamp.cullingMask=1<<30;lamp.renderMode=LightRenderMode.ForcePixel;
            lamp.transform.position=position;lamp.transform.rotation=Quaternion.LookRotation(-position);
            Shader.SetGlobalFloat("_MaestroLocalLightingEnabled",1);return lamp;
        }
        [UnityTest]public IEnumerator FirstPointLightIlluminatesTheSharedPigmentAndRetainsOpacity() {
            var m=Own(IllustratedMaterials.Create(Color.white,0));var receiver=Quad(m);receiver.layer=30;var camera=View();camera.cullingMask=1<<30;
            Light(Vector3.back,Vector3.zero,0);var dark=Read(camera);Assert.That(dark.r,Is.LessThan(.01));
            var lamp=Lamp(LightType.Point,Vector3.back);yield return null;camera.renderingPath=RenderingPath.Forward;
            CaptureLocalLight(camera,"point-on");var lit=Read(camera);Assert.That(lit.r,Is.GreaterThan(.15));Assert.That(lit.a,Is.EqualTo(1).Within(.01));
            Assert.That(IllustratedSurface.TryCreate("blend",.5f,0,true,out var surface,out var error),Is.True,error);surface.Apply(m,true);
            var transparent=Read(camera);Assert.That(transparent.r,Is.EqualTo(lit.r*.5f).Within(.04));Assert.That(transparent.a,Is.EqualTo(.5f).Within(.01));
            m.SetFloat("_WorldLighting",0);Pixel(Read(camera),.5f,.5f,.5f,.5f);
            lamp.enabled=false;m.SetFloat("_WorldLighting",1);Assert.That(Read(camera).r,Is.LessThan(.01));
        }
        [UnityTest]public IEnumerator SpotLightShadowBlocksIlluminationAndCutoutHolesTransmitIt()=>ShadowTransmission(LightType.Spot);
        [UnityTest]public IEnumerator PointLightShadowBlocksIlluminationAndCutoutHolesTransmitIt()=>ShadowTransmission(LightType.Point);
        IEnumerator ShadowTransmission(LightType type) {
            var previous=QualitySettings.shadows;int pixels=QualitySettings.pixelLightCount;float distance=QualitySettings.shadowDistance;
            try {
                QualitySettings.shadows=ShadowQuality.HardOnly;QualitySettings.pixelLightCount=4;QualitySettings.shadowDistance=10;
                var material=Own(IllustratedMaterials.Create(Color.white,0));Quad(material).layer=30;var camera=View();camera.cullingMask=1<<30;Light(Vector3.back,Vector3.zero,0);
                var lamp=Lamp(type,new Vector3(.6f,0,-.8f));lamp.shadows=LightShadows.Hard;lamp.shadowBias=.005f;lamp.shadowNormalBias=0;lamp.shadowCustomResolution=512;
                yield return null;camera.renderingPath=RenderingPath.Forward;float lit=Read(camera).r;Assert.That(lit,Is.GreaterThan(.1f));
                var blocker=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));blocker.layer=30;blocker.transform.position=lamp.transform.position*.5f;blocker.transform.localScale=Vector3.one*.18f;
                var blockerMaterial=Own(IllustratedMaterials.Create(Color.white,0));blocker.GetComponent<Renderer>().sharedMaterial=blockerMaterial;
                yield return null;CaptureLocalLight(camera,type+"-shadow");float shadowed=Read(camera).r;Assert.That(shadowed,Is.LessThan(lit*.25f),"A virtual wall must block the light, not merely dim the lamp itself");
                var holes=Own(new Texture2D(1,1,TextureFormat.RGBA32,false,true));holes.SetPixel(0,0,new Color(1,1,1,0));holes.Apply();blockerMaterial.mainTexture=holes;
                Assert.That(IllustratedSurface.TryCreate("cutout",1,.5f,true,out var cutout,out var error),Is.True,error);cutout.Apply(blockerMaterial,true);
                Assert.That(Read(camera).r,Is.EqualTo(lit).Within(.05f));
                // A faded visual layer must not retain an invisible opaque shadow.
                blockerMaterial.mainTexture=Texture2D.whiteTexture;blockerMaterial.SetFloat("_VisibilityOpacity",0);
                Assert.That(Read(camera).r,Is.EqualTo(lit).Within(.05f));
            } finally {QualitySettings.shadows=previous;QualitySettings.pixelLightCount=pixels;QualitySettings.shadowDistance=distance;}
        }
    }
}
