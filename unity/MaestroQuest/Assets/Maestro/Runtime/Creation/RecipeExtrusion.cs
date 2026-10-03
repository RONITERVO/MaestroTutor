// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>A simple XY outline extruded along Z; no code, Boolean engine or runtime mesh input.</summary>
    public static class RecipeExtrusion {
        public const string Feature="extrusionGeometry.v1";
        public static bool Valid(RecipePart part)=>part!=null&&part.shape=="extrude"&&part.segments==0&&RecipeOutline.Valid(part.profile,-.5f,32)&&Triangulate(part.profile)!=null;
        static double Cross(Vector2 a,Vector2 b,Vector2 c)=>(double)(b.x-a.x)*(c.y-a.y)-(double)(b.y-a.y)*(c.x-a.x);
        static List<int> Triangulate(Vector2[] points){
            const double epsilon=1e-8;var polygon=Enumerable.Range(0,points.Length).ToList();var triangles=new List<int>((points.Length-2)*3);
            // Inserted points on straight edges remain in saved source/walls; caps do not emit zero-area triangles.
            for(int i=polygon.Count-1;i>=0&&polygon.Count>3;i--)if(Math.Abs(Cross(points[polygon[(i+polygon.Count-1)%polygon.Count]],points[polygon[i]],points[polygon[(i+1)%polygon.Count]]))<=epsilon)polygon.RemoveAt(i);
            while(polygon.Count>3){bool found=false;
                for(int i=0;i<polygon.Count;i++){
                    int a=polygon[(i+polygon.Count-1)%polygon.Count],b=polygon[i],c=polygon[(i+1)%polygon.Count];if(Cross(points[a],points[b],points[c])<=epsilon)continue;
                    bool contains=false;foreach(int p in polygon){if(p==a||p==b||p==c)continue;if(Cross(points[a],points[b],points[p])>=-epsilon&&Cross(points[b],points[c],points[p])>=-epsilon&&Cross(points[c],points[a],points[p])>=-epsilon){contains=true;break;}}
                    if(contains)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);polygon.RemoveAt(i);found=true;break;
                }
                if(!found)return null;
            }
            if(Cross(points[polygon[0]],points[polygon[1]],points[polygon[2]])<=epsilon)return null;
            triangles.AddRange(polygon);return triangles;
        }
        public static Mesh Build(RecipePart part){
            if(!Valid(part))throw new ArgumentException("Use a simple counter-clockwise extrusion outline with 3–32 points");
            var p=part.profile;int count=p.Length;var caps=Triangulate(p);var vertices=new List<Vector3>(6*count);var normals=new List<Vector3>(6*count);var uv=new List<Vector2>(6*count);var indices=new List<int>(12*count-12);
            foreach(float z in new[]{-.5f,.5f})foreach(var point in p){vertices.Add(new Vector3(point.x,point.y,z));normals.Add(z<0?Vector3.back:Vector3.forward);uv.Add(point+Vector2.one*.5f);}
            for(int i=0;i<caps.Count;i+=3){indices.Add(caps[i+2]);indices.Add(caps[i+1]);indices.Add(caps[i]);indices.Add(count+caps[i]);indices.Add(count+caps[i+1]);indices.Add(count+caps[i+2]);}
            float perimeter=0,walked=0;for(int i=0;i<count;i++)perimeter+=Vector2.Distance(p[i],p[(i+1)%count]);
            for(int i=0;i<count;i++){
                var a=p[i];var b=p[(i+1)%count];int start=vertices.Count;var delta=b-a;float length=delta.magnitude;var normal=new Vector3(delta.y,-delta.x,0).normalized;
                vertices.Add(new Vector3(a.x,a.y,-.5f));vertices.Add(new Vector3(b.x,b.y,-.5f));vertices.Add(new Vector3(b.x,b.y,.5f));vertices.Add(new Vector3(a.x,a.y,.5f));for(int j=0;j<4;j++)normals.Add(normal);
                uv.Add(new Vector2(walked/perimeter,0));uv.Add(new Vector2((walked+length)/perimeter,0));uv.Add(new Vector2((walked+length)/perimeter,1));uv.Add(new Vector2(walked/perimeter,1));walked+=length;
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
            var mesh=new Mesh{name="Editable extrusion"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
