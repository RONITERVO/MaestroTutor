// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Horizontal slice of the same expanded cavity used by live movement.
    // Candidate height is resampled against accepted ground and every returned
    // route is rechecked; the outline itself never grants movement permission.
    internal static class LiquidRouteFootprint {
        internal static void Build(in LiquidMediumGeometry medium,float radius,float height,float footHeight,List<Vector3> result){
            result.Clear();if(medium.Contents.amountMl<=0)return;
            var lines=new List<Vector3>(19);medium.RouteLines(radius,height,footHeight,lines);var points=new List<Vector2>(32);
            foreach(var line in lines)if(line.x*line.x+line.y*line.y<1e-12f&&line.z<0)return;
            for(int i=0;i<lines.Count;i++)for(int j=i+1;j<lines.Count;j++){
                var a=lines[i];var b=lines[j];float determinant=a.x*b.y-a.y*b.x;if(Mathf.Abs(determinant)<1e-8f)continue;
                var p=new Vector2((a.z*b.y-a.y*b.z)/determinant,(a.x*b.z-a.z*b.x)/determinant);
                bool inside=true;foreach(var line in lines)if(line.x*p.x+line.y*p.y>line.z+.000001f){inside=false;break;}
                if(!inside)continue;foreach(var old in points)if((old-p).sqrMagnitude<1e-10f){inside=false;break;}if(inside)points.Add(p);
            }
            if(points.Count<3)return;
            points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));var hull=new List<Vector2>(points.Count*2);
            static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[^2],hull[^1],p)<=1e-10f)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[^2],hull[^1],p)<=1e-10f)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);foreach(var p in hull)result.Add(new Vector3(p.x,footHeight,p.y));
        }
    }
}
