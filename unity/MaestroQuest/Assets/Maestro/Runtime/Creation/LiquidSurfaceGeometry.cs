// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal readonly partial struct LiquidMediumGeometry {
        // Clip the convex swept box by the actual free-surface plane. Its vertices
        // are endpoint corners plus intersections along box edges and translated
        // corner edges. Checking all of them proves the immersed convex hull fits
        // a convex cavity. Above-water volume still needs the caller's solid sweep.
        internal bool ContainsSurfaceBoxSweep(Vector3 from,Vector3 to,Vector3 extents,out string reason){
            reason="Surface admission needs a finite body and current liquid";
            if(!Item||Contents==null||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!RoomRecipe.Finite(extents)||
                extents.x<=0||extents.y<=0||extents.z<=0||!float.IsFinite(Level)||!RoomRecipe.Finite(Up)||Up.sqrMagnitude<.99f||
                !float.IsFinite(inverse.determinant)||Mathf.Abs(inverse.determinant)<1e-12f)return false;
            reason="The chosen habitat contains no liquid";if(Contents.amountMl<=0)return false;
            float support=Vector3.Dot(new(Mathf.Abs(Up.x),Mathf.Abs(Up.y),Mathf.Abs(Up.z)),extents);
            reason="The body must already intersect liquid; surface travel cannot cross a dry gap";
            if(Vector3.Dot(Up,from)-support>=Level-.00001f||Vector3.Dot(Up,to)-support>=Level-.00001f)return false;
            reason="The immersed collision envelope leaves the chosen cavity";
            for(int i=0;i<8;i++){
                var a=SurfaceCorner(from,extents,i);var b=SurfaceCorner(to,extents,i);
                if(!SurfaceCornerFits(a)||!SurfaceCornerFits(b)||!SurfaceEdgeFits(a,b))return false;
                for(int bit=1;bit<=4;bit<<=1)if((i&bit)==0){
                    if(!SurfaceEdgeFits(a,SurfaceCorner(from,extents,i|bit))||!SurfaceEdgeFits(b,SurfaceCorner(to,extents,i|bit)))return false;
                }
            }
            reason="The swept collision envelope intersects the actual terrain bed";
            if(terrain!=null&&terrain.IntersectsSweptBox(inverse.inverse,from,to,extents))return false;
            reason=null;return true;
        }
        static Vector3 SurfaceCorner(Vector3 centre,Vector3 extents,int index)=>centre+Vector3.Scale(extents,new((index&1)==0?-1:1,(index&2)==0?-1:1,(index&4)==0?-1:1));
        bool SurfaceCornerFits(Vector3 point)=>Vector3.Dot(Up,point)>Level||SurfacePointFits(point);
        bool SurfaceEdgeFits(Vector3 a,Vector3 b){
            float da=Vector3.Dot(Up,a)-Level,db=Vector3.Dot(Up,b)-Level;
            if((da>0)==(db>0))return true;
            return SurfacePointFits(Vector3.LerpUnclamped(a,b,da/(da-db)));
        }
        bool SurfacePointFits(Vector3 point){
            var local=inverse.MultiplyPoint3x4(point);
            return local.y>=-.00001f&&local.y<=Contents.height+.00001f&&Contents.ContainsHorizontal(local);
        }
        internal bool SurfacePosition(Vector3 root,Vector3 centreOffset,Vector3 extents,float immersion,out Vector3 position,out string reason){
            position=default;reason="Surface placement needs a finite body and an immersion fraction from 0.1 to 0.95";
            if(!Item||Contents==null||!RoomRecipe.Finite(root)||!RoomRecipe.Finite(centreOffset)||!RoomRecipe.Finite(extents)||
                extents.x<=0||extents.y<=0||extents.z<=0||!float.IsFinite(Level)||!RoomLighting.Range(immersion,.1f,.95f))return false;
            reason="The chosen habitat contains no liquid";if(Contents.amountMl<=0)return false;
            reason="Surface navigation needs vertical gravity";
            if(!RoomRecipe.Finite(Up)||Mathf.Abs(Up.x)>1e-5f||Mathf.Abs(Up.z)>1e-5f||Mathf.Abs(Up.y-1)>1e-5f)return false;
            position=root;position.y=Level-centreOffset.y+(1-2*immersion)*extents.y;
            reason=null;return RoomRecipe.Finite(position);
        }
    }
}
