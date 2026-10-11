// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Detached accepted-surface candidate. Inactive geometry has no room parent,
    // so preparation cannot publish contacts, navigation or acoustic boundaries.
    internal sealed class HeightFieldGeometry:IDisposable
    {
        internal readonly string Encoded;
        internal GameObject Surface {get;private set;}
        internal Mesh Mesh {get;private set;}
        internal MeshCollider Collision {get;private set;}
        Material pigment;
        readonly Color color;
        readonly CollisionResources.Lease resource;
        internal HeightFieldGeometry(RoomHeightField data,RoomResourceOwner owner)
        {
            if(data==null||!data.Validate(out _))throw new ArgumentException("Invalid height surface");
            Encoded=JsonUtility.ToJson(data);color=data.color;
            resource=CollisionResources.Begin(owner,"terrain",CollisionResources.Cost.Terrain(data));
            try {
                Surface=new GameObject("Editable height surface");Surface.SetActive(false);Surface.layer=RoomPhysicsLayers.Item;
                Surface.transform.SetLocalPositionAndRotation(data.frame.position,data.frame.rotation);
                Mesh=new Mesh{name="Bounded height surface"};Fill(Mesh,data);Surface.AddComponent<MeshFilter>().sharedMesh=Mesh;
                pigment=IllustratedMaterials.Create(color,.035f);pigment.SetFloat("_Shading",.28f);Surface.AddComponent<MeshRenderer>().sharedMaterial=pigment;
                Collision=Surface.AddComponent<MeshCollider>();Collision.convex=false;Collision.sharedMesh=Mesh;
                Book.AcousticSurface.Attach(Surface,Mesh);Surface.AddComponent<RoomWalkableSurface>().Publish(Collision);
                resource.Ready(new[]{Collision});
            }catch{Dispose();throw;}
        }
        internal void Attach(Transform parent,Color tint)
        {
            if(!Surface)throw new InvalidOperationException("Height surface preparation has ended");
            Surface.transform.SetParent(parent,false);Tint(tint);Surface.SetActive(true);
        }
        internal void Tint(Color tint){if(pigment)pigment.SetColor("_Color",color*tint);}
        internal void SetActive(bool active){if(Surface)Surface.SetActive(active);}
        internal static void Fill(Mesh mesh,RoomHeightField data){
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
        public void Dispose()
        {
            if(Surface){Surface.SetActive(false);Surface.transform.SetParent(null,false);}if(Collision)Collision.sharedMesh=null;
            ArtResources.Release(Mesh);ArtResources.Release(pigment);ArtResources.Release(Surface);resource?.Retire(new[]{Collision});
            Mesh=null;pigment=null;Surface=null;Collision=null;
        }
    }
}
