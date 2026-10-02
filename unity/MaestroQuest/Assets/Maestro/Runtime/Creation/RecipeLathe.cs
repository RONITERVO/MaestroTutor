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
        public const int MaximumRoomVertices=262144;
        public static bool Valid(RecipePart part)
        {
            var p=part.profile;
            if(part.shape!="lathe")return (p==null||p.Length==0)&&part.segments==0;
            if(p==null||p.Length<3||p.Length>16||part.segments<8||part.segments>48)return false;
            double area=0;
            for(int i=0;i<p.Length;i++) {
                var a=p[i];var b=p[(i+1)%p.Length];
                if(!float.IsFinite(a.x)||!float.IsFinite(a.y)||a.x<0||a.x>.5f||a.y<-.5f||a.y>.5f||(b-a).sqrMagnitude<.000001f)return false;
                area+=(double)a.x*b.y-(double)b.x*a.y;
                for(int j=i+1;j<p.Length;j++) {
                    if(j==i+1||i==0&&j==p.Length-1)continue;
                    if(Intersects(a,b,p[j],p[(j+1)%p.Length]))return false;
                }
            }
            // Counter-clockwise, nonzero solid cross section; touching and self intersections are refused.
            return area>=.0002;
        }
        static double Cross(Vector2 a,Vector2 b,Vector2 c)=>(double)(b.x-a.x)*(c.y-a.y)-(double)(b.y-a.y)*(c.x-a.x);
        static bool Intersects(Vector2 a,Vector2 b,Vector2 c,Vector2 d) {
            const double e=1e-8;
            double u=Cross(a,b,c),v=Cross(a,b,d),w=Cross(c,d,a),x=Cross(c,d,b);
            if((u>e&&v< -e||u< -e&&v>e)&&(w>e&&x< -e||w< -e&&x>e))return true;
            bool On(Vector2 p,Vector2 q,Vector2 r)=>r.x>=Math.Min(p.x,q.x)-e&&r.x<=Math.Max(p.x,q.x)+e&&r.y>=Math.Min(p.y,q.y)-e&&r.y<=Math.Max(p.y,q.y)+e;
            return Math.Abs(u)<=e&&On(a,b,c)||Math.Abs(v)<=e&&On(a,b,d)||Math.Abs(w)<=e&&On(c,d,a)||Math.Abs(x)<=e&&On(c,d,b);
        }
        public static int VertexCost(RoomRecipe recipe) {
            int total=0;if(recipe?.parts==null)return total;
            foreach(var part in recipe.parts)if(part?.shape=="lathe"&&part.profile!=null)total+=part.profile.Length*2*(part.segments+1);
            return total;
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
