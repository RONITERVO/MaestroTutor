// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Parallel-transported closed cross section along a bounded open polyline.</summary>
    public static class RecipeSweep {
        public const string Feature="sweepGeometry.v1";
        public static bool Valid(RecipePart part)=>Geometry(part,out _,out _);
        static bool Geometry(RecipePart part,out Vector3[][] rings,out Vector3[] tangents){
            rings=null;tangents=null;
            if(part==null||part.shape!="sweep"||part.segments!=0||!RecipeOutline.Valid(part.profile,-.5f,32)||RecipeExtrusion.Triangulate(part.profile)==null||part.path==null||part.path.Length<2||part.path.Length>16)return false;
            var path=part.path;if((path[0]-path[^1]).sqrMagnitude<.000001f)return false;int count=path.Length;var directions=new Vector3[count-1];
            for(int i=0;i<count;i++){
                var point=path[i];if(!RoomRecipe.Finite(point)||Mathf.Abs(point.x)>.5f||Mathf.Abs(point.y)>.5f||Mathf.Abs(point.z)>.5f)return false;
                if(i==0)continue;var delta=point-path[i-1];if(delta.sqrMagnitude<.000001f)return false;directions[i-1]=delta.normalized;
                if(i>1&&Vector3.Dot(directions[i-2],directions[i-1])<-.95f)return false;
            }
            tangents=new Vector3[count];rings=new Vector3[count][];var right=new Vector3[count];var up=new Vector3[count];
            for(int i=0;i<count;i++){
                var tangent=i==0?directions[0]:i==count-1?directions[^1]:(directions[i-1]+directions[i]).normalized;tangents[i]=tangent;
                if(i==0)right[i]=Vector3.Cross(Mathf.Abs(tangent.y)>.99f?Vector3.forward:Vector3.up,tangent).normalized;
                else {
                    var axis=Vector3.Cross(tangents[i-1],tangent);var prior=right[i-1];
                    var transported=prior+Vector3.Cross(axis,prior)+Vector3.Cross(axis,Vector3.Cross(axis,prior))/(1+Vector3.Dot(tangents[i-1],tangent));
                    right[i]=(transported-tangent*Vector3.Dot(transported,tangent)).normalized;
                }
                up[i]=Vector3.Cross(tangent,right[i]);rings[i]=new Vector3[part.profile.Length];
                for(int j=0;j<part.profile.Length;j++){
                    var p=path[i]+right[i]*part.profile[j].x+up[i]*part.profile[j].y;
                    if(!RoomRecipe.Finite(p)||Mathf.Abs(p.x)>.50001f||Mathf.Abs(p.y)>.50001f||Mathf.Abs(p.z)>.50001f)return false;
                    rings[i][j]=p;
                }
            }
            // A thick section at a tight bend can fold its side wall inside out. Refuse that source.
            for(int i=0;i<count-1;i++)for(int j=0;j<part.profile.Length;j++){
                int next=(j+1)%part.profile.Length;var edge=part.profile[next]-part.profile[j];
                var outside=(right[i]+right[i+1])*edge.y-(up[i]+up[i+1])*edge.x;
                var a=rings[i][j];var b=rings[i][next];var c=rings[i+1][next];var d=rings[i+1][j];
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outside)<=1e-10f||Vector3.Dot(Vector3.Cross(c-a,d-a),outside)<=1e-10f)return false;
            }
            return true;
        }
        public static Mesh Build(RecipePart part){
            if(!Geometry(part,out var rings,out var tangents))throw new ArgumentException("Use a simple closed sweep profile and an open 2–16 point path without reversals, folded sides or geometry outside -0.5–0.5");
            int count=part.profile.Length,last=rings.Length-1;var caps=RecipeExtrusion.Triangulate(part.profile);
            var vertices=new List<Vector3>(count*(2+4*last));var normals=new List<Vector3>(vertices.Capacity);var uv=new List<Vector2>(vertices.Capacity);var indices=new List<int>();
            foreach(int end in new[]{0,last})for(int j=0;j<count;j++){vertices.Add(rings[end][j]);normals.Add(end==0?-tangents[0]:tangents[last]);uv.Add(part.profile[j]+Vector2.one*.5f);}
            for(int j=0;j<caps.Count;j+=3){indices.Add(caps[j+2]);indices.Add(caps[j+1]);indices.Add(caps[j]);indices.Add(count+caps[j]);indices.Add(count+caps[j+1]);indices.Add(count+caps[j+2]);}
            var along=new float[rings.Length];for(int i=1;i<along.Length;i++)along[i]=along[i-1]+Vector3.Distance(part.path[i],part.path[i-1]);
            var around=new float[count+1];for(int j=1;j<=count;j++)around[j]=around[j-1]+Vector2.Distance(part.profile[j%count],part.profile[j-1]);
            for(int i=0;i<last;i++)for(int j=0;j<count;j++){
                int next=(j+1)%count,start=vertices.Count;var a=rings[i][j];var b=rings[i][next];var c=rings[i+1][next];var d=rings[i+1][j];
                var n1=Vector3.Cross(b-a,c-a).normalized;var n2=Vector3.Cross(c-a,d-a).normalized;var middle=(n1+n2).normalized;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);normals.Add(middle);normals.Add(n1);normals.Add(middle);normals.Add(n2);
                float u=around[j]/around[count],v=around[j+1]/around[count],y=along[i]/along[last],z=along[i+1]/along[last];uv.Add(new Vector2(u,y));uv.Add(new Vector2(v,y));uv.Add(new Vector2(v,z));uv.Add(new Vector2(u,z));
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
            var mesh=new Mesh{name="Editable sweep"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
