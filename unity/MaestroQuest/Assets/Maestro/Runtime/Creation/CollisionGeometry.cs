// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Owned bounded compound geometry. One root Rigidbody remains authoritative.</summary>
    internal sealed class CollisionGeometry:IDisposable
    {
        readonly GameObject root;
        readonly CollisionResources.Lease resource;
        readonly List<Mesh> meshes=new();
        readonly List<Collider> colliders=new();
        public Collider[] Colliders=>colliders.ToArray();
        public void SetActive(bool value){if(root)root.SetActive(value);}
        public CollisionGeometry(CollisionRecipe recipe,Transform parent,RoomResourceOwner owner=null) {
            if(recipe==null||!recipe.Validate(out _))throw new ArgumentException("Invalid collision recipe");
            resource=CollisionResources.Begin(owner??new RoomResourceOwner(null,null,"unscoped"),"compound",CollisionResources.Cost.Recipe(recipe));
            try{root=new GameObject("Editable collision shapes");root.SetActive(false);root.transform.SetParent(parent,false);
            foreach(var s in recipe.shapes){
                var node=new GameObject(s.id);node.transform.SetParent(root.transform,false);node.transform.SetLocalPositionAndRotation(s.position,s.rotation);node.transform.localScale=s.size;node.layer=RoomPhysicsLayers.Item;
                if(s.shape=="box")colliders.Add(node.AddComponent<BoxCollider>());
                else if(s.shape=="sphere"){var sphere=node.AddComponent<SphereCollider>();sphere.radius=.5f;colliders.Add(sphere);}
                else if(s.shape=="cylinder")AddMesh(node,Prism(Circle(s.segments)));
                else for(int i=0;i<s.segments;i++){
                    float a=2*Mathf.PI*i/s.segments,b=2*Mathf.PI*(i+1)/s.segments;
                    Vector2 Point(float radius,float angle)=>new(radius*Mathf.Cos(angle),radius*Mathf.Sin(angle));
                    var sector=new GameObject("Wall "+i);sector.transform.SetParent(node.transform,false);sector.layer=RoomPhysicsLayers.Item;
                    AddMesh(sector,Prism(new[]{Point(s.innerRadius,a),Point(.5f,a),Point(.5f,b),Point(s.innerRadius,b)}));
                }
            }resource.Ready(colliders);}catch{Dispose();throw;}
        }
        void AddMesh(GameObject node,Mesh mesh) {
            meshes.Add(mesh);var collider=node.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=mesh;colliders.Add(collider);
        }
        static Vector2[] Circle(int count){var p=new Vector2[count];for(int i=0;i<count;i++){float a=2*Mathf.PI*i/count;p[i]=new Vector2(.5f*Mathf.Cos(a),.5f*Mathf.Sin(a));}return p;}
        // Counter-clockwise X/Z footprint. Each ring sector is convex and leaves the centre empty.
        internal static Mesh Prism(Vector2[] p) {
            int count=p.Length;var v=new Vector3[count*2];var t=new List<int>();
            for(int i=0;i<count;i++){v[i]=new Vector3(p[i].x,-.5f,p[i].y);v[i+count]=new Vector3(p[i].x,.5f,p[i].y);}
            for(int i=1;i<count-1;i++){t.AddRange(new[]{0,i,i+1,count,count+i+1,count+i});}
            for(int i=0;i<count;i++){int j=(i+1)%count;t.AddRange(new[]{i,count+i,j,j,count+i,count+j});}
            var mesh=new Mesh {name="Collision convex prism",vertices=v,triangles=t.ToArray()};mesh.RecalculateBounds();return mesh;
        }
        public void Dispose(){if(root){root.SetActive(false);ArtResources.Release(root);}foreach(var mesh in meshes)ArtResources.Release(mesh);meshes.Clear();resource?.Retire(colliders);colliders.Clear();}
    }
}
