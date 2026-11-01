// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Main-thread admission for user-created native roots. Component geometry,
    // models, images and simulation retain their independent ownership/budgets.
    internal static class NativeObjectResources
    {
        internal const int MaximumInstances=128;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal readonly RoomResourceOwner Owner;
            internal readonly RoomObjectKind Kind;
            internal string Phase="preparing";
            internal GameObject Root;
            internal bool Bound;
            internal Lease(RoomResourceOwner owner,RoomObjectKind kind){Owner=owner;Kind=kind;}
            internal void Validate(RoomObjectData data){if(data==null||(data.id??"")!=Owner.Target||data.kind!=Kind)throw new InvalidOperationException("Object identity or kind changed after preparation");}
            internal void Bind(GameObject root){
                if(Phase!="preparing"||Bound||!entries.Contains(this)||!root||entries.Any(e=>e!=this&&e.Root==root))throw new InvalidOperationException("Object preparation cannot bind this root");
                Bound=true;Root=root;
            }
            internal void Ready(){if(Phase!="preparing"||!entries.Contains(this)||!Root)throw new InvalidOperationException("Object preparation has ended");Phase="ready";}
            public void Dispose(){if(!entries.Contains(this))return;Phase="retiring";Collect();}
        }
        static readonly List<Lease> entries=new();
        internal static Lease Begin(RoomObjectData data,RoomResourceOwner owner)
        {
            if(owner==null||owner.Role is not ("object" or "unscoped")||data==null||data.IsBuiltIn||!Enum.IsDefined(typeof(RoomObjectKind),data.kind)||(data.id??"")!=owner.Target)throw new ArgumentException("Invalid native object reservation");
            Collect();if(entries.Count>=MaximumInstances)throw new RoomResourceLimitException("The live object budget is full. Close an unused object or wait for pending objects to finish releasing, then retry. Saved objects are unchanged.");
            var lease=new Lease(owner,data.kind);entries.Add(lease);return lease;
        }
        internal static void Collect(){
            // A private unbound candidate reserves a future root. Once bound,
            // destruction can release its slot even if Unity omitted OnDestroy
            // for a never-active component. Disabled roots continue to count.
            for(int i=entries.Count-1;i>=0;i--){var e=entries[i];if(e.Root||e.Phase=="preparing"&&!e.Bound)continue;entries.RemoveAt(i);}
        }
        internal static JObject ObserveBudget(){
            Collect();return new JObject{["instances"]=entries.Count,["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["instanceLimit"]=MaximumInstances};
        }
        internal static JObject Observe(int index){
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["role"]=e.Owner.Role,["kind"]=e.Kind.ToString(),["phase"]=e.Phase,["active"]=e.Root&&e.Root.activeInHierarchy};
        }
    }
}
