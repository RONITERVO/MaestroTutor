// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal sealed partial class TerrainLiquidGeometry {
        static readonly int[] SolidEdges={0,1,1,2,2,0,3,4,4,5,5,3,0,3,1,4,2,5};
        // Test the swept box against the solid below each actual bed triangle.
        // SAT includes the sweep's faces/edges, so wet endpoints cannot jump a
        // dry ridge, and independent expanded planes cannot reject a clear path.
        internal bool IntersectsSweptBox(Matrix4x4 toWorld,Vector3 from,Vector3 to,Vector3 extents){
            var toLocal=toWorld.inverse;Bounds coverage=default;bool first=true;
            for(int end=0;end<2;end++)for(int i=0;i<8;i++){
                var offset=new Vector3((i&1)==0?-extents.x:extents.x,(i&2)==0?-extents.y:extents.y,(i&4)==0?-extents.z:extents.z);
                var p=toLocal.MultiplyPoint3x4((end==0?from:to)+offset);
                if(first){coverage=new Bounds(p,Vector3.zero);first=false;}else coverage.Encapsulate(p);
            }
            int x0=Cell(coverage.min.x,bed.width),x1=Cell(coverage.max.x,bed.width);
            int z0=Cell(coverage.min.z,bed.depth),z1=Cell(coverage.max.z,bed.depth);
            // This lower face is strictly below both the body and every valid bed
            // sample. It cannot hide penetration by clipping the solid too early.
            float bottom=Mathf.Min(-1,coverage.min.y-1);
            Span<Vector3> solid=stackalloc Vector3[6];
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)for(int half=0;half<2;half++){
                int at=(z*bed.cells+x)*6+half*3;float high=float.NegativeInfinity;
                for(int i=0;i<3;i++){
                    var top=points[triangles[at+i]];high=Mathf.Max(high,top.y);
                    solid[i]=toWorld.MultiplyPoint3x4(new Vector3(top.x,bottom,top.z));
                    solid[i+3]=toWorld.MultiplyPoint3x4(top);
                }
                if(high<=coverage.min.y)continue;
                if(SweptBoxIntersectsPrism(from,to,extents,solid))return true;
            }
            return false;
        }
        int Cell(float value,float size)=>Mathf.Clamp(Mathf.FloorToInt((value/size+.5f)*bed.cells),0,bed.cells-1);
        static bool SweptBoxIntersectsPrism(Vector3 from,Vector3 to,Vector3 extents,ReadOnlySpan<Vector3> solid){
            var movement=to-from;
            for(int i=0;i<3;i++){
                var axis=i==0?Vector3.right:i==1?Vector3.up:Vector3.forward;
                if(Separates(axis,from,to,extents,solid)||Separates(Vector3.Cross(axis,movement),from,to,extents,solid))return false;
            }
            if(Separates(Vector3.Cross(solid[1]-solid[0],solid[2]-solid[0]),from,to,extents,solid)||
                Separates(Vector3.Cross(solid[4]-solid[3],solid[5]-solid[3]),from,to,extents,solid))return false;
            for(int i=0;i<3;i++){
                int next=(i+1)%3;
                if(Separates(Vector3.Cross(solid[next]-solid[i],solid[i+3]-solid[i]),from,to,extents,solid))return false;
            }
            for(int edge=0;edge<SolidEdges.Length;edge+=2){
                var direction=solid[SolidEdges[edge+1]]-solid[SolidEdges[edge]];
                for(int i=0;i<4;i++){
                    var axis=i==0?Vector3.right:i==1?Vector3.up:i==2?Vector3.forward:movement;
                    if(Separates(Vector3.Cross(axis,direction),from,to,extents,solid))return false;
                }
            }
            return true;
        }
        static bool Separates(Vector3 axis,Vector3 from,Vector3 to,Vector3 extents,ReadOnlySpan<Vector3> solid){
            float squared=axis.sqrMagnitude;if(squared<1e-16f)return false;
            float support=Mathf.Abs(axis.x)*extents.x+Mathf.Abs(axis.y)*extents.y+Mathf.Abs(axis.z)*extents.z;
            float a=Vector3.Dot(axis,from),b=Vector3.Dot(axis,to),lo=Mathf.Min(a,b)-support,hi=Mathf.Max(a,b)+support;
            float solidLo=float.PositiveInfinity,solidHi=float.NegativeInfinity;
            foreach(var point in solid){float value=Vector3.Dot(axis,point);solidLo=Mathf.Min(solidLo,value);solidHi=Mathf.Max(solidHi,value);}
            // Contact with the bed is allowed; one micrometre absorbs projection
            // roundoff, not a visible clearance allowance.
            float epsilon=1e-6f*Mathf.Sqrt(squared);
            return hi<=solidLo+epsilon||lo>=solidHi-epsilon;
        }
    }
}
