// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal sealed class RoomResourceLimitException:InvalidOperationException
    {
        internal RoomResourceLimitException(string message):base(message){}
    }
    // Process-wide native ownership, independent of saved document quotas. These
    // source counts are admission bounds, not measured memory or frame time.
    internal static class RecipeResources
    {
        internal const int MaximumInstances=128,MaximumParts=512,MaximumCustomVertices=524288;
        internal sealed class Lease
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal readonly RoomResourceOwner Owner;
            internal readonly int Parts,CustomVertices;
            readonly List<UnityEngine.Object> retained=new();
            internal string Phase="preparing";
            internal GameObject Root;
            internal Lease(RoomResourceOwner owner,int parts,int vertices){Owner=owner;Parts=parts;CustomVertices=vertices;}
            internal void Track(UnityEngine.Object resource)
            {
                if(Phase!="preparing"||!entries.Contains(this))throw new InvalidOperationException("Recipe reservation is no longer preparing");
                if(resource)retained.Add(resource);
            }
            internal void Ready(GameObject root)
            {
                if(Phase!="preparing"||!entries.Contains(this)||!root)throw new InvalidOperationException("Recipe preparation has ended");
                Root=root;Phase="ready";
            }
            internal void Retire(){if(!entries.Contains(this))return;Phase="retiring";Collect();}
            internal bool Retained=>retained.Any(resource=>resource);
        }
        static readonly List<Lease> entries=new();
        internal static Lease Begin(RoomRecipe recipe,RoomResourceOwner owner)
        {
            if(owner==null||recipe==null||!new RoomRecipe.Validation(recipe).Header)throw new ArgumentException("Invalid recipe reservation");
            // Bounds precede native allocation. Use wide arithmetic even for an
            // invalid direct caller; detailed shape validation still runs per part.
            if(recipe.parts.Any(p=>p==null))throw new ArgumentException("Invalid recipe part");
            long vertices=RecipeGeometry.VertexCostWide(recipe);
            Collect();
            if(entries.Count>=MaximumInstances||entries.Sum(e=>e.Parts)+recipe.parts.Length>MaximumParts||entries.Sum(e=>(long)e.CustomVertices)+vertices>MaximumCustomVertices)
                throw new RoomResourceLimitException("The live procedural geometry budget is full. Close an unused creation or wait for pending geometry to finish releasing, then retry. Saved objects are unchanged.");
            var lease=new Lease(owner,recipe.parts.Length,(int)vertices);entries.Add(lease);return lease;
        }
        internal static void Collect()
        {
            // Disabled roots, private candidates and deferred Destroy all retain
            // their charge. Custom meshes can outlive a destroyed hierarchy.
            for(int i=entries.Count-1;i>=0;i--)if(entries[i].Phase!="preparing"&&!entries[i].Retained)entries.RemoveAt(i);
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["instances"]=entries.Count,["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),
                ["parts"]=entries.Sum(e=>e.Parts),["customVertices"]=entries.Sum(e=>e.CustomVertices),["instanceLimit"]=MaximumInstances,["partLimit"]=MaximumParts,["customVertexLimit"]=MaximumCustomVertices};
        }
        internal static JObject Observe(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["role"]=e.Owner.Role,["phase"]=e.Phase,
                ["parts"]=e.Parts,["customVertices"]=e.CustomVertices,["active"]=e.Root&&e.Root.activeInHierarchy};
        }
    }
}
