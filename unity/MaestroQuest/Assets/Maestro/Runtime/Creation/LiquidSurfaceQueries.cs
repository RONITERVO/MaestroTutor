// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal enum LiquidSurfaceState { Clear,Submerged,Surface,Unknown }
    internal readonly struct LiquidSurfaceContact
    {
        internal readonly LiquidSurfaceState State;
        internal readonly Vector3 Point;
        internal readonly LiquidMediumGeometry Medium;
        internal readonly float Fraction;
        internal LiquidSurfaceContact(LiquidSurfaceState state,Vector3 point=default,LiquidMediumGeometry medium=default,float fraction=0)
        {State=state;Point=point;Medium=medium;Fraction=fraction;}
    }
    public sealed partial class LiquidPouring
    {
        bool surfaceContactsCaptured;
        // Reuse the measured live inventory and missing-dependency storage for
        // one synchronous batch. Do not retain a capture over an await/frame or
        // another medium capture. This never requests physics or native loading.
        internal void CaptureSurfaceContacts()
        {
            CaptureMedia(true);
            if(!float.IsFinite(Physics.gravity.sqrMagnitude)||Physics.gravity.sqrMagnitude<.01f)
                mediumQueryIssue="Liquid queries need a known gravity direction";
            surfaceContactsCaptured=true;
        }
        internal LiquidSurfaceContact QuerySurfaceContact(Vector3 from,Vector3 to)
        {
            if(!surfaceContactsCaptured||mediumQueryIssue!=null||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!float.IsFinite((to-from).sqrMagnitude))return new(LiquidSurfaceState.Unknown);
            bool found=false,submerged=false;float closest=1;Vector3 point=to;
            LiquidMediumGeometry chosen=default;
            for(int i=0;i<mediaCount;i++){
                var candidate=media[i];
                if(candidate.FirstSurfaceContact(from,to,out var hit,out float fraction,out bool suppressImpact)){
                    if(found&&(fraction>closest||fraction==closest&&!Preferred(candidate,chosen)))continue;
                    found=true;closest=fraction;point=hit;chosen=candidate;submerged=suppressImpact;
                }
            }
            // Only the prefix before the proven first wet contact matters.
            // Unknown water farther along the original segment is irrelevant.
            var prefix=new Bounds(from,Vector3.zero);prefix.Encapsulate(point);
            if(!CapturedMediumAvailability(prefix).Ready)return new(LiquidSurfaceState.Unknown);
            if(found)return new(submerged?LiquidSurfaceState.Submerged:LiquidSurfaceState.Surface,point,chosen,closest);
            return new(LiquidSurfaceState.Clear,to);
        }
        static bool Preferred(in LiquidMediumGeometry candidate,in LiquidMediumGeometry current)=>
            LiquidPath.Preferred(candidate,current);
    }
}
