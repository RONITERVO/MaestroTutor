// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    /// <summary>Immutable style variants shared by equal source/style values.
    /// Repainting replaces leases, never mutates a neighbour's material.</summary>
    internal static class AppearanceMaterials
    {
        sealed class Entry {internal string Key;internal Material Material;internal int Owners;}
        internal sealed class Lease:IDisposable
        {
            Entry entry;
            internal Material Material=>entry?.Material;
            internal Lease(string key,Material source,AppearanceStyle style,Color? basePigment) {
                if(!entries.TryGetValue(key,out entry)) {
                    entry=new Entry{Key=key,Material=Create(source,style,basePigment)};entries.Add(key,entry);
                }
                entry.Owners++;
            }
            public void Dispose(){var value=entry;if(value==null)return;entry=null;if(--value.Owners==0){entries.Remove(value.Key);ArtResources.Release(value.Material);}}
        }
        static readonly Dictionary<string,Entry> entries=new(StringComparer.Ordinal);
        internal static int LiveVariants=>entries.Count;
        internal static Lease Acquire(Material source,AppearanceStyle style,Color? basePigment=null) {
            if(!source||source.shader.name!="Maestro/Watercolor")throw new ArgumentException("An illustrated source material is required");
            if(style==null||!style.Validate(out _))throw new ArgumentException("A supported appearance is required");
            return new Lease(Key(source,style,basePigment),source,style,basePigment);
        }
        static string Key(Material source,AppearanceStyle style,Color? basePigment) {
            var b=IllustratedMaterialKey.Source(source);
            if(basePigment.HasValue){b.Append("|base");var c=basePigment.Value;IllustratedMaterialKey.Vector(b,new Vector4(c.r,c.g,c.b,c.a));}
            return b.Append('|').Append(JsonUtility.ToJson(style)).ToString();
        }
        static Material Create(Material source,AppearanceStyle style,Color? basePigment) {
            var value=new Material(source){name="Shared appearance",enableInstancing=true};
            ColorUtility.TryParseHtmlString(style.tint,out var tint);value.color=(basePigment??source.color)*tint;value.SetColor("_PatternColor",source.GetColor("_PatternColor")*tint);
            if(style.patternMode=="replace") {
                value.mainTexture=null;style.pattern.Apply(value,"box");
                value.SetVector("_PatternCoordinates",source.GetVector("_PatternCoordinates"));value.SetColor("_PatternColor",style.pattern.Color*tint);
            }
            var baseScale=source.mainTextureScale;
            value.mainTextureScale=Vector2.Scale(baseScale,style.tiling);value.mainTextureOffset=source.mainTextureOffset+Vector2.Scale(baseScale,style.offset);
            var inherited=IllustratedSurface.Imported(source,source.color);
            string mode=style.renderMode=="inherit"?inherited.Mode:style.renderMode;
            float opacity=style.renderMode=="inherit"?inherited.Opacity:style.opacity,cutoff=style.renderMode=="inherit"?inherited.Cutoff:style.cutoff;
            bool sided=style.sidedness=="inherit"?inherited.DoubleSided:style.sidedness=="both";
            if(!IllustratedSurface.TryCreate(mode,opacity,cutoff,sided,out var surface,out var error)){ArtResources.Release(value);throw new ArgumentException(error);}
            surface.Apply(value,source.GetShaderPassEnabled("PENCIL"));
            if(style.grain>=0)value.SetFloat("_Grain",style.grain);
            if(style.shading>=0)value.SetFloat("_Shading",style.shading);
            return value;
        }
    }
}
