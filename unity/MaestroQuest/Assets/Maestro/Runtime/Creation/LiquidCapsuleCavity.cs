// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal readonly partial struct LiquidMediumGeometry {
        // Admission for a vertical capsule in an upright convex cavity. This is
        // deliberately separate from solid clearance: the caller must also sweep
        // this same capsule against retained native geometry, including the bed.
        // Endpoint bed seeds exclude a body wholly below a one-sided bed mesh.
        internal bool SurfaceCapsuleCavity(Vector3 from,Vector3 to,RoomUprightBody body,out string reason){
            reason="Surface capsule admission needs a valid body and upright current liquid";
            if(!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!body.Valid||!UprightCapsuleFrame(out float scale))return false;
            reason="The chosen habitat contains no liquid";if(Contents.amountMl<=0)return false;
            reason="Both capsule endpoints must intersect the chosen liquid";
            if(from.y+body.Bottom>=Level-.00001f||to.y+body.Bottom>=Level-.00001f)return false;
            // The wet radius grows monotonically with depth up to the equator.
            // Its maximum over this linear sweep bounds every wet cross section.
            float radius=Mathf.Max(body.WetRadius(from.y,Level),body.WetRadius(to.y,Level))/scale;
            reason="The immersed capsule leaves the chosen cavity";
            for(int end=0;end<2;end++){
                var foot=end==0?from:to;var bottom=foot+Vector3.up*body.Bottom;
                var p=inverse.MultiplyPoint3x4(bottom);
                float top=inverse.MultiplyPoint3x4(new Vector3(foot.x,Mathf.Min(Level,foot.y+body.Height),foot.z)).y;
                if(p.y<-.00001f||top>Contents.height+.00001f)return false;
                if(Contents.IsRectangular){
                    if(Mathf.Abs(p.x)+radius>Contents.rectangle.width*.5f+.000001f||
                        Mathf.Abs(p.z)+radius>Contents.rectangle.depth*.5f+.000001f)return false;
                }else if(new Vector2(p.x,p.z).magnitude+radius>Contents.radius+.000001f)return false;
                reason="The capsule starts or ends below the actual terrain bed";
                if(terrain!=null&&!terrain.Contains(p))return false;
                reason="The immersed capsule leaves the chosen cavity";
            }
            reason=null;return true;
        }
        internal bool SurfaceCapsuleFoot(RoomUprightBody body,float immersion,out float foot,out string reason){
            foot=0;reason="Shore traversal needs an upright liquid cavity and vertical gravity";
            if(!UprightCapsuleFrame(out _))return false;
            reason="The chosen habitat contains no liquid";if(Contents.amountMl<=0)return false;
            reason="Choose a valid upright body and immersion from 0.1 to 0.95";
            if(!body.FloatFoot(Level,immersion,out foot))return false;
            reason=null;return true;
        }
        bool UprightCapsuleFrame(out float scale){
            scale=0;
            if(!Item||Contents==null||!float.IsFinite(Level)||!RoomRecipe.Finite(Up)||
                Vector3.Distance(Up,Vector3.up)>.00001f||!float.IsFinite(inverse.determinant)||inverse.determinant<=1e-12f||
                Contents.TerrainFloor&&terrain==null)return false;
            var world=inverse.inverse;var x=world.MultiplyVector(Vector3.right);var y=world.MultiplyVector(Vector3.up);var z=world.MultiplyVector(Vector3.forward);
            scale=y.magnitude;
            return float.IsFinite(scale)&&scale>.00001f&&Vector3.Distance(y/scale,Vector3.up)<.00001f&&
                Mathf.Abs(x.magnitude-scale)<scale*.00001f&&Mathf.Abs(z.magnitude-scale)<scale*.00001f&&
                Mathf.Abs(Vector3.Dot(x,z))<scale*scale*.00001f;
        }
    }
}
