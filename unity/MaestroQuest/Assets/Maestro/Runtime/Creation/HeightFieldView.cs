// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>One bounded mesh/collider, rebuilt only when accepted source changes.</summary>
    public sealed class HeightFieldView:MonoBehaviour {
        GameObject surface;Mesh mesh,previewMesh;Material pigment;MeshCollider collision;string encoded;Color sourceColor,tint=Color.white;
        internal void Tint(Color color){tint=color;if(pigment){pigment.SetColor("_Color",sourceColor*tint);RoomAppearanceView.VisualsChanged(this);}}
        public Collider Collision=>collision;
        internal RoomHeightField Accepted {get;private set;}
        public Bounds WorldBounds {
            get {if(!surface||!mesh)return default;var b=mesh.bounds;var t=surface.transform;var x=t.TransformVector(Vector3.right*b.extents.x);var y=t.TransformVector(Vector3.up*b.extents.y);var z=t.TransformVector(Vector3.forward*b.extents.z);return new Bounds(t.TransformPoint(b.center),2*new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));}
        }
        public Transform Surface=>surface?surface.transform:null;
        internal void Preview(RoomHeightField field){
            if(!surface)return;if(field==null){surface.GetComponent<MeshFilter>().sharedMesh=mesh;return;}
            if(!previewMesh)previewMesh=new Mesh{name="Sculpt gesture preview"};Fill(previewMesh,field);surface.GetComponent<MeshFilter>().sharedMesh=previewMesh;
        }
        public bool Apply(RoomHeightField[] fields){
            var data=fields?.Length==1?fields[0]:null;string next=data==null?null:JsonUtility.ToJson(data);if(next==encoded)return false;encoded=next;Accepted=data?.Copy();
            if(data==null){ReleaseVisual();return true;}
            if(!data.Validate(out var error))throw new ArgumentException(error);
            if(!surface){surface=new GameObject("Editable height surface");surface.transform.SetParent(transform,false);surface.layer=RoomPhysicsLayers.Item;mesh=new Mesh{name="Bounded height surface"};surface.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=surface.AddComponent<MeshRenderer>();pigment=IllustratedMaterials.Create(data.color,.035f);pigment.SetFloat("_Shading",.28f);renderer.sharedMaterial=pigment;collision=surface.AddComponent<MeshCollider>();collision.convex=false;}
            surface.SetActive(true);surface.transform.SetLocalPositionAndRotation(data.frame.position,data.frame.rotation);sourceColor=data.color;Tint(tint);
            collision.sharedMesh=null;Fill(mesh,data);collision.sharedMesh=mesh;Preview(null);Book.AcousticSurface.Attach(surface,mesh);
            var walkable=surface.GetComponent<RoomWalkableSurface>()??surface.AddComponent<RoomWalkableSurface>();walkable.Publish(collision);return true;
        }
        static void Fill(Mesh mesh,RoomHeightField data){
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int z=0;z<=data.cells;z++)for(int x=0;x<=data.cells;x++){vertices.Add(data.Vertex(x,z));uv.Add(new Vector2((float)x/data.cells,(float)z/data.cells));}
            for(int z=0;z<data.cells;z++)for(int x=0;x<data.cells;x++){int a=z*(data.cells+1)+x,b=a+1,c=a+data.cells+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
            // Vertical skirts close visible edges down to a thin backing plane. The
            // backing is part of this component; no per-cell boxes or rigid bodies.
            var boundary=new List<int>();int n=data.cells+1;
            for(int x=0;x<data.cells;x++)boundary.Add(x);
            for(int z=0;z<data.cells;z++)boundary.Add(z*n+data.cells);
            for(int x=data.cells;x>0;x--)boundary.Add(data.cells*n+x);
            for(int z=data.cells;z>0;z--)boundary.Add(z*n);
            for(int i=0;i<boundary.Count;i++){var a=vertices[boundary[i]];var b=vertices[boundary[(i+1)%boundary.Count]];int start=vertices.Count;vertices.AddRange(new[]{a,b,new Vector3(a.x,-.002f,a.z),new Vector3(b.x,-.002f,b.z)});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});triangles.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});}
            int bottom=vertices.Count;vertices.AddRange(new[]{new Vector3(-data.width/2,-.002f,-data.depth/2),new Vector3(data.width/2,-.002f,-data.depth/2),new Vector3(-data.width/2,-.002f,data.depth/2),new Vector3(data.width/2,-.002f,data.depth/2)});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});triangles.AddRange(new[]{bottom,bottom+1,bottom+2,bottom+1,bottom+3,bottom+2});
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        void ReleaseVisual(){if(surface)surface.SetActive(false);if(collision)collision.sharedMesh=null;ArtResources.Release(mesh);ArtResources.Release(previewMesh);ArtResources.Release(pigment);if(surface)ArtResources.Release(surface);mesh=null;previewMesh=null;pigment=null;collision=null;surface=null;}
        void OnDestroy()=>ReleaseVisual();
    }
}
