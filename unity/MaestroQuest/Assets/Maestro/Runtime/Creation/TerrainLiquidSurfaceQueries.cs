// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal sealed partial class TerrainLiquidGeometry
    {
        internal bool FirstWetEntry(Vector3 from,Vector3 to,Vector3 normal,float level,out float fraction){
            bool found=MeasureSegment(from,to,normal,level,out var path);fraction=path.Enter;return found;
        }
        // Walk only the grid cells crossed by the ray, then clip against the SAME
        // two terrain triangle columns used for quantity, collision and rendering.
        // At most 2*cells+3 visits, no allocation, independent of physics running.
        // This geometric calculation does not establish native geometry readiness.
        internal bool MeasureSegment(Vector3 from,Vector3 to,Vector3 normal,float level,out LiquidSegment path,LiquidIntervals intervals=null)
        {
            path=default;intervals?.Clear();var change=to-from;
            if(!RoomRecipe.Finite(from)||!RoomRecipe.Finite(to)||!RoomRecipe.Finite(change)||
                !RoomRecipe.Finite(normal)||normal.sqrMagnitude<1e-12f||!float.IsFinite(level)){intervals?.Fail();return false;}
            float enter=0,exit=1;
            if(!LiquidMediumGeometry.Clip(from.x,change.x,bed.width*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.x,-change.x,bed.width*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(from.z,change.z,bed.depth*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.z,-change.z,bed.depth*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.y,-change.y,0,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(from.y,change.y,Ceiling,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(Vector3.Dot(from,normal),Vector3.Dot(change,normal),level,ref enter,ref exit))return false;
            var first=from+change*enter;
            int x=Cell(first.x,bed.width),z=Cell(first.z,bed.depth),sx=Math.Sign(change.x),sz=Math.Sign(change.z);
            double cursor=enter,wet=0;float earliest=exit,latest=enter;int visits=0;bool found=false,complete=false;
            while(x>=0&&x<bed.cells&&z>=0&&z<bed.cells&&visits<2*bed.cells+3){
                visits++;
                double tx=Boundary(x,sx,true,from.x,change.x),tz=Boundary(z,sz,false,from.z,change.z);
                double end=Math.Max(cursor,Math.Min(exit,Math.Min(tx,tz)));
                bool one=Column(x,z,0,from,change,(float)cursor,(float)end,out float a,out float b);
                bool two=Column(x,z,1,from,change,(float)cursor,(float)end,out float c,out float d);
                if(one||two){
                    found=true;
                    if(intervals!=null){
                        if(one&&two){if(a<=c){intervals.Add(a,b);intervals.Add(c,d);}else{intervals.Add(c,d);intervals.Add(a,b);}}
                        else if(one)intervals.Add(a,b);else intervals.Add(c,d);
                    }
                    if(one){earliest=Mathf.Min(earliest,a);latest=Mathf.Max(latest,b);wet+=(double)b-a;}
                    if(two){earliest=Mathf.Min(earliest,c);latest=Mathf.Max(latest,d);wet+=(double)d-c;}
                    // A ray on the shared diagonal can belong to both triangles.
                    if(one&&two)wet-=Math.Max(0,(double)Mathf.Min(b,d)-Mathf.Max(a,c));
                }
                if(end>=exit){complete=true;break;}
                if(tx<=end)x+=sx;
                if(tz<=end)z+=sz;
                cursor=end;
            }
            if(!complete){intervals?.Fail();return false;}
            if(!found)return false;
            path=new LiquidSegment(earliest,latest,Math.Clamp(wet,0,1),visits);return true;
        }
        float Boundary(int cell,int step,bool alongX,float origin,float change){
            int edge=cell+(step>0?1:0);
            // The outer footprint already clipped enter/exit. Traverse only
            // INTERNAL edges: recomputing an outer exit here can round earlier
            // than that clip and discard an otherwise complete wet segment.
            if(step==0||edge==0||edge==bed.cells)return float.PositiveInfinity;
            // Read the mesh vertex rather than recomputing its grid coordinate
            // with a different floating-point expression.
            float position=alongX?points[edge].x:points[edge*(bed.cells+1)].z;
            return (position-origin)/change;
        }
        bool Column(int x,int z,int half,Vector3 from,Vector3 change,float enter,float exit,out float begin,out float end){
            begin=enter;end=exit;int index=(z*bed.cells+x)*6+half*3;
            var a=points[triangles[index]];var b=points[triangles[index+1]];var c=points[triangles[index+2]];
            var floor=-Vector3.Cross(b-a,c-a).normalized;
            if(!ClipColumn(floor,Vector3.Dot(floor,a),from,change,ref begin,ref end))return false;
            var side=Vector3.Cross(b-a,Vector3.up).normalized;
            if(!ClipColumn(side,Vector3.Dot(side,a),from,change,ref begin,ref end))return false;
            side=Vector3.Cross(c-b,Vector3.up).normalized;
            if(!ClipColumn(side,Vector3.Dot(side,b),from,change,ref begin,ref end))return false;
            side=Vector3.Cross(a-c,Vector3.up).normalized;
            return ClipColumn(side,Vector3.Dot(side,c),from,change,ref begin,ref end);
        }
        static bool ClipColumn(Vector3 normal,float limit,Vector3 from,Vector3 change,ref float enter,ref float exit)=>
            LiquidMediumGeometry.Clip(Vector3.Dot(normal,from),Vector3.Dot(normal,change),limit,ref enter,ref exit);
    }
}
