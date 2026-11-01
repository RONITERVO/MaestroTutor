// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal readonly partial struct LiquidMediumGeometry {
        // A translated world-axis-aligned collision envelope. Convex cavity and
        // free-surface containment follow from every corner at both endpoints.
        // Terrain is nonconvex: its entire swept path also needs a solid-bed test.
        // This proves volume admission only, never ownership or obstacle clearance.
        internal bool ContainsBoxSweep(Vector3 from,Vector3 to,Vector3 extents,out string reason){
            reason="Habitat containment needs a finite collision envelope and current liquid";
            if(!Item||Contents==null||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!RoomRecipe.Finite(extents)||
                extents.x<=0||extents.y<=0||extents.z<=0||!float.IsFinite(Level)||!RoomRecipe.Finite(Up)||Up.sqrMagnitude<.99f||
                !float.IsFinite(inverse.determinant)||Mathf.Abs(inverse.determinant)<1e-12f)return false;
            reason="The chosen habitat contains no liquid";if(Contents.amountMl<=0)return false;
            for(int end=0;end<2;end++)for(int i=0;i<8;i++){
                var offset=new Vector3((i&1)==0?-extents.x:extents.x,(i&2)==0?-extents.y:extents.y,(i&4)==0?-extents.z:extents.z);
                var corner=(end==0?from:to)+offset;var local=inverse.MultiplyPoint3x4(corner);
                reason="The whole collision envelope must remain inside the chosen cavity";
                if(local.y<0||local.y>Contents.height||!Contents.ContainsHorizontal(local))return false;
                reason="The whole collision envelope must remain below the live liquid surface";
                if(Vector3.Dot(Up,corner)>Level)return false;
            }
            reason="The swept collision envelope intersects the actual terrain bed";
            if(terrain!=null&&terrain.IntersectsSweptBox(inverse.inverse,from,to,extents))return false;
            reason=null;return true;
        }
    }
}
