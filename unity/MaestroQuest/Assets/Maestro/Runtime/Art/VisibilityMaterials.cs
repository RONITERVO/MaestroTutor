// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Art
{
    /// <summary>Room-owned presentation state. Identity deliberately differs
    /// between rooms/layers even when their current values are equal. Updating
    /// opacity does not create materials or alter saved surface definitions.</summary>
    internal sealed class VisibilityState
    {
        internal float Opacity {get;private set;}=1;
        internal bool RealDepth {get;private set;}=true;
        internal event Action Changed;
        internal void Set(float opacity,bool realDepth) {
            if(!float.IsFinite(opacity)||opacity<0||opacity>1)throw new ArgumentOutOfRangeException(nameof(opacity));
            if(Opacity==opacity&&RealDepth==realDepth)return;
            Opacity=opacity;RealDepth=realDepth;Changed?.Invoke();
        }
    }
    /// <summary>The final stage after imported and bound appearances. Variants
    /// share one source snapshot and one layer state. Only this owner changes
    /// their visibility values; source materials remain independently owned.</summary>
    internal static class VisibilityMaterials
    {
        sealed class Entry
        {
            internal string SourceKey;
            internal VisibilityState State;
            internal Material Material;
            internal int Owners;
            int queue,sourceBlend,destinationBlend,zwrite;
            bool pencil;
            string renderType;
            internal Entry(string key,Material source,VisibilityState state) {
                SourceKey=key;State=state;
                Material=new Material(source){name="Shared visibility layer",enableInstancing=true};
                queue=source.renderQueue;sourceBlend=source.GetInt("_SrcBlend");destinationBlend=source.GetInt("_DstBlend");
                zwrite=source.GetInt("_ZWrite");pencil=source.GetShaderPassEnabled("PENCIL");renderType=source.GetTag("RenderType",false,"");
                state.Changed+=Apply;Apply();
            }
            void Apply() {
                bool fade=State.Opacity<1;
                Material.SetFloat("_VisibilityOpacity",State.Opacity);
                Material.SetFloat("_VisibilityRealDepth",State.RealDepth?1:0);
                // Keep the original alpha mode/cutoff. Fading a cutout must not
                // shrink its texture mask; only its final coverage is blended.
                Material.SetInt("_SrcBlend",fade?(int)BlendMode.SrcAlpha:sourceBlend);
                Material.SetInt("_DstBlend",fade?(int)BlendMode.OneMinusSrcAlpha:destinationBlend);
                Material.SetInt("_ZWrite",fade?0:zwrite);
                Material.renderQueue=fade?(int)RenderQueue.Transparent:queue;
                Material.SetOverrideTag("RenderType",fade?"Transparent":renderType);
                Material.SetShaderPassEnabled("PENCIL",pencil&&!fade);
            }
            internal void Release(){State.Changed-=Apply;ArtResources.Release(Material);}
        }
        internal sealed class Lease:IDisposable
        {
            Entry entry;
            internal Material Material=>entry?.Material;
            internal Lease(Material source,VisibilityState state) {
                string key=IllustratedMaterialKey.Source(source).ToString();
                if(!entries.TryGetValue(state,out var variants)){variants=new(StringComparer.Ordinal);entries.Add(state,variants);}
                if(!variants.TryGetValue(key,out entry)){entry=new Entry(key,source,state);variants.Add(key,entry);}
                entry.Owners++;
            }
            public void Dispose() {
                var value=entry;if(value==null)return;entry=null;
                if(--value.Owners!=0)return;
                var variants=entries[value.State];variants.Remove(value.SourceKey);if(variants.Count==0)entries.Remove(value.State);
                value.Release();
            }
        }
        static readonly Dictionary<VisibilityState,Dictionary<string,Entry>> entries=new();
        internal static int LiveVariants {get{int count=0;foreach(var values in entries.Values)count+=values.Count;return count;}}
        internal static Lease Acquire(Material source,VisibilityState state) {
            if(!source||source.shader.name!="Maestro/Watercolor")throw new ArgumentException("An illustrated source material is required");
            if(state==null)throw new ArgumentNullException(nameof(state));
            return new Lease(source,state);
        }
    }
}
