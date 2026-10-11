// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Art
{
    /// <summary>Bounded viewer preference layered over saved defaults. No document or
    /// material allocation occurs while blending; the leased state keeps its identity.</summary>
    internal sealed class LayerPresentation
    {
        internal readonly VisibilityState Visual=new();
        internal string StateId {get;private set;}=Guid.NewGuid().ToString("N");
        internal float Opacity {get;private set;}=1;
        internal float CurrentOpacity {get;private set;}=1;
        internal bool RealDepth {get;private set;}=true;
        internal bool Blending=>elapsed<duration;
        internal float Remaining=>Mathf.Max(0,duration-elapsed);
        float authoredOpacity=1,from=1,elapsed,duration;
        bool authoredDepth=true;
        int revision;
        internal void Synchronize(int savedRevision,float opacity,bool depth) {
            if(revision==savedRevision)return;
            revision=savedRevision;authoredOpacity=opacity;authoredDepth=depth;
            Reset(true); // Editing/Undo of the saved default supersedes an older view preference.
        }
        internal bool Reset(bool invalidate=false) {
            bool changed=Opacity!=1||CurrentOpacity!=1||!RealDepth||Blending;
            Opacity=CurrentOpacity=from=1;RealDepth=true;elapsed=duration=0;
            if(changed||invalidate)StateId=Guid.NewGuid().ToString("N");
            Apply();return changed;
        }
        internal void Set(float opacity,bool depth,float seconds) {
            if(!float.IsFinite(opacity)||opacity<0||opacity>1||!float.IsFinite(seconds)||seconds<0||seconds>30)throw new ArgumentOutOfRangeException(nameof(opacity));
            if(Opacity==opacity&&RealDepth==depth&&(!Blending||duration==seconds))return;
            from=CurrentOpacity;Opacity=opacity;RealDepth=depth;elapsed=0;
            duration=from==opacity?0:seconds;
            if(duration==0)CurrentOpacity=opacity;
            StateId=Guid.NewGuid().ToString("N");Apply();
        }
        internal void Tick(float seconds) {
            if(!Blending||!float.IsFinite(seconds)||seconds<=0)return;
            elapsed=Mathf.Min(duration,elapsed+seconds);float t=elapsed/duration;
            CurrentOpacity=elapsed==duration?Opacity:Mathf.Lerp(from,Opacity,t*t*(3-2*t));Apply();
        }
        void Apply()=>Visual.Set(authoredOpacity*CurrentOpacity,authoredDepth&&RealDepth);
    }
}
