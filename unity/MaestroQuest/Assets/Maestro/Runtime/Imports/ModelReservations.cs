// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Imports
{
    // Runtime leases, never authored object IDs. A disposed loader keeps its
    // reservation until its outstanding importer has drained and released assets.
    internal static class ModelReservations
    {
        internal sealed class Owner
        {
            internal readonly string World,Region,Target,Role;
            internal Owner(RoomWorldIdentity world,string target,string role)
            {
                if(world!=null&&!world.Valid)throw new ArgumentException("Invalid model world identity");
                if(role!="object"&&role!="avatar"&&role!="preview"&&role!="unscoped")throw new ArgumentException("Invalid model owner role");
                if(target!=null&&target!=""&&(target.Length>128||target.Any(char.IsControl)))throw new ArgumentException("Invalid model owner target");
                World=world?.worldId??"";Region=world?.regionId??"";Target=target??"";Role=role;
            }
        }
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Hash;
            internal readonly Owner Owner;
            internal readonly int Vertices,Pixels,Morphs;
            internal string State="loading";
            bool disposed;
            internal Lease(ModelAsset asset,Owner owner)
            {
                Hash=asset.Hash;Owner=owner;Vertices=asset.Inspection.Vertices;
                Pixels=asset.Inspection.TexturePixels;Morphs=asset.Inspection.MorphVertices;
            }
            internal void Mark(string state){lock(gate){if(!disposed)State=state;}}
            public void Dispose(){lock(gate){if(disposed)return;disposed=true;leases.Remove(this);}}
        }
        static readonly object gate=new();
        static readonly List<Lease> leases=new();
        internal static (int Models,int Vertices,int TexturePixels,int MorphVertices) Budget
        {
            get{lock(gate)return(leases.Count,leases.Sum(x=>x.Vertices),leases.Sum(x=>x.Pixels),leases.Sum(x=>x.Morphs));}
        }
        internal static Lease Reserve(ModelAsset asset,Owner owner)
        {
            lock(gate){
                var used=Budget;var info=asset.Inspection;
                if(used.Models>=ImportedModel.MaximumLiveModels||used.Vertices+(long)info.Vertices>ImportedModel.MaximumLiveVertices||
                    used.TexturePixels+(long)info.TexturePixels>ImportedModel.MaximumLiveTexturePixels||used.MorphVertices+(long)info.MorphVertices>ImportedModel.MaximumLiveMorphVertices)
                    throw new ModelImportException("The live model budget is full. Close an unused preview or remove an unneeded model, then retry after pending imports finish.");
                var lease=new Lease(asset,owner);leases.Add(lease);return lease;
            }
        }
        internal static JObject Observe(int index)
        {
            lock(gate){
                if(index<0||index>=leases.Count)return null;var value=leases[index];
                return new JObject{["reservationId"]=value.Id,["worldId"]=value.Owner.World,["regionId"]=value.Owner.Region,
                    ["target"]=value.Owner.Target,["role"]=value.Owner.Role,["modelHash"]=value.Hash,["state"]=value.State,
                    ["vertices"]=value.Vertices,["textureMiPixels"]=value.Pixels/1048576.0,["morphMillionVertices"]=value.Morphs/1000000.0};
            }
        }
    }
}
