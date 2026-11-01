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
    // Logical authored-ink admission, independent of saved document quotas.
    // System artwork/selection outlines use their existing separate ownership.
    internal static class InkResources
    {
        internal const int MaximumSurfaces=128,MaximumStrokes=384,MaximumVertices=196608;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Kind,Source;
            internal readonly RoomResourceOwner Owner;
            internal long Vertices {get;private set;}
            internal string Phase="preparing";
            readonly List<UnityEngine.Object> owned=new();
            UnityEngine.Object host;bool bound;
            internal Lease(string kind,long vertices,string source,RoomResourceOwner owner){Kind=kind;Vertices=vertices;Source=source;Owner=owner;}
            internal void Own(UnityEngine.Object value){if(Phase!="preparing"||!value||owned.Contains(value))throw new InvalidOperationException("Ink preparation cannot own this resource");owned.Add(value);}
            internal void TransferMaterial(Material material,Lease destination){
                if(Phase!="ready"||destination.Phase!="preparing"||!material||!owned.Contains(material)||destination.owned.Contains(material))throw new InvalidOperationException("Ink pigment ownership changed during preparation");
                destination.owned.Add(material);owned.Remove(material);
            }
            internal void CheckMesh(Mesh mesh){
                if(Kind!="stroke"||Phase!="preparing"||!mesh||!owned.Contains(mesh))throw new InvalidOperationException("Ink preparation has no owned mesh");
                long actual=mesh.vertexCount;if(actual<=Vertices)return;
                Vertices=actual;throw new RoomResourceLimitException("Prepared ink exceeded its reserved geometry. The drawing was not replaced.");
            }
            internal void Ready(UnityEngine.Object owner){
                if(Phase!="preparing"||bound||!owner||!entries.Contains(this))throw new InvalidOperationException("Ink preparation has ended");host=owner;bound=true;Phase="ready";
            }
            internal bool Active=>host is Component c?c&&c.gameObject.activeInHierarchy:host is GameObject go&&go&&go.activeInHierarchy;
            internal bool Orphan=>bound&&!host&&Phase!="retiring";
            internal bool Released=>Phase=="retiring"&&owned.All(o=>!o);
            public void Dispose(){
                if(Phase=="retiring")return;Phase="retiring";
                foreach(var value in owned){if(value is GameObject go&&go){go.SetActive(false);NativeResourceLifetime.Release(go);}ArtResources.Release(value);}
                Collect();
            }
        }
        static readonly List<Lease> entries=new();static bool collecting;
        internal static Lease Surface(RoomResourceOwner owner,string source)=>Begin("surface",0,owner,source);
        internal static Lease Stroke(int points,RoomResourceOwner owner,string source){if(points<0||points>PencilStrokeMesh.MaximumPoints)throw new ArgumentOutOfRangeException(nameof(points));return Begin("stroke",3L*points,owner,source);}
        static Lease Begin(string kind,long vertices,RoomResourceOwner owner,string source){
            if(owner==null||owner.Role is not ("ink" or "preview" or "unscoped")||string.IsNullOrEmpty(source)||source.Length>160||source.Any(char.IsControl))throw new ArgumentException("Invalid ink reservation");
            Collect();
            if(kind=="surface"&&entries.Count(e=>e.Kind=="surface")>=MaximumSurfaces||kind=="stroke"&&(entries.Count(e=>e.Kind=="stroke")>=MaximumStrokes||entries.Sum(e=>e.Vertices)+vertices>MaximumVertices))
                throw new RoomResourceLimitException("The live drawing budget is full. Release unused ink or wait for pending ink to finish releasing, then retry. Saved drawings are unchanged.");
            var lease=new Lease(kind,vertices,source,owner);entries.Add(lease);return lease;
        }
        internal static void Collect(){
            if(collecting)return;collecting=true;
            try{
                // A never-active owner may not receive Unity's OnDestroy. Reap
                // its detached meshes/materials without dropping their charges early.
                foreach(var entry in entries.ToArray())if(entry.Orphan)entry.Dispose();
                entries.RemoveAll(e=>e.Released);
            }finally{collecting=false;}
        }
        internal static JObject ObserveBudget(){Collect();return new JObject{["surfaces"]=entries.Count(e=>e.Kind=="surface"),["strokes"]=entries.Count(e=>e.Kind=="stroke"),["vertices"]=entries.Sum(e=>e.Vertices),["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["surfaceLimit"]=MaximumSurfaces,["strokeLimit"]=MaximumStrokes,["vertexLimit"]=MaximumVertices};}
        internal static JObject Observe(int index){Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["role"]=e.Owner.Role,["source"]=e.Source,["kind"]=e.Kind,["phase"]=e.Phase,["vertices"]=e.Vertices,["active"]=e.Active};}
    }
}
