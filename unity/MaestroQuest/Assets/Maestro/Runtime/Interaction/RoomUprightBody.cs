// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>The same vertical capsule for supported and buoyant traversal.
    /// Foot is the accepted support reference; the lower cap keeps ground skin.</summary>
    internal readonly struct RoomUprightBody {
        internal readonly float Radius,Height;
        internal float Bottom=>RoomGroundQuery.Skin;
        internal float CollisionHeight=>Height-Bottom;
        internal bool Valid=>float.IsFinite(Radius)&&float.IsFinite(Height)&&Radius>0&&Height>=2*Radius+Bottom;
        internal RoomUprightBody(float radius,float height){Radius=radius;Height=height;}
        internal void Points(Vector3 foot,out Vector3 lower,out Vector3 upper){
            lower=foot+Vector3.up*(Radius+Bottom);upper=foot+Vector3.up*(Height-Radius);
        }
        internal Bounds Bounds(Vector3 foot)=>new(foot+Vector3.up*((Bottom+Height)*.5f),new Vector3(2*Radius,CollisionHeight,2*Radius));
        internal Bounds SweepBounds(Vector3 from,Vector3 to){var result=Bounds(from);result.Encapsulate(Bounds(to));return result;}
        internal bool FloatFoot(float level,float immersion,out float y){
            y=0;if(!Valid||!float.IsFinite(level)||!RoomLighting.Range(immersion,.1f,.95f))return false;
            y=level-Bottom-immersion*CollisionHeight;return float.IsFinite(y);
        }
        internal float WetRadius(float footY,float level){
            float depth=level-footY-Bottom;
            if(depth<=0)return 0;
            return depth>=Radius?Radius:Mathf.Sqrt(Mathf.Max(0,depth*(2*Radius-depth)));
        }
    }
}
