// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Main-thread ownership of authored collision geometry. These are source
    // counts, not PhysX memory measurements or permission to evict a live object.
    // Admission belongs before the shared save/Undo transaction, not in Reconcile.
    internal static class CollisionResources
    {
        // Independent process limits, including replacement overlap. Imported rigid
        // collision has tighter 32-mesh/50000-triangle limits than render assets.
        internal const int MaximumEntries=256,MaximumColliders=2048;
        internal const long MaximumSourceTriangles=1048576;
        internal readonly struct Cost
        {
            internal readonly int Colliders,Meshes;
            internal readonly long Triangles;
            internal Cost(int colliders,int meshes,long triangles)
            {
                if(colliders<0||meshes<0||meshes>colliders||triangles<0||meshes==0&&triangles!=0)throw new ArgumentException("Invalid collision source cost");
                Colliders=colliders;Meshes=meshes;Triangles=triangles;
            }
            internal static Cost Recipe(CollisionRecipe recipe)
            {
                int colliders=0,meshes=0;long triangles=0;
                foreach(var shape in recipe.shapes){
                    int pieces=shape.shape=="ring"?shape.segments:1;colliders+=pieces;
                    if(shape.shape=="cylinder"){meshes++;triangles+=4*shape.segments-4;}
                    else if(shape.shape=="ring"){meshes+=pieces;triangles+=12*pieces;}
                }
                return new(colliders,meshes,triangles);
            }
            internal static Cost Terrain(RoomHeightField field)=>new(1,1,2L*field.cells*field.cells+8L*field.cells+2);
            internal static Cost Imported(MeshFilter[] filters)
            {
                long triangles=0;foreach(var filter in filters)triangles+=MeshTriangles(filter.sharedMesh);
                return new(filters.Length,filters.Length,triangles);
            }
            internal static Cost Accepted(Collider[] colliders)
            {
                int count=0,meshes=0;long triangles=0;
                foreach(var collider in colliders){if(!collider)continue;count++;if(collider is MeshCollider mesh&&mesh.sharedMesh){meshes++;triangles+=MeshTriangles(mesh.sharedMesh);}}
                return new(count,meshes,triangles);
            }
            static long MeshTriangles(Mesh mesh){long triangles=0;for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=checked((long)mesh.GetIndexCount(sub)/3);return triangles;}
        }
        internal sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Kind;
            internal readonly RoomResourceOwner Owner;
            internal Cost Source;
            internal string Phase="preparing";
            internal Collider[] Colliders=Array.Empty<Collider>();
            internal readonly List<Mesh> OwnedMeshes=new();
            internal Entry(RoomResourceOwner owner,string kind,Cost source){Owner=owner;Kind=kind;Source=source;}
        }
        internal sealed class Lease
        {
            Entry resource;
            internal Lease(Entry entry)=>resource=entry;
            internal void Ready(IEnumerable<Collider> colliders)
            {
                var entry=resource;if(entry==null||!entries.Contains(entry)||entry.Phase!="preparing")throw new InvalidOperationException("Collision geometry ownership has ended");
                var accepted=colliders.ToArray();var actual=Cost.Accepted(accepted);var reserved=entry.Source;
                entry.Colliders=accepted;
                if(actual.Colliders>reserved.Colliders||actual.Meshes>reserved.Meshes||actual.Triangles>reserved.Triangles){
                    // An evaluator mismatch must never make its physical allocation
                    // invisible to admission. Keep the larger charge until rollback.
                    entry.Source=new Cost(Math.Max(actual.Colliders,reserved.Colliders),Math.Max(actual.Meshes,reserved.Meshes),Math.Max(actual.Triangles,reserved.Triangles));
                    throw new RoomResourceLimitException("Prepared collision geometry exceeded its reservation; the edit was not accepted");
                }
                entry.Source=actual;entry.Phase="ready";
            }
            internal void Own(Mesh mesh)
            {
                var entry=resource;if(entry==null||entry.Phase!="preparing"||!entries.Contains(entry))throw new InvalidOperationException("Collision preparation has ended");
                if(mesh)entry.OwnedMeshes.Add(mesh);
            }
            internal void Retire(IEnumerable<Collider> partiallyCreated=null)
            {
                var entry=resource;resource=null;if(entry==null||!entries.Contains(entry))return;
                if(partiallyCreated!=null)entry.Colliders=partiallyCreated.ToArray();
                entry.Phase="retiring";Collect();
            }
        }
        static readonly List<Entry> entries=new();
        internal static Lease Begin(RoomResourceOwner owner,string kind,Cost source)
        {
            if(owner==null||owner.Role is not ("collision" or "unscoped")||kind is not ("compound" or "terrain" or "imported"))throw new ArgumentException("Invalid collision resource owner");
            Collect();
            if(entries.Count>=MaximumEntries||entries.Sum(e=>(long)e.Source.Colliders)+source.Colliders>MaximumColliders||source.Triangles>MaximumSourceTriangles||entries.Sum(e=>e.Source.Triangles)>MaximumSourceTriangles-source.Triangles)
                throw new RoomResourceLimitException("The live collision geometry budget is full. Close unused collision geometry or wait for pending geometry to finish releasing, then retry. Saved objects are unchanged.");
            var entry=new Entry(owner,kind,source);entries.Add(entry);return new Lease(entry);
        }
        // Destroy is deferred by Unity. A replaced/closed world keeps its logical
        // collision charge until those actual components are gone. Collection also
        // notices hierarchy destruction that bypassed the wrapper's Dispose call.
        internal static void Collect()
        {
            for(int i=entries.Count-1;i>=0;i--){var entry=entries[i];if(entry.Phase=="preparing"||entry.Colliders.Any(c=>c)||entry.OwnedMeshes.Any(m=>m))continue;entry.Colliders=Array.Empty<Collider>();entry.OwnedMeshes.Clear();entries.RemoveAt(i);}
        }
        internal static JObject Observe()
        {
            Collect();return new JObject{["entries"]=entries.Count,["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),
                ["colliders"]=entries.Sum(e=>e.Source.Colliders),["meshColliders"]=entries.Sum(e=>e.Source.Meshes),["sourceTriangles"]=entries.Sum(e=>e.Source.Triangles),["activeColliders"]=entries.Sum(Active)};
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["entries"]=entries.Count,["colliders"]=entries.Sum(e=>e.Source.Colliders),["meshColliders"]=entries.Sum(e=>e.Source.Meshes),["sourceTriangles"]=entries.Sum(e=>e.Source.Triangles),
                ["entryLimit"]=MaximumEntries,["colliderLimit"]=MaximumColliders,["triangleLimit"]=MaximumSourceTriangles};
        }
        static int Active(Entry entry)=>entry.Colliders.Count(c=>c&&c.enabled&&c.gameObject.activeInHierarchy);
        internal static JObject ObserveEntry(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["leaseId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["role"]=e.Owner.Role,["kind"]=e.Kind,["phase"]=e.Phase,
                ["colliders"]=e.Source.Colliders,["meshColliders"]=e.Source.Meshes,["sourceTriangles"]=e.Source.Triangles,["activeColliders"]=Active(e)};
        }
    }
}
