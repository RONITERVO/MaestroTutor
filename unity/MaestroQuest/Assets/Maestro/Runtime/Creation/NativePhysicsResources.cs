// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Authored native body/material and potential connection-joint slots. Saved
    // quotas, solver work, broadphase/contact memory and system bodies are separate.
    internal static class NativePhysicsResources
    {
        internal const int MaximumBodies=128,MaximumJoints=32;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Kind;
            internal readonly RoomResourceOwner Owner;
            readonly RoomObjectKind objectKind;
            internal string Phase="preparing";
            internal Component Host;
            internal GameObject Root;bool rootBound;
            internal Rigidbody Body;
            internal Joint Joint;
            PhysicsMaterial material;bool bound;
            internal bool Live=>Phase is "preparing" or "ready";
            internal bool CanCreateJoint=>Kind=="joint"&&Phase=="ready"&&Host&&!Joint;
            internal Lease(RoomResourceOwner owner,string kind,RoomObjectKind objectKind){Owner=owner;Kind=kind;this.objectKind=objectKind;}
            internal void ValidateBody(RoomObjectData data){if(!Live||Kind!="body"||data==null||(data.id??"")!=Owner.Target||data.kind!=objectKind)throw new InvalidOperationException("Physics body identity or kind changed after preparation");}
            internal void AttachBodyRoot(GameObject root){if(Kind!="body"||Phase!="preparing"||rootBound||!root)throw new InvalidOperationException("Physics body root cannot transfer");Root=root;rootBound=true;}
            void Bind(Component host){if(Phase!="preparing"||bound||!host||!entries.Contains(this)||rootBound&&Root!=host.gameObject||entries.Any(e=>e!=this&&e.Host==host))throw new InvalidOperationException("Physics preparation cannot bind this owner");Host=host;bound=true;Root=host.gameObject;rootBound=true;}
            internal void BindBody(Rigidbody body){if(Kind!="body")throw new InvalidOperationException("Not a body reservation");Bind(body);Body=body;}
            internal void OwnMaterial(PhysicsMaterial value){if(Kind!="body"||Phase!="preparing"||!Body||!value||material)throw new InvalidOperationException("Physics material cannot transfer to this body");material=value;}
            internal void ReadyBody(){if(Kind!="body"||Phase!="preparing"||!Body||!material)throw new InvalidOperationException("Physics body preparation is incomplete");Phase="ready";}
            internal void BindJointHost(RoomConnectionView view){if(Kind!="joint")throw new InvalidOperationException("Not a joint reservation");Bind(view);Phase="ready";}
            internal void TrackJoint(Joint value){if(!CanCreateJoint||!value||value.gameObject!=Host.gameObject||entries.Any(e=>e!=this&&e.Joint==value))throw new InvalidOperationException("A joint slot cannot own overlapping native joints");Joint=value;}
            internal bool Orphan=>(rootBound&&!Root||bound&&!Host)&&Phase!="retiring";
            internal bool Released=>Phase=="retiring"&&!Body&&!Joint&&!material;
            public void Dispose(){if(Phase=="retiring")return;Phase="retiring";ArtResources.Release(Joint);ArtResources.Release(material);Collect();}
        }
        static readonly List<Lease> entries=new();static bool collecting;
        internal static Lease Body(RoomObjectData data,RoomResourceOwner owner){
            if(data==null||data.IsBuiltIn||!Enum.IsDefined(typeof(RoomObjectKind),data.kind)||owner==null||(data.id??"")!=owner.Target)throw new ArgumentException("Invalid authored body");
            return Begin(owner,"body",data.kind);
        }
        internal static Lease Connection(RoomResourceOwner owner)=>Begin(owner,"joint",default);
        static Lease Begin(RoomResourceOwner owner,string kind,RoomObjectKind objectKind){
            if(owner==null||owner.Role is not ("physics" or "unscoped"))throw new ArgumentException("Invalid physics owner");Collect();
            if(entries.Count(e=>e.Kind==kind)>=(kind=="body"?MaximumBodies:MaximumJoints))throw new RoomResourceLimitException("The live physics "+kind+" budget is full. Release unused native content or wait for pending physics resources to finish releasing, then retry. Saved objects and connections are unchanged.");
            var lease=new Lease(owner,kind,objectKind);entries.Add(lease);return lease;
        }
        internal static void Collect(){
            if(collecting)return;collecting=true;
            try{foreach(var e in entries.ToArray())if(e.Orphan)e.Dispose();entries.RemoveAll(e=>e.Released);}finally{collecting=false;}
        }
        internal static JObject ObserveBudget(){Collect();return new JObject{["bodies"]=entries.Count(e=>e.Kind=="body"),["joints"]=entries.Count(e=>e.Kind=="joint"),["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["bodyLimit"]=MaximumBodies,["jointLimit"]=MaximumJoints};}
        internal static JObject Observe(int index){Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["kind"]=e.Kind,["phase"]=e.Phase,["active"]=e.Root&&e.Root.activeInHierarchy,["allocated"]=e.Kind=="body"?(bool)e.Body:(bool)e.Joint};}
    }
}
