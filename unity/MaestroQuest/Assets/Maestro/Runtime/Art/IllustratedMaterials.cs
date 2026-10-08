// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using System.Collections.Generic;

namespace Maestro.Quest.Art
{
    public static class IllustratedMaterials
    {
        static Texture2D pigment;
        static readonly Dictionary<Font,Material> textMaterials = new();
        static bool watchingFonts;
        public static readonly Color Paper = Hex("FFF0D2");
        public static readonly Color Ink = Hex("342D2B");
        public static readonly Color Cover = Hex("73534E");
        public static readonly Color PageBlock = Hex("E8D8B3");
        public static readonly Color Ribbon = Hex("C39950");

        public static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var result) ? result : Color.white;

        // Legacy TextMesh passes vertex colors through without sRGB decoding.
        public static Color TextColor(Color srgb) => QualitySettings.activeColorSpace == ColorSpace.Linear ? srgb.linear : srgb;

        public static Material TextMaterial(Font font)
        {
            if (textMaterials.TryGetValue(font,out var existing) && existing) return existing;
            var shader = Shader.Find("Maestro/WorldText");
            if (!shader) throw new System.InvalidOperationException("The world text shader is missing.");
            var material = new Material(shader) { name = "Depth-tested tool lettering", mainTexture = font.material.mainTexture };
            textMaterials[font] = material;
            if (!watchingFonts) { Font.textureRebuilt += UpdateTextAtlas; watchingFonts = true; }
            return material;
        }

        static void UpdateTextAtlas(Font font)
        {
            if (textMaterials.TryGetValue(font,out var material) && material) material.mainTexture = font.material.mainTexture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTextMaterials()
        {
            Font.textureRebuilt -= UpdateTextAtlas; watchingFonts = false;
            foreach (var material in textMaterials.Values) ArtResources.Release(material);
            textMaterials.Clear();
        }

        // Built-in controls remain readable even when an authored scene has no light.
        public static Material CreateControl(Color color, float grain = .07f)
        {
            var material=Create(color,grain);material.SetFloat("_WorldLighting",0);return material;
        }

        public static Material Create(Color color, float grain = .07f)
        {
            var shader = Shader.Find("Maestro/Watercolor");
            if (shader == null) throw new System.InvalidOperationException("Maestro watercolor shader was not included in the build.");
            var material = new Material(shader) { name = "Watercolor " + ColorUtility.ToHtmlStringRGB(color), enableInstancing = true };
            material.SetColor("_Color", color);
            material.SetFloat("_Grain", grain);
            material.SetFloat("_Shading", grain > 0 ? .12f : 0);
            material.SetTexture("_PigmentTex", Pigment());
            material.SetShaderPassEnabled("PENCIL", grain > 0);
            return material;
        }

        static Texture2D Pigment()
        {
            if (pigment) return pigment;
            const int size = 128;
            var pixels = new Color32[size * size];
            uint random = 71;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                random = unchecked(random * 1664525u + 1013904223u);
                float dryBrush = Mathf.Sin((x + y * .37f) * .9f);
                float pooled = Mathf.Sin(x * .095f) * Mathf.Cos(y * .072f);
                byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(229 + pooled * 12 + dryBrush * 5 + (random >> 8) / 16777216f * 9), 0, 255);
                pixels[y * size + x] = new Color32(value, value, value, 255);
            }
            pigment = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Shared dry watercolor pigment", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 1 };
            pigment.SetPixels32(pixels); pigment.Apply(true, true);
            return pigment;
        }
    }
}
