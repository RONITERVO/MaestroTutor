// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // One bounded physical gesture selects existing stroke IDs. Geometry is only
    // cached here; the accepted room remains unchanged until the shared edit saves.
    internal sealed class SurfaceEraseSelection
    {
        sealed class Ink {internal string Id;internal Vector3[] Path;internal Bounds Bounds;internal float Radius;}
        readonly DrawingSurface surface;
        readonly Ink[] ink;
        readonly List<string> removed=new();
        readonly float radius;
        Vector3 previous;
        bool sampled;
        internal IReadOnlyList<string> Removed=>removed;
        internal int Count=>removed.Count;
        internal SurfaceEraseSelection(DrawingSurface source,float toolRadius){
            surface=source;radius=Mathf.Max(.01f,toolRadius*2);
            ink=source.strokes.Select(s=>{var path=DrawingSurfaceGeometry.Path(source,s.points,s.radius,false);return new Ink{Id=s.id,Path=path,Bounds=BoundsOf(path),Radius=s.radius};}).ToArray();
        }
        internal bool Sample(Vector3 point){
            if(sampled){float travel=Vector3.Distance(previous,point);if(travel>.35f)return false;if(travel<.001f)return true;}
            var path=DrawingSurfaceGeometry.Path(surface,new[]{sampled?previous:point,point},radius,false);var sweep=BoundsOf(path);
            foreach(var mark in ink){
                if(removed.Contains(mark.Id))continue;var broad=mark.Bounds;broad.Expand((radius+mark.Radius)*2);if(!broad.Intersects(sweep))continue;
                bool hit=false;float limit=(radius+mark.Radius)*(radius+mark.Radius);
                for(int i=1;i<path.Length&&!hit;i++)for(int j=1;j<mark.Path.Length;j++)if(DistanceSquared(path[i-1],path[i],mark.Path[j-1],mark.Path[j])<=limit){hit=true;break;}
                if(hit)removed.Add(mark.Id);
            }
            sampled=true;previous=point;return true;
        }
        static Bounds BoundsOf(Vector3[] path){var b=new Bounds(path[0],Vector3.zero);foreach(var point in path)b.Encapsulate(point);return b;}
        // Closest points between finite 3D segments, including zero-length samples.
        static float DistanceSquared(Vector3 p,Vector3 q,Vector3 r,Vector3 s){
            var u=q-p;var v=s-r;var w=p-r;float a=Vector3.Dot(u,u),b=Vector3.Dot(u,v),c=Vector3.Dot(v,v),d=Vector3.Dot(u,w),e=Vector3.Dot(v,w),x,y;
            if(a<1e-12f){x=0;y=c<1e-12f?0:Mathf.Clamp01(e/c);}
            else if(c<1e-12f){y=0;x=Mathf.Clamp01(-d/a);}
            else {float denominator=a*c-b*b;x=denominator>1e-12f?Mathf.Clamp01((b*e-c*d)/denominator):0;y=(b*x+e)/c;if(y<0){y=0;x=Mathf.Clamp01(-d/a);}else if(y>1){y=1;x=Mathf.Clamp01((b-d)/a);}}
            return (w+u*x-v*y).sqrMagnitude;
        }
    }
}
