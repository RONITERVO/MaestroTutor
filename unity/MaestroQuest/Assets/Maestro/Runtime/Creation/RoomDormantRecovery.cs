// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        sealed class DormantContact {
            internal RigidRoomItem Body;internal string Actor,Target;internal int Revision;
            internal RoomCollisionProxy Proxy;internal uint Generation;
            internal RoomJournal Journal;internal Matrix4x4 Frame;internal string[] Members;
        }
        sealed class PhysicsInterruption:IDisposable {
            internal RoomJournal Journal;internal Matrix4x4 Frame;internal string[] Members,Targets;
            internal readonly Dictionary<string,(RigidRoomItem body,uint placement)> Actors=new(StringComparer.Ordinal);
            internal RoomSpatialInputs Inputs;internal bool Pending=true,Disposed;internal string Reason;
            public void Dispose(){if(Disposed)return;Disposed=true;Inputs?.Dispose();Inputs=null;}
        }
        readonly List<DormantContact> dormantContacts=new();
        readonly List<PhysicsInterruption> dormantRecoveries=new();
        readonly Dictionary<string,PhysicsInterruption> recoveringBodies=new(StringComparer.Ordinal);
        bool processingDormantContacts;
        // PhysX callbacks only copy a bounded contact identity. Publication,
        // loading and body changes happen after all callbacks from that step.
        internal void QueueDormantContact(RigidRoomItem body,RoomCollisionProxy proxy){
            if(!this||!isActiveAndEnabled||applying||!body||body.PhysicsEditor!=this||!PhysicsStepDynamic(body)||!proxy||proxy.Owner!=this)return;
            var item=body.GetComponent<RoomItem>();if(Identity(item)==null)return;
            if(dormantContacts.Any(c=>c.Body==body&&c.Proxy==proxy))return;
            RecordPhysicsGuard(body);
            dormantContacts.Add(new(){Body=body,Actor=Identity(item),Target=proxy.Target,Revision=ObjectRevision(proxy.Target),Proxy=proxy,Generation=proxy.Generation,
                Journal=journal,Frame=transform.localToWorldMatrix,Members=PhysicsStepMembers(body)});
        }
        bool RecoveryActor(RigidRoomItem body,RoomItem item,uint placement)=>body&&item&&body.PhysicsEditor==this&&Find(Identity(item))==item&&body.PlacementRevision==placement&&body.ContactTracking&&!item.Grab.isSelected;
        bool RecoveryOwned(PhysicsInterruption value){
            if(value.Disposed||!this||!isActiveAndEnabled||!ReferenceEquals(value.Journal,journal)||RuntimeGate.Held||WriteGate.Frozen)return false;
            foreach(var pair in value.Actors)if(!RecoveryActor(pair.Value.body,Find(pair.Key),pair.Value.placement))return false;
            return true;
        }
        internal bool DormantRecoveryBlocked(RoomItem item,out bool pending,out string reason){
            pending=false;reason=null;string id=Identity(item);if(id==null||!recoveringBodies.TryGetValue(id,out var value))return false;
            pending=value.Pending;reason=value.Reason;return true;
        }
        internal string[] DormantRecoveryTargets(RoomItem item)=>Identity(item) is string id&&recoveringBodies.TryGetValue(id,out var value)?value.Targets:null;
        void RemoveDormantRecovery(PhysicsInterruption value){
            value.Dispose();dormantRecoveries.Remove(value);
            foreach(var pair in value.Actors){if(recoveringBodies.TryGetValue(pair.Key,out var found)&&ReferenceEquals(found,value))recoveringBodies.Remove(pair.Key);if(pair.Value.body)pair.Value.body.ClearCollisionInterruption();}
        }
        internal void CancelDormantRecovery(RoomItem item){
            string id=Identity(item);if(id==null)return;
            // A current constraint island follows its new owner together. A
            // transient collision edge does not grant that owner its neighbours.
            var cancelled=PhysicsIslandMembers(item).Append(id).Where(member=>member==id||journal?.PhysicalDynamic(member)==true).ToHashSet(StringComparer.Ordinal);
            CancelPhysicsStepActors(cancelled);
            foreach(var value in dormantRecoveries.ToArray()){
                foreach(var member in value.Actors.Keys.Where(cancelled.Contains).ToArray()){
                    var body=value.Actors[member].body;value.Actors.Remove(member);
                    if(recoveringBodies.TryGetValue(member,out var found)&&ReferenceEquals(found,value))recoveringBodies.Remove(member);
                    if(body)body.ClearCollisionInterruption();
                }
                if(value.Actors.Count==0)RemoveDormantRecovery(value);
            }
        }
        void ReleaseDormantRecoveries(){CancelPhysicsStep();dormantContacts.Clear();foreach(var value in dormantRecoveries.ToArray())RemoveDormantRecovery(value);}
        void RefuseRecovery(PhysicsInterruption value,string reason){
            value.Inputs?.Dispose();value.Inputs=null;value.Pending=false;
            // Keep the stopped state and recovery action within the shared short status.
            value.Reason="Movement stopped. Retry from rest: pause then start physics, or grab. "+reason;
            ReportStatus(value.Reason);
        }
        internal void ProcessDormantContacts(){
            if(processingDormantContacts||applying)return;processingDormantContacts=true;
            try{
                InterruptPhysicsBoundaries();
                var queued=dormantContacts.ToArray();dormantContacts.Clear();
                foreach(var contact in queued){
                    if(!ReferenceEquals(contact.Journal,journal)||!PhysicsWorld||!PhysicsWorld.Running||RuntimeGate.Held||Ownership.Suspended)continue;
                    var members=PhysicsStepRecoveryMembers(contact.Actor,contact.Members);
                    var actors=new Dictionary<string,(RigidRoomItem body,uint placement)>(StringComparer.Ordinal);
                    foreach(var id in members)if(PhysicsStepRecoveryActor(id,out var body))actors[id]=(body,body.PlacementRevision);
                    if(actors.Count==0||actors.Keys.All(recoveringBodies.ContainsKey))continue;
                    string actor=actors.Keys.OrderBy(id=>id,StringComparer.Ordinal).First();
                    var previous=dormantRecoveries.Where(r=>r.Members.Intersect(members).Any(id=>recoveringBodies.ContainsKey(id))).ToArray();
                    if(previous.Length>0){members=members.Concat(previous.SelectMany(r=>r.Members)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();foreach(var old in previous){foreach(var pair in old.Actors)actors[pair.Key]=pair.Value;RemoveDormantRecovery(old);}}
                    var contacts=queued.Where(c=>members.Contains(c.Actor)).ToArray();
                    var value=new PhysicsInterruption{Journal=journal,Frame=contact.Frame,Members=members,
                        Targets=members.Concat(contacts.Select(c=>c.Target)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),
                        Reason="Movement stopped at unavailable geometry; preparing nearby objects"};
                    foreach(var pair in actors)value.Actors[pair.Key]=pair.Value;
                    // Register the complete hold before refreshing any member/joint.
                    dormantRecoveries.Add(value);foreach(var id in value.Actors.Keys)recoveringBodies[id]=value;
                    foreach(var member in value.Actors.Values)member.body.InterruptAtCollisionGuard();
                    bool stale=contacts.Any(c=>!ReferenceEquals(c.Journal,journal)||ObjectRevision(c.Target)!=c.Revision||c.Frame!=transform.localToWorldMatrix||
                        CollisionGuard(c.Target) is RoomCollisionGuard guard&&(!c.Proxy||guard.Shape!=c.Proxy.GetComponent<Collider>()||c.Proxy.Generation!=c.Generation));
                    if(previous.Length>0)RefuseRecovery(value,"The connected group changed during collision recovery.");
                    else if(stale)RefuseRecovery(value,"Collision inputs changed while movement was interrupted.");
                    else value.Inputs=new RoomSpatialInputs(this,actor,()=>RecoveryOwned(value)&&value.Frame==transform.localToWorldMatrix,false,purpose:"Collision recovery");
                    foreach(var member in value.Actors.Values)member.body.Refresh();
                    if(value.Inputs!=null)ReportStatus(value.Reason);
                }
                foreach(var value in dormantRecoveries.ToArray()){
                    if(!RecoveryOwned(value)){RemoveDormantRecovery(value);continue;}
                    if(value.Inputs==null)continue;
                    if(value.Frame!=transform.localToWorldMatrix){RefuseRecovery(value,"The world moved during collision preparation.");continue;}
                    bool ready=value.Inputs.Require(value.Targets,(string[] ids,out string error)=>SpatialMovementReady(ids,null,out error),out var issue);
                    if(ready){if(value.Pending){value.Pending=false;value.Reason="Geometry ready. Movement stopped; pause then start physics, or grab the object, to resume from rest.";ReportStatus(value.Reason);}}
                    else if(value.Inputs.Pending){value.Pending=true;value.Reason="Movement stopped at unavailable geometry; preparing nearby objects";}
                    else RefuseRecovery(value,issue??"Collision preparation could not finish.");
                }
            }finally{CompletePhysicsStep();processingDormantContacts=false;}
        }
        void FixedUpdate()=>ProcessDormantContacts();
    }
}
