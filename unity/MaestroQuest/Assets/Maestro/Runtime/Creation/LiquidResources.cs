// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Each admitted container owns its surface and potential stream, even empty.
    // Independent of saved quotas. This counts native owners, not simulation time.
    internal static class LiquidResources
    {
        internal const int MaximumInstances=32,StreamPoints=33;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal readonly RoomResourceOwner Owner;
            internal string Phase="preparing";
            internal ContainerFillView Host;
            internal GameObject Surface,Stream;
            internal Mesh Mesh;
            internal Material SurfaceMaterial,StreamMaterial;
            internal MeshRenderer Renderer;
            internal LineRenderer Line;
            bool complete,bound;
            internal Lease(RoomResourceOwner owner){Owner=owner;}
            internal bool Live=>Phase is "preparing" or "ready";
            internal bool Available=>Live&&complete&&Surface&&Stream&&Mesh&&SurfaceMaterial&&StreamMaterial&&Renderer&&Line;
            internal void Complete(){if(Phase!="preparing"||!Surface||!Stream||!Mesh||!SurfaceMaterial||!StreamMaterial||!Renderer||!Line)throw new InvalidOperationException("Liquid preparation is incomplete");complete=true;}
            internal void Ready(ContainerFillView view){if(!Available||Phase!="preparing"||bound||!view)throw new InvalidOperationException("Liquid ownership cannot transfer");Host=view;bound=true;Phase="ready";}
            internal bool Orphan=>Phase!="retiring"&&(bound&&!Host||complete&&!Available);
            internal bool Released=>Phase=="retiring"&&!Surface&&!Stream&&!Mesh&&!SurfaceMaterial&&!StreamMaterial;
            public void Dispose(){
                if(Phase=="retiring")return;Phase="retiring";
                if(Renderer)Renderer.enabled=false;if(Line)Line.enabled=false;if(Surface)Surface.SetActive(false);if(Stream)Stream.SetActive(false);
                ArtResources.Release(Surface);ArtResources.Release(Stream);ArtResources.Release(Mesh);ArtResources.Release(SurfaceMaterial);ArtResources.Release(StreamMaterial);Collect();
            }
        }
        static readonly List<Lease> entries=new();static bool collecting;
        internal static Lease Begin(RoomResourceOwner owner){
            if(owner==null||owner.Role is not ("liquid" or "unscoped"))throw new ArgumentException("Invalid liquid owner");Collect();
            if(entries.Count>=MaximumInstances)throw new RoomResourceLimitException("The live liquid budget is full. Release unused containers or wait for pending liquid resources to finish releasing, then retry. Saved water and objects are unchanged.");
            var lease=new Lease(owner);entries.Add(lease);return lease;
        }
        internal static void Collect(){if(collecting)return;collecting=true;try{foreach(var e in entries.ToArray())if(e.Orphan)e.Dispose();entries.RemoveAll(e=>e.Released);}finally{collecting=false;}}
        internal static JObject ObserveBudget(){Collect();return new JObject{["instances"]=entries.Count,["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["instanceLimit"]=MaximumInstances,["streamPointLimit"]=StreamPoints};}
        internal static JObject Observe(int index){Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["phase"]=e.Phase,["active"]=e.Host&&e.Host.gameObject.activeInHierarchy,["surfaceAllocated"]=(bool)e.Surface,["streamAllocated"]=(bool)e.Stream};}
    }
}
