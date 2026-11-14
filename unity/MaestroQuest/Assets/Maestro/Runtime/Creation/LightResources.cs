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
    // Logical native light/shadow-face admission, independent of saved quotas.
    // This does not measure Unity's internal shadow render targets or GPU memory.
    internal static class LightResources
    {
        internal const int MaximumLights=8,MaximumShadowFaces=12;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),LightId,Kind;
            internal readonly RoomResourceOwner Owner;
            internal readonly int ShadowFaces;
            internal string Phase="preparing";
            internal GameObject Root;
            internal Light Light;
            bool bound;
            internal Lease(RoomResourceOwner owner,RoomLight data){Owner=owner;LightId=data.id;Kind=data.kind;ShadowFaces=data.ShadowFaces;}
            internal void Bind(GameObject root){
                if(Phase!="preparing"||bound||!entries.Contains(this)||!root)throw new InvalidOperationException("Light preparation cannot bind this root");
                Root=root;bound=true;
            }
            internal void Complete(Light light){
                if(Phase!="preparing"||!Root||!light||light.gameObject!=Root||light.type!=(Kind=="point"?LightType.Point:LightType.Spot)||
                    (light.shadows==LightShadows.None?0:Kind=="point"?6:1)!=ShadowFaces)throw new InvalidOperationException("Native light differs from its reservation");
                Light=light;
            }
            internal void Ready(){if(Phase!="preparing"||!entries.Contains(this)||!Light)throw new InvalidOperationException("Light preparation has ended");Phase="ready";}
            public void Dispose(){
                if(Phase=="retiring")return;Phase="retiring";
                if(Light)Light.enabled=false;
                if(Root){Root.SetActive(false);ArtResources.Release(Root);}
                Collect();
            }
            internal bool Released=>bound?!Root:Phase=="retiring";
        }
        static readonly List<Lease> entries=new();
        internal static Lease Begin(RoomLight data,RoomResourceOwner owner){
            if(data==null||!DrawingSurface.Name(data.id)||data.kind is not ("point" or "spot")||data.shadows is not ("none" or "hard")||owner==null||owner.Role is not ("lighting" or "unscoped"))throw new ArgumentException("Invalid light reservation");
            Collect();
            if(entries.Count>=MaximumLights||entries.Sum(e=>e.ShadowFaces)+data.ShadowFaces>MaximumShadowFaces)
                throw new RoomResourceLimitException("The live light or shadow budget is full. Release unused lamps or wait for pending lamps to finish releasing, then retry. Saved lights are unchanged.");
            var lease=new Lease(owner,data);entries.Add(lease);return lease;
        }
        internal static void Collect(){for(int i=entries.Count-1;i>=0;i--)if(entries[i].Released)entries.RemoveAt(i);}
        internal static JObject ObserveBudget(){Collect();return new JObject{["lights"]=entries.Count,["shadowFaces"]=entries.Sum(e=>e.ShadowFaces),["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["lightLimit"]=MaximumLights,["shadowFaceLimit"]=MaximumShadowFaces};}
        internal static JObject Observe(int index){
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["light"]=e.LightId,["kind"]=e.Kind,["phase"]=e.Phase,["shadowFaces"]=e.ShadowFaces,["active"]=e.Light&&e.Light.isActiveAndEnabled};
        }
    }
}
