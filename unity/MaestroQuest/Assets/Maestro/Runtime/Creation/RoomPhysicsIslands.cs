// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomJournal {
        // Physical edges only: an area or a remembered arrangement is not a joint.
        internal bool PhysicalDynamic(string id)=>!items.TryGetValue(id,out var value)||value.physics!=ItemPhysics.Fixed;
        internal RoomConnection PhysicalLink(string id)=>items.TryGetValue(id,out var value)?value.connections?.FirstOrDefault(x=>x.enabled):null;
        internal IEnumerable<(string owner,RoomConnection link)> PhysicalLinks(){
            foreach(var data in items.Values)foreach(var link in data.connections??Array.Empty<RoomConnection>())
                if(link.enabled&&data.physics!=ItemPhysics.Fixed)yield return(data.id,link);
        }
    }
    internal sealed class PhysicsIslandQuery {
        internal string[] Members,Targets;
        internal readonly List<(string id,Bounds motion,Bounds ground)> Sweeps=new();
        internal RoomSpatialReadiness State=RoomSpatialReadiness.Ready,Result;internal string Error,ReadError;internal bool Checked;
        internal void Refuse(RoomSpatialReadiness state,string error){if(State!=RoomSpatialReadiness.Unavailable){State=state;Error=error;}}
    }
    public sealed partial class RoomEditor {
        bool preparingPhysicsIsland;
        bool PhysicsLinkBroken(string owner,RoomConnection link){
            var view=Find(owner)?.GetComponent<RoomConnectionView>();
            if(view)return view.Broken;
            return dormantNative.TryGetValue(owner,out var entry)&&entry.ConnectionBroken&&entry.ConnectionSource==JsonUtility.ToJson(link);
        }
        internal string[] PhysicsIslandMembers(RoomItem item){
            string id=Identity(item);if(id==null||journal==null)return Array.Empty<string>();
            var links=journal.PhysicalLinks().Where(x=>!PhysicsLinkBroken(x.owner,x.link)).Select(x=>(x.owner,other:x.link.connected)).ToArray();
            var found=new HashSet<string>(StringComparer.Ordinal){id};var pending=new Queue<string>();pending.Enqueue(id);
            while(pending.Count>0){
                var next=pending.Dequeue();
                // A fixed anchor is shared geometry, not a force path between its
                // separate moving children. Interrupting the anchor reaches both.
                if(next!=id&&!journal.PhysicalDynamic(next))continue;
                foreach(var edge in links){string peer=edge.owner==next?edge.other:edge.other==next?edge.owner:null;if(peer!=null&&found.Add(peer))pending.Enqueue(peer);}
            }
            return found.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        internal void InterruptPhysicsIsland(RoomItem item,bool discardAcceptedPose=true){
            foreach(var id in PhysicsIslandMembers(item)){
                var body=Find(id)?.GetComponent<RigidRoomItem>();if(!body)continue;
                body.ReleasePhysicsInputs();if(discardAcceptedPose)body.ResetAcceptedPhysicsPose();
            }
        }
        internal bool PhysicsIslandOwned(RoomItem item){
            foreach(var id in PhysicsIslandMembers(item)){
                // A dormant member can be prepared. An authored-motion owner cannot.
                var peer=Find(id);if(peer&&peer.GetComponent<RigidRoomItem>() is RigidRoomItem rigid&&rigid.AnimationOwned)return false;
            }
            return true;
        }
        internal PhysicsIslandQuery PhysicsInputs(RoomItem item){
            var query=new PhysicsIslandQuery{Members=PhysicsIslandMembers(item)};if(query.Members.Length==0)query.Refuse(RoomSpatialReadiness.Unavailable,"The physics object has no accepted world identity");var targets=new HashSet<string>(StringComparer.Ordinal);
            if(query.Members.Length>1)targets.UnionWith(query.Members);
            foreach(var id in query.Members){
                if(NativeEntityDormant(id)){targets.Add(id);query.Refuse(RoomSpatialReadiness.Preparing,"Preparing a connected physics member");continue;}
                var peer=Find(id);var rigid=peer?peer.GetComponent<RigidRoomItem>():null;var body=peer?peer.GetComponent<Rigidbody>():null;
                if(!peer||!peer.isActiveAndEnabled||!rigid||!rigid.isActiveAndEnabled){query.Refuse(RoomSpatialReadiness.Unavailable,"Connected physics member "+id+" is unavailable");continue;}
                if(!rigid.GeometryReady){
                    bool loading=peer.GetComponent<CreatedRoomObject>()?.ModelLoading==true;
                    query.Refuse(loading?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable,"Connected collision geometry is unavailable");continue;
                }
                if(!RoomPhysicsSweep.Bounds(peer,body,Time.fixedDeltaTime,out var motion,out var ground)){
                    query.Refuse(RoomSpatialReadiness.Unavailable,"Object collision bounds are unavailable");continue;
                }
                if(!rigid.Dynamic)continue; // A fixed anchor is geometry, not another falling actor.
                query.Sweeps.Add((id,motion,ground));targets.UnionWith(SpatialInputTargets(motion,ground,true,id));
            }
            query.Targets=targets.OrderBy(x=>x,StringComparer.Ordinal).ToArray();return query;
        }
        internal RoomSpatialReadiness PhysicsInputsReady(PhysicsIslandQuery query,out string error){
            // A query lives for one synchronous preparation/observation only.
            // Share geometry checks across its members, never across frames.
            if(query.Checked){error=query.ReadError;return query.Result;}
            query.Checked=true;query.Result=query.State;query.ReadError=query.Error;
            if(query.Result==RoomSpatialReadiness.Ready)foreach(var sweep in query.Sweeps){
                query.Result=SpatialInputsReady(query.Targets,sweep.motion,sweep.ground,true,sweep.id,out query.ReadError);
                if(query.Result!=RoomSpatialReadiness.Ready)break;
            }
            error=query.ReadError;return query.Result;
        }
        // Recheck the complete dependency set at retirement, not just at the
        // previous tick. Pending bodies are kinematic but still own their identity;
        // sleeping bodies still own support. Unknown actor sweeps cannot prove
        // that any area is safe to retire.
        void RetainPhysicsEnvironment(RoomRetentionGraph graph){
            if(!PhysicsWorld||!PhysicsWorld.Running)return;
            var visited=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in objects.Values){
                var rigid=item?item.GetComponent<RigidRoomItem>():null;
                if(!rigid||!rigid.ContactTracking)continue;
                string id=Identity(item);graph.Retain(id,RoomRetentionReason.Physics);
                if(visited.Contains(id))continue;
                var query=PhysicsInputs(item);
                foreach(var member in query.Members){visited.Add(member);graph.Retain(member,RoomRetentionReason.CollisionEnvironment);}
                foreach(var target in query.Targets)graph.Retain(target,RoomRetentionReason.CollisionEnvironment);
                if(query.State==RoomSpatialReadiness.Unavailable)graph.RetainAll(RoomRetentionReason.CollisionEnvironment);
            }
        }
        internal void PreparePhysicsIsland(RoomItem item){
            if(preparingPhysicsIsland||applying)return;
            preparingPhysicsIsland=true;
            try{
                var visited=new HashSet<string>(StringComparer.Ordinal);
                foreach(var id in PhysicsIslandMembers(item)){
                    if(visited.Contains(id))continue;var peer=Find(id);var rigid=peer?peer.GetComponent<RigidRoomItem>():null;
                    if(!rigid)continue;if(!rigid.Dynamic){rigid.ReleasePhysicsInputs();continue;}
                    var query=PhysicsInputs(peer);
                    foreach(var member in query.Members){var body=Find(member)?.GetComponent<RigidRoomItem>();if(body&&body.Dynamic&&visited.Add(member))body.PreparePhysicsInputs(query);}
                }
            }finally{preparingPhysicsIsland=false;}
        }
        internal bool PhysicsIslandReady(RoomItem item,bool joints,out string error){
            error=null;var query=PhysicsInputs(item);
            if(PhysicsInputsReady(query,out error)!=RoomSpatialReadiness.Ready)return false;
            foreach(var id in query.Members){
                var peer=Find(id);var rigid=peer?peer.GetComponent<RigidRoomItem>():null;
                if(!peer||!peer.isActiveAndEnabled||!rigid||!rigid.isActiveAndEnabled){error="Connected physics member "+id+" is unavailable";return false;}
                if(rigid.AnimationOwned){error="An animation or carried prop owns a connected member";return false;}
                if(!rigid.GeometryReady||!PhysicsWorld||!PhysicsWorld.CanSimulate(peer.transform.position,peer)){error="A connected member's physics environment is unavailable";return false;}
                if(rigid.Dynamic&&!peer.Grab.isSelected&&!rigid.CurrentInputs(query,out error))return false;
                var view=peer.GetComponent<RoomConnectionView>();
                var link=journal.PhysicalLink(id);
                if(rigid.Dynamic&&link!=null&&!PhysicsLinkBroken(id,link)&&(!view||!view.isActiveAndEnabled||joints&&!view.Active)){
                    error=view&&!string.IsNullOrEmpty(view.Error)?view.Error:"A connected joint is not ready";return false;
                }
            }
            return true;
        }
    }
}
