// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        sealed class PhysicsStepActor {
            internal string[] Members;
            internal bool Dynamic;
        }
        sealed class PhysicsStepEvent {
            internal string Source,Other;
            internal RoomJournal Journal;
            internal int Intent;
            internal NativePhysicsObservation Observation;
        }
        readonly Dictionary<string,PhysicsStepActor> physicsStepActors=new(StringComparer.Ordinal);
        readonly Dictionary<string,HashSet<string>> physicsStepContacts=new(StringComparer.Ordinal);
        readonly HashSet<string> physicsStepGuards=new(StringComparer.Ordinal);
        readonly List<PhysicsStepEvent> physicsStepEvents=new();

        // This record is independent of normal preparation leases. A native joint
        // break may release those leases before another collision callback runs.
        internal void RememberPhysicsStep(){
            foreach(var item in objects.Values)if(item&&item.GetComponent<RigidRoomItem>() is RigidRoomItem body)RememberPhysicsStepBody(body);
        }
        internal void RememberPhysicsStepBody(RigidRoomItem body) {
            if(!body||body.PhysicsEditor!=this)return;
            string id=Identity(body.GetComponent<RoomItem>());if(id==null)return;
            var members=body.CollisionInputMembers;
            physicsStepActors[id]=new(){Members=members.Length>0?members:PhysicsIslandMembers(Find(id)),Dynamic=body.ContactTracking&&body.Simulating};
        }
        void EnsurePhysicsStepGroup(RigidRoomItem body) {
            if(!body||body.PhysicsEditor!=this)return;
            var item=body.GetComponent<RoomItem>();string id=Identity(item);if(id==null||physicsStepActors.ContainsKey(id))return;
            // Also supports explicit native simulation in diagnostics. Normal
            // FixedUpdate records each body before the solver starts.
            foreach(var member in PhysicsIslandMembers(item)) {
                if(physicsStepActors.ContainsKey(member))continue;
                var peer=Find(member)?.GetComponent<RigidRoomItem>();if(peer)RememberPhysicsStepBody(peer);
            }
        }
        internal void BeforePhysicsJointBreak(RigidRoomItem body)=>EnsurePhysicsStepGroup(body);
        internal bool PhysicsStepDynamic(RigidRoomItem body) {
            EnsurePhysicsStepGroup(body);
            string id=body?Identity(body.GetComponent<RoomItem>()):null;
            return id!=null&&physicsStepActors.TryGetValue(id,out var actor)&&actor.Dynamic;
        }
        internal string[] PhysicsStepMembers(RigidRoomItem body) {
            EnsurePhysicsStepGroup(body);
            string id=body?Identity(body.GetComponent<RoomItem>()):null;
            return id!=null&&physicsStepActors.TryGetValue(id,out var actor)?actor.Members:Array.Empty<string>();
        }
        internal void RecordPhysicsContact(RigidRoomItem body,Collider other) {
            if(!other||!PhysicsStepDynamic(body))return;
            var peer=other.attachedRigidbody?other.attachedRigidbody.GetComponent<RigidRoomItem>():null;
            if(!peer||peer.PhysicsEditor!=this||!PhysicsStepDynamic(peer))return;
            string a=Identity(body.GetComponent<RoomItem>()),b=Identity(peer.GetComponent<RoomItem>());
            if(a==null||b==null||a==b)return;
            void Link(string source,string target) {
                if(!physicsStepContacts.TryGetValue(source,out var contacts))physicsStepContacts[source]=contacts=new(StringComparer.Ordinal);
                contacts.Add(target);
            }
            Link(a,b);Link(b,a);
        }
        internal void RecordPhysicsGuard(RigidRoomItem body) {
            EnsurePhysicsStepGroup(body);string id=body?Identity(body.GetComponent<RoomItem>()):null;
            if(id!=null)physicsStepGuards.Add(id);
        }
        HashSet<string> PhysicsStepAffected() {
            var affected=new HashSet<string>(StringComparer.Ordinal);
            var pending=new Queue<string>(physicsStepGuards);
            while(pending.Count>0) {
                string id=pending.Dequeue();if(!affected.Add(id))continue;
                if(physicsStepActors.TryGetValue(id,out var actor))foreach(var member in actor.Members) {
                    // Shared fixed anchors do not transmit a solver impulse from
                    // one independently moving child to another.
                    if(physicsStepActors.TryGetValue(member,out var peer)&&peer.Dynamic&&!affected.Contains(member))pending.Enqueue(member);
                }
                if(physicsStepContacts.TryGetValue(id,out var contacts))foreach(var peer in contacts)if(!affected.Contains(peer))pending.Enqueue(peer);
            }
            return affected;
        }
        string[] PhysicsStepRecoveryMembers(string source,string[] initial) {
            var dynamicMembers=new HashSet<string>(StringComparer.Ordinal){source};
            var pending=new Queue<string>();pending.Enqueue(source);
            while(pending.Count>0) {
                string id=pending.Dequeue();
                if(physicsStepActors.TryGetValue(id,out var actor))foreach(var member in actor.Members)
                    if(physicsStepActors.TryGetValue(member,out var peer)&&peer.Dynamic&&dynamicMembers.Add(member))pending.Enqueue(member);
                if(physicsStepContacts.TryGetValue(id,out var contacts))foreach(var peer in contacts)if(dynamicMembers.Add(peer))pending.Enqueue(peer);
            }
            return initial.Concat(dynamicMembers).Concat(dynamicMembers.Where(physicsStepActors.ContainsKey).SelectMany(id=>physicsStepActors[id].Members))
                .Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        }
        internal void ObservePhysicsEvent(string source,string other,NativePhysicsObservation observation) {
            // Only observations admitted to the scheduler's bounded queue arrive
            // here. Their receivers and original payload were already captured.
            physicsStepEvents.Add(new(){Source=source,Other=other,Journal=journal,Intent=PhysicsWorld?PhysicsWorld.SimulationIntent:0,Observation=observation});
        }
        void CompletePhysicsStep() {
            var affected=PhysicsStepAffected();
            foreach(var value in physicsStepEvents) {
                if(!this||!isActiveAndEnabled||!ReferenceEquals(value.Journal,journal)||!PhysicsWorld||!PhysicsWorld.Running||
                    PhysicsWorld.SimulationIntent!=value.Intent||RuntimeGate.Held||Ownership.Suspended)value.Observation.Cancel();
                else value.Observation.Resolve(affected.Contains(value.Source)||value.Other!=null&&affected.Contains(value.Other));
            }
            physicsStepEvents.Clear();physicsStepActors.Clear();physicsStepContacts.Clear();physicsStepGuards.Clear();
        }
        void CancelPhysicsStep() {
            foreach(var value in physicsStepEvents)value.Observation.Cancel();
            physicsStepEvents.Clear();physicsStepActors.Clear();physicsStepContacts.Clear();physicsStepGuards.Clear();
        }
    }
}
