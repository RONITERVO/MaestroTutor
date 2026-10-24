// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class EnvironmentOcclusionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RealDepthHidesPigmentOutlinesAndLetteringButVirtualCaptureStaysComplete(bool lettering)
        {
            var root = new GameObject("Environment depth pixel test");
            var target = new RenderTexture(64,64,24,RenderTextureFormat.ARGB32);
            var readback = new Texture2D(64,64,TextureFormat.RGBA32,false);
            var depth = new Texture2DArray(2,2,2,TextureFormat.RFloat,false,true);
            var previousTarget = RenderTexture.active;
            var previousDepth = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
            var previousParams = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
            var previousMatrices = Shader.GetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices");
            var previousBypass = Shader.GetGlobalFloat("_MaestroEnvironmentDepthBypass");
            bool hard = Shader.IsKeywordEnabled("HARD_OCCLUSION"), soft = Shader.IsKeywordEnabled("SOFT_OCCLUSION");
            Material material = null;
            try
            {
                if (lettering)
                {
                    var text = new GameObject("Marking",typeof(TextMesh)).GetComponent<TextMesh>(); text.transform.SetParent(root.transform,false);
                    text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=64; text.characterSize=.1f;
                    text.anchor=TextAnchor.MiddleCenter; text.text="M"; text.color=Color.black;
                    material=new Material(IllustratedMaterials.TextMaterial(text.font)); text.GetComponent<Renderer>().sharedMaterial=material;
                }
                else
                {
                    var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform,false); cube.transform.localScale=Vector3.one*.65f;
                    material=IllustratedMaterials.Create(Color.red); material.SetFloat("_PencilWidth",.004f); cube.GetComponent<Renderer>().sharedMaterial=material;
                }
                var camera=new GameObject("Depth camera",typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                camera.enabled=false; camera.transform.position=Vector3.back*2; camera.orthographic=true; camera.orthographicSize=.6f;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.white; camera.targetTexture=target;
                Shader.DisableKeyword("SOFT_OCCLUSION"); Shader.EnableKeyword("HARD_OCCLUSION"); Shader.SetGlobalFloat("_MaestroEnvironmentDepthBypass",0);
                // Constant virtual depth 2m, with real depth switched between 1m and 4m.
                var reprojection=Matrix4x4.identity; reprojection.m22=0;
                Shader.SetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices",new[]{reprojection,reprojection});
                Shader.SetGlobalVector("_EnvironmentDepthZBufferParams",new Vector4(-2,-1,0,0)); Shader.SetGlobalTexture("_EnvironmentDepthTexture",depth);
                void SetDepth(float value) { var values=new[]{value,value,value,value}; depth.SetPixelData(values,0,0); depth.SetPixelData(values,0,1); depth.Apply(); }
                int VisiblePixels() { camera.Render(); RenderTexture.active=target; readback.ReadPixels(new Rect(0,0,64,64),0,0); readback.Apply(); int count=0; foreach(var pixel in readback.GetPixels()) if(Mathf.Min(pixel.r,Mathf.Min(pixel.g,pixel.b))<.8f) count++; return count; }
                SetDepth(.75f); Assert.That(VisiblePixels(),Is.GreaterThan(10),"Virtual content in front of the real surface must remain visible");
                SetDepth(0); Assert.That(VisiblePixels(),Is.Zero,"No outline, pigment or lettering may leak through the real surface");
                using(new RoomDepthOcclusion.VirtualCapture()) Assert.That(VisiblePixels(),Is.GreaterThan(10),"Virtual-only capture must ignore real depth");
                Assert.That(VisiblePixels(),Is.Zero,"The viewer must regain depth occlusion after capture");
                Shader.DisableKeyword("HARD_OCCLUSION"); Assert.That(VisiblePixels(),Is.GreaterThan(10),"Missing/disabled depth must not hide the world");
            }
            finally
            {
                if(hard)Shader.EnableKeyword("HARD_OCCLUSION");else Shader.DisableKeyword("HARD_OCCLUSION");
                if(soft)Shader.EnableKeyword("SOFT_OCCLUSION");else Shader.DisableKeyword("SOFT_OCCLUSION");
                Shader.SetGlobalFloat("_MaestroEnvironmentDepthBypass",previousBypass); Shader.SetGlobalTexture("_EnvironmentDepthTexture",previousDepth); Shader.SetGlobalVector("_EnvironmentDepthZBufferParams",previousParams);
                if(previousMatrices!=null&&previousMatrices.Length>0)Shader.SetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices",previousMatrices);
                RenderTexture.active=previousTarget; Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(readback); Object.DestroyImmediate(depth); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
