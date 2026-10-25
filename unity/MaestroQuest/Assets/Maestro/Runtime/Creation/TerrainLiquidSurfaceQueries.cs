// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal sealed partial class TerrainLiquidGeometry
    {
        // Zero-radius first wet entry through the same accepted triangle columns
        // as the rendered surface. No displaced-body tolerance or new geometry.
        internal bool FirstWetEntry(Vector3 from,Vector3 to,Vector3 normal,float level,out float fraction)
        {
            fraction=0;var change=to-from;float enter=0,exit=1;
            if(!LiquidMediumGeometry.Clip(from.x,change.x,bed.width*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.x,-change.x,bed.width*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(from.z,change.z,bed.depth*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.z,-change.z,bed.depth*.5f,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(-from.y,-change.y,0,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(from.y,change.y,Ceiling,ref enter,ref exit)||
                !LiquidMediumGeometry.Clip(Vector3.Dot(from,normal),Vector3.Dot(change,normal),level,ref enter,ref exit))return false;
            var first=from+change*enter;var last=from+change*exit;
            int x0=Cell(Mathf.Min(first.x,last.x),bed.width),x1=Cell(Mathf.Max(first.x,last.x),bed.width);
            int z0=Cell(Mathf.Min(first.z,last.z),bed.depth),z1=Cell(Mathf.Max(first.z,last.z),bed.depth);
            bool found=false;float closest=exit;
            // At most 16 x 16 cells / 512 triangles. Typical short rain segments
            // visit only their clipped cell range, and all storage is reused.
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)for(int half=0;half<2;half++){
                int index=(z*bed.cells+x)*6+half*3;
                var a=points[triangles[index]];var b=points[triangles[index+1]];var c=points[triangles[index+2]];
                float begin=enter,end=closest;
                var floor=-Vector3.Cross(b-a,c-a).normalized;
                if(!ClipColumn(floor,Vector3.Dot(floor,a),from,change,ref begin,ref end))continue;
                var side=Vector3.Cross(b-a,Vector3.up).normalized;
                if(!ClipColumn(side,Vector3.Dot(side,a),from,change,ref begin,ref end))continue;
                side=Vector3.Cross(c-b,Vector3.up).normalized;
                if(!ClipColumn(side,Vector3.Dot(side,b),from,change,ref begin,ref end))continue;
                side=Vector3.Cross(a-c,Vector3.up).normalized;
                if(!ClipColumn(side,Vector3.Dot(side,c),from,change,ref begin,ref end))continue;
                found=true;closest=begin;
            }
            fraction=closest;return found;
        }
        static bool ClipColumn(Vector3 normal,float limit,Vector3 from,Vector3 change,ref float enter,ref float exit)=>
            LiquidMediumGeometry.Clip(Vector3.Dot(normal,from),Vector3.Dot(normal,change),limit,ref enter,ref exit);
    }
}
