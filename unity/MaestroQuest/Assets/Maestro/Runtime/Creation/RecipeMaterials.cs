// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Shared materials are immutable while leased. Repainting replaces an owner's
    // lease; it never edits a material that another object may still be rendering.
    internal static class RecipeMaterials
    {
        internal readonly struct Style : IEquatable<Style>
        {
            readonly Color pigment,secondary;
            readonly string kind,plane;
            readonly int columns,rows;
            readonly bool cylinder;
            internal Style(RecipePart part,Color tint)
            {
                var pattern=part.pattern;pigment=part.color*tint;secondary=(pattern?.Color??Color.white)*tint;
                kind=pattern?.kind??"solid";plane=pattern?.plane??"uv";columns=pattern?.columns??1;rows=pattern?.rows??1;cylinder=part.shape=="cylinder";
            }
            public bool Equals(Style other)=>pigment.Equals(other.pigment)&&secondary.Equals(other.secondary)&&kind==other.kind&&plane==other.plane&&columns==other.columns&&rows==other.rows&&cylinder==other.cylinder;
            public override bool Equals(object other)=>other is Style value&&Equals(value);
            public override int GetHashCode()=>HashCode.Combine(pigment,secondary,kind,plane,columns,rows,cylinder);
            internal Material Create(RecipePart part)
            {
                var material=IllustratedMaterials.Create(pigment);
                (part.pattern??new RecipePattern()).Apply(material,part.shape);material.SetColor("_PatternColor",secondary);
                return material;
            }
        }
        internal sealed class Entry {internal Style Style;internal Material Material;internal int Owners;}
        internal sealed class Lease : IDisposable
        {
            Entry entry;
            internal Lease(Entry value){entry=value;value.Owners++;}
            internal Material Material=>entry?.Material;
            public void Dispose()
            {
                var value=entry;if(value==null)return;entry=null;
                if(--value.Owners!=0)return;
                if(entries.TryGetValue(value.Style,out var current)&&ReferenceEquals(current,value))entries.Remove(value.Style);
                ArtResources.Release(value.Material);
            }
        }
        static readonly Dictionary<Style,Entry> entries=new();
        internal static Lease Acquire(RecipePart part,Color tint)
        {
            var style=new Style(part,tint);
            if(!entries.TryGetValue(style,out var entry)||!entry.Material) {
                entry=new Entry {Style=style,Material=style.Create(part)};entries[style]=entry;
            }
            return new Lease(entry);
        }
    }
}
