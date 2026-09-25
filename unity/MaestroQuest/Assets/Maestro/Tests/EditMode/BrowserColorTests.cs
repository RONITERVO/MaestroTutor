// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class BrowserColorTests
    {
        [Test] public void ToolLetteringIsVisibleInFrontAndOccludedBehindTheBook()
        {
            var root = new GameObject("Tool depth check");
            var readback = new Texture2D(128,128,TextureFormat.RGBA32,false,true);
            var target = new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var paper = IllustratedMaterials.Create(Color.white,0);
            var previous = RenderTexture.active;
            try
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.SetParent(root.transform,false);
                quad.GetComponent<Renderer>().sharedMaterial = paper;
                var text = new GameObject("Marking",typeof(TextMesh)).GetComponent<TextMesh>(); text.transform.SetParent(root.transform,false);
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 64; text.characterSize = .1f;
                text.anchor = TextAnchor.MiddleCenter; text.text = "M"; text.color = IllustratedMaterials.TextColor(IllustratedMaterials.Ink);
                text.GetComponent<Renderer>().sharedMaterial = IllustratedMaterials.TextMaterial(text.font);
                var camera = new GameObject("Depth camera",typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                camera.transform.position = Vector3.back*2; camera.orthographic = true; camera.orthographicSize = .5f; camera.nearClipPlane = .01f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.targetTexture = target;
                int InkPixels(float depth)
                {
                    text.transform.localPosition = new Vector3(0,0,depth); camera.Render();
                    RenderTexture.active = target; readback.ReadPixels(new Rect(0,0,128,128),0,0); readback.Apply();
                    int count = 0; foreach (var pixel in readback.GetPixels32()) if (pixel.r < 150) count++; return count;
                }
                Assert.That(InkPixels(-.1f),Is.GreaterThan(40),"Lettering in front must actually render");
                Assert.That(InkPixels(.1f),Is.Zero,"Opaque pages must hide lettering behind them");
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(root); Object.DestroyImmediate(readback); Object.DestroyImmediate(paper);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        [Test] public void RawBrowserPixelsKeepTheirSrgbColorInLinearRendering()
        {
            Assert.That(QualitySettings.activeColorSpace,Is.EqualTo(ColorSpace.Linear));
            var root = new GameObject("Browser color check");
            var pixels = new Texture2D(1,1,TextureFormat.RGBA32,false,true);
            var readback = new Texture2D(16,16,TextureFormat.RGBA32,false,true);
            var target = new RenderTexture(16,16,16,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var material = IllustratedMaterials.Create(Color.white,0);
            var previous = RenderTexture.active;
            try
            {
                var expected = new Color(.4f,.2f,.65f,1); pixels.SetPixel(0,0,expected); pixels.Apply();
                material.mainTexture = pixels; material.SetFloat("_DecodeBrowserSrgb",1);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.SetParent(root.transform,false); quad.GetComponent<Renderer>().sharedMaterial = material;
                var camera = new GameObject("Color camera",typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform,false);
                camera.transform.position = Vector3.back*2; camera.orthographic = true; camera.orthographicSize = .5f; camera.nearClipPlane = .01f;
                camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; readback.ReadPixels(new Rect(0,0,16,16),0,0); readback.Apply(); var actual = readback.GetPixel(8,8);
                Assert.That(actual.r,Is.EqualTo(expected.r).Within(.02f));
                Assert.That(actual.g,Is.EqualTo(expected.g).Within(.02f));
                Assert.That(actual.b,Is.EqualTo(expected.b).Within(.02f));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root); Object.DestroyImmediate(pixels); Object.DestroyImmediate(readback); Object.DestroyImmediate(material); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
