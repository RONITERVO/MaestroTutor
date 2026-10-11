// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Owns one detached procedural hierarchy until the exact edit accepts it.
    // Shared paint leases and custom meshes live with this instance; cancelling
    // preparation never alters the geometry or paint of the accepted object.
    internal sealed class RecipeVisual : IDisposable
    {
        internal readonly string Encoded;
        internal readonly RoomRecipe Source;
        internal readonly RoomResourceOwner Owner;
        internal readonly Dictionary<string,Transform> Nodes=new();
        internal readonly Dictionary<string,Quaternion> Rest=new();
        internal readonly List<RecipeMaterials.Lease> Materials=new();
        internal readonly List<Renderer> Renderers=new();
        internal readonly List<Mesh> Meshes=new();
        internal GameObject Root {get;private set;}
        internal Bounds LocalBounds {get;private set;}
        bool building,complete,ended;
        internal bool Ready=>complete&&!ended&&Root;
        internal RecipeVisual(RoomRecipe source,RoomResourceOwner owner):this(source,owner,false){}
        internal static RecipeVisual BeginPreparation(RoomRecipe source,RoomResourceOwner owner)=>new(source,owner,true);
        RecipeVisual(RoomRecipe source,RoomResourceOwner owner,bool deferred)
        {
            if(source==null)throw new ArgumentException("Invalid procedural recipe");
            // Encode once: the private copy and its exact adoption identity agree.
            Encoded=JsonUtility.ToJson(source);Source=JsonUtility.FromJson<RoomRecipe>(Encoded);
            Owner=owner??throw new ArgumentNullException(nameof(owner));
            try {
                Root=new GameObject("Recipe geometry");Root.SetActive(false);
                if(!deferred)foreach(var step in BuildSteps()){}
            }catch{Dispose();throw;}
        }
        void EnsurePreparing(){if(ended||!Root)throw new ObjectDisposedException(nameof(RecipeVisual));}
        internal IEnumerable<object> BuildSteps()
        {
            EnsurePreparing();if(building)throw new InvalidOperationException("Recipe preparation has already started");building=true;
            var validation=new RoomRecipe.Validation(Source);if(!validation.Header)throw new ArgumentException("Invalid procedural recipe");
            Bounds bounds=default;bool first=true;
            foreach (var part in Source.parts)
            {
                EnsurePreparing();if(!validation.Part(part))throw new ArgumentException("Invalid procedural recipe");
                var node = new GameObject(part.id).transform;
                node.SetParent(string.IsNullOrEmpty(part.parent) ? Root.transform : Nodes[part.parent],false);
                node.SetLocalPositionAndRotation(part.position,part.rotation); Nodes.Add(part.id,node); Rest.Add(part.id,part.rotation);
                GameObject shape;
                if(RecipeGeometry.Custom(part.shape)) {
                    shape=new GameObject(part.shape,typeof(MeshFilter),typeof(MeshRenderer));
                    // Own the object before evaluating a mesh that may fail.
                    shape.transform.SetParent(node,false);
                    var mesh=RecipeGeometry.Build(part);Meshes.Add(mesh);shape.GetComponent<MeshFilter>().sharedMesh=mesh;
                }
                else shape=GameObject.CreatePrimitive(part.shape == "sphere" ? PrimitiveType.Sphere : part.shape == "cylinder" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                shape.transform.SetParent(node,false); shape.transform.localScale = Vector3.Scale(part.size,part.shape == "cylinder" ? new Vector3(1,.5f,1) : Vector3.one);
                // One stable proxy collider belongs to the complete grabbable assembly.
                var collider = shape.GetComponent<Collider>(); if(collider){collider.enabled = false; ArtResources.Release(collider);}
                var material=RecipeMaterials.Acquire(part,Color.white);var renderer=shape.GetComponent<Renderer>();
                Materials.Add(material);Renderers.Add(renderer);renderer.sharedMaterial=material.Material;
                Book.AcousticSurface.Attach(shape, shape.GetComponent<MeshFilter>().sharedMesh);
                for (int i=0;i<8;i++)
                {
                    var corner = new Vector3((i&1)==0 ? -.5f : .5f,(i&2)==0 ? -.5f : .5f,(i&4)==0 ? -.5f : .5f);
                    var point = Root.transform.InverseTransformPoint(node.TransformPoint(Vector3.Scale(part.size,corner)));
                    if (first) { bounds = new Bounds(point,Vector3.zero); first=false; } else bounds.Encapsulate(point);
                }
                yield return null;
            }
            EnsurePreparing();if(!validation.Tracks())throw new ArgumentException("Invalid procedural recipe");
            LocalBounds=bounds;complete=true;
        }
        internal void Attach(Transform parent)
        {
            if(!Ready)throw new InvalidOperationException("Recipe preparation is incomplete or has ended");
            Root.transform.SetParent(parent,false);Root.SetActive(true);
        }
        internal void SetActive(bool active){if(active&&!Ready)throw new InvalidOperationException("Recipe preparation is incomplete or has ended");if(Root)Root.SetActive(active);}
        public void Dispose()
        {
            if(ended)return;ended=true;
            if(Root){Root.SetActive(false);Root.transform.SetParent(null,false);ArtResources.Release(Root);Root=null;}
            foreach(var material in Materials)material.Dispose();Materials.Clear();
            foreach(var mesh in Meshes)ArtResources.Release(mesh);Meshes.Clear();
            Nodes.Clear();Rest.Clear();Renderers.Clear();
        }
    }
}
