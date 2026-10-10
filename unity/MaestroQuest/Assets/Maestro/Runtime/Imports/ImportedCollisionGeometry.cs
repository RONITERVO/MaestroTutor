// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    // Owned collider components reuse the imported asset's exact rigid meshes.
    // Publication is explicit; there is no bounding-box fallback for a bad mesh.
    internal sealed class ImportedCollisionGeometry:IDisposable
    {
        internal Collider[] Colliders {get;}
        readonly List<RoomWalkableSurface> surfaces=new();
        readonly CollisionResources.Lease resource;
        internal ImportedCollisionGeometry(ImportedModel model,bool walkable,RoomResourceOwner owner=null)
        {
            if(!model.CollisionMeshes(out var filters,out var error))throw new ModelImportException(error);
            resource=CollisionResources.Begin(owner??new RoomResourceOwner(null,null,"unscoped"),"imported",CollisionResources.Cost.Imported(filters));
            var created=new List<Collider>();
            try
            {
                foreach(var filter in filters)
                {
                    var child=new GameObject("Imported rigid collision");child.transform.SetParent(filter.transform,false);child.SetActive(false);
                    var collider=child.AddComponent<MeshCollider>();collider.enabled=false;created.Add(collider);
                    collider.convex=false;collider.sharedMesh=filter.sharedMesh;
                    if(walkable){var surface=collider.gameObject.AddComponent<RoomWalkableSurface>();surface.enabled=false;surface.Publish(collider);surfaces.Add(surface);}
                }
                Colliders=created.ToArray();resource.Ready(Colliders);
            }
            catch {foreach(var collider in created)ArtResources.Release(collider.gameObject);resource.Retire(created);throw;}
        }
        internal void SetActive(bool active){foreach(var surface in surfaces)if(surface)surface.enabled=active;foreach(var collider in Colliders)if(collider){collider.gameObject.SetActive(active);collider.enabled=active;}}
        public void Dispose(){SetActive(false);foreach(var surface in surfaces)if(surface)surface.Publish(null);foreach(var collider in Colliders)if(collider)ArtResources.Release(collider.gameObject);resource.Retire(Colliders);}
    }
}
