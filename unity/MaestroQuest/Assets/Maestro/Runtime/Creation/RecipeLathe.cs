// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Bounded, compiled surface-of-revolution evaluator. Coordinates are normalized by part dimensions.</summary>
    public static class RecipeLathe
    {
        public const string Feature="latheGeometry.v1";
        public static bool Valid(RecipePart part) {
            if(part.shape!="lathe")return (part.profile==null||part.profile.Length==0)&&part.segments==0;
            return part.segments>=8&&part.segments<=48&&RecipeOutline.Valid(part.profile,0,16);
        }
        public static Mesh Build(RecipePart part)
        {
            if(part==null||part.shape!="lathe"||!Valid(part))throw new ArgumentException("Use a simple counter-clockwise lathe profile and 8–48 segments.");
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var profile=part.profile;int segments=part.segments;
            for(int edge=0;edge<profile.Length;edge++) {
                var a=profile[edge];var b=profile[(edge+1)%profile.Length];if(a.x==0&&b.x==0)continue;
                int start=vertices.Count;var n=new Vector2(b.y-a.y,a.x-b.x).normalized;
                for(int j=0;j<=segments;j++) {
                    float angle=j==segments?0:2*Mathf.PI*j/segments,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                    vertices.Add(new Vector3(a.x*c,a.y,a.x*s));vertices.Add(new Vector3(b.x*c,b.y,b.x*s));
                    var normal=new Vector3(n.x*c,n.y,n.x*s);normals.Add(normal);normals.Add(normal);
                    uv.Add(new Vector2((float)j/segments,(float)edge/profile.Length));uv.Add(new Vector2((float)j/segments,(float)(edge+1)/profile.Length));
                    if(j==segments)continue;
                    int k=start+j*2;
                    if(a.x>0){triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);}
                    if(b.x>0){triangles.Add(k+1);triangles.Add(k+3);triangles.Add(k+2);}
                }
            }
            var mesh=new Mesh {name="Editable lathe"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
