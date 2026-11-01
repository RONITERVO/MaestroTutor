// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Coordinates remain editable X/Y metres. Curves use arc length at the
    // equator; the tangent origin faces -Z and the curvature centre is at +Z*r.
    internal static class DrawingSurfaceGeometry
    {
        internal const string Feature="curvedDrawingSurfaces.v1";
        internal const int MaximumRenderedPoints=2048;
        const float AngleStep=5*Mathf.Deg2Rad;
        internal static bool Valid(DrawingSurface s)=>s.Kind=="plane"?s.curvatureRadius==0:
            (s.Kind=="cylinder"||s.Kind=="sphere")&&float.IsFinite(s.curvatureRadius)&&s.curvatureRadius>=.01f&&s.curvatureRadius<=4&&
            s.width<=2*Mathf.PI*s.curvatureRadius&& (s.Kind!="sphere"||s.height<=.9f*Mathf.PI*s.curvatureRadius);
        internal static bool Contains(DrawingSurface s,Vector3 p,float radius)=>Mathf.Abs(p.x)+radius<=s.width*.5f+.000001f&&Mathf.Abs(p.y)+radius<=s.height*.5f+.000001f;
        internal static Vector3 Point(DrawingSurface s,Vector3 p,float offset=0)
        {
            if(s.Kind=="plane")return new Vector3(p.x,p.y,-offset);
            float r=s.curvatureRadius,u=p.x/r,v=s.Kind=="sphere"?p.y/r:0;
            var n=new Vector3(Mathf.Cos(v)*Mathf.Sin(u),Mathf.Sin(v),-Mathf.Cos(v)*Mathf.Cos(u));
            return new Vector3(0,s.Kind=="cylinder"?p.y:0,r)+n*(r+offset);
        }
        internal static int Segments(DrawingSurface s,Vector3 a,Vector3 b,float radius)
        {
            if(s.Kind=="plane")return 1;
            // Bound chord sag below one quarter of the ink radius, even for a
            // thin mark on a large surface. Five degrees is an additional cap.
            float angle=Mathf.Min(AngleStep,2*Mathf.Acos(1-radius*.25f/(s.curvatureRadius+radius)));
            float travel=Mathf.Abs(b.x-a.x)+(s.Kind=="sphere"?Mathf.Abs(b.y-a.y):0);
            return Mathf.Max(1,Mathf.CeilToInt(travel/(s.curvatureRadius*angle)));
        }

        internal static int PointCount(DrawingSurface s,Vector3[] points,float radius=.003f)
        {
            if(points==null||points.Length==0)return 0;
            int n=1;for(int i=1;i<points.Length;i++){n+=Segments(s,points[i-1],points[i],radius);if(n>MaximumRenderedPoints)return MaximumRenderedPoints+1;}return n;
        }
        internal static Vector3[] Path(DrawingSurface s,Vector3[] points,float radius,bool lift=true)
        {
            float offset=lift?radius:0;int count=PointCount(s,points,radius);if(count>MaximumRenderedPoints)throw new ArgumentException("Surface ink exceeds its rendered point budget");
            var path=new List<Vector3>(count);if(count==0)return path.ToArray();path.Add(Point(s,points[0],offset));
            for(int i=1;i<points.Length;i++){int steps=Segments(s,points[i-1],points[i],radius);for(int k=1;k<=steps;k++)path.Add(Point(s,Vector3.LerpUnclamped(points[i-1],points[i],(float)k/steps),offset));}return path.ToArray();
        }
        // Local direction is deliberately not normalized: t remains world-ray metres,
        // including scaled/rotated parents. Only outside-facing contacts are accepted.
        internal static bool Hit(DrawingSurface s,Vector3 origin,Vector3 direction,float maximum,float radius,out Vector3 local,out float distance)
        {
            local=default;distance=maximum;
            if(s.Kind=="plane"){
                if(direction.z<=.00001f||origin.z>0)return false;float t=-origin.z/direction.z;
                var p=origin+direction*t;if(t<0||t>maximum||!Contains(s,p,radius))return false;local=new Vector3(p.x,p.y,0);distance=t;return true;
            }
            float r=s.curvatureRadius;var o=origin-new Vector3(0,0,r);var d=direction;if(s.Kind=="cylinder"){o.y=0;d.y=0;}
            double a=Vector3.Dot(d,d),b=2*(double)Vector3.Dot(o,d),c=(double)Vector3.Dot(o,o)-(double)r*r,disc=b*b-4*a*c;
            if(a<1e-12||disc<0||c<0)return false;
            double t0=(-b-Math.Sqrt(disc))/(2*a);if(t0<0||t0>maximum)return false;
            var hit=origin+direction*(float)t0;var normal=hit-new Vector3(0,s.Kind=="cylinder"?hit.y:0,r);
            if(Vector3.Dot(normal,direction)>=-.000001f)return false;
            var uv=new Vector3(Mathf.Atan2(hit.x,r-hit.z)*r,s.Kind=="sphere"?Mathf.Asin(Mathf.Clamp(hit.y/r,-1,1))*r:hit.y,0);
            if(!Contains(s,uv,radius))return false;local=uv;distance=(float)t0;return true;
        }
    }
}
