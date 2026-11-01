// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        // Resolve before publishing the completed step's events. Ground boundaries
        // interrupt only the affected free contact/constraint group, not the world.
        internal void InterruptPhysicsBoundaries(){
            if(!this||!isActiveAndEnabled||applying||journal==null||!PhysicsWorld||!PhysicsWorld.SimulationActive||RuntimeGate.Held||Ownership.Suspended)return;
            foreach(var item in objects.Values.ToArray()){
                var source=item?item.GetComponent<RigidRoomItem>():null;
                string id=Identity(item);
                if(!source||id==null||!source.PhysicsBoundaryExceeded||!PhysicsIslandOwned(item)||recoveringBodies.ContainsKey(id))continue;
                EnsurePhysicsStepGroup(source);
                var members=PhysicsStepRecoveryMembers(id,PhysicsIslandMembers(item));
                var actors=new Dictionary<string,(RigidRoomItem body,uint placement)>(StringComparer.Ordinal);
                foreach(var member in members){
                    var peer=Find(member);var body=peer?peer.GetComponent<RigidRoomItem>():null;
                    if(!body||!body.Dynamic||!body.HasAcceptedPhysicsPose||!RecoveryActor(body,peer,body.PlacementRevision)||physicsStepCancelled.Contains(member))continue;
                    if(physicsStepActors.TryGetValue(member,out var captured)&&(captured.Body!=body||!RecoveryActor(captured.Body,peer,captured.Placement)))continue;
                    actors[member]=(body,body.PlacementRevision);
                }
                if(actors.Count==0)continue;
                RecordPhysicsGuard(source);
                // Preserve any existing group hold while combining its free actors;
                // the boundary never grants control over a held/animated neighbour.
                var previous=dormantRecoveries.Where(r=>r.Actors.Keys.Intersect(actors.Keys).Any()).ToArray();
                foreach(var old in previous){
                    foreach(var pair in old.Actors)if(RecoveryActor(pair.Value.body,Find(pair.Key),pair.Value.placement))actors[pair.Key]=pair.Value;
                    members=members.Concat(old.Members).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                    RemoveDormantRecovery(old);
                }
                var value=new PhysicsInterruption{Journal=journal,Frame=transform.localToWorldMatrix,Members=members,Targets=members,Pending=false,
                    Reason="Movement stopped at its ground boundary. Grab it, or fix the ground then pause and start physics to retry from rest."};
                foreach(var pair in actors)value.Actors[pair.Key]=pair.Value;
                // Publish the complete hold before changing any pose or joint.
                dormantRecoveries.Add(value);foreach(var member in actors.Keys)recoveringBodies[member]=value;
                foreach(var pair in actors){
                    pair.Value.body.InterruptAtCollisionGuard();pair.Value.body.RestoreAcceptedPhysicsPose();
                    value.Actors[pair.Key]=(pair.Value.body,pair.Value.body.PlacementRevision);
                }
                Physics.SyncTransforms();
                foreach(var actor in value.Actors.Values)actor.body.Refresh();
                ReportStatus(value.Reason);
            }
        }
    }
}
