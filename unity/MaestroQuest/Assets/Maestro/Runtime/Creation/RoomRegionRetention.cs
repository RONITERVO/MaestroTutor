// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal RoomRetentionGraph ReadRetention()
        {
            if(journal==null)return null;
            var graph=journal.RetentionGraph();
            foreach(var target in graph.Targets){
                if(NativeEntityDormant(target)){if(graph.MissingDependency(target))graph.Retain(target,RoomRetentionReason.Unavailable);continue;}
                var item=Find(target);
                if(target is "book" or "maestro")graph.Retain(target,RoomRetentionReason.WorldOwned);
                if(!item||!item.isActiveAndEnabled){graph.Retain(target,RoomRetentionReason.Unavailable);continue;}
                if(item.Grab&&item.Grab.isSelected)graph.Retain(target,RoomRetentionReason.Held);
                var body=item.GetComponent<Rigidbody>();
                if(body&&!body.isKinematic)graph.Retain(target,RoomRetentionReason.Physics);
                if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true||item.GetComponent<RecipeObject>()?.IsPlaying==true)
                    graph.Retain(target,RoomRetentionReason.Animation);
                var model=item.GetComponent<CreatedRoomObject>();
                if(model&&model.Model&&model.Model.IsPlaying)graph.Retain(target,RoomRetentionReason.Animation);
                if(model&&regionModels.ContainsKey(target)&&!model.ModelGeometryReady)graph.Retain(target,RoomRetentionReason.Unavailable);
                if(graph.MissingDependency(target))graph.Retain(target,RoomRetentionReason.Unavailable);
            }
            // Reuse actual channel leases. This includes manual posing/recording,
            // preparing actions and grants that deliberately survive a grip.
            foreach(var owner in Ownership.Observe().owners)foreach(var claim in owner.claims)
                if(graph.Contains(claim.target))graph.Retain(claim.target,RoomRetentionReason.Ownership);
            var audio=GetComponent<WorldAudio>();
            if(audio)foreach(var target in audio.RetainedTargets())graph.Retain(target,RoomRetentionReason.Audio);
            // The current collision admission is whole-world. Until swept bounds and
            // per-area ground/water dependencies exist, no collider area can be
            // released while simulation runs (even if a body is asleep).
            if(PhysicsWorld&&PhysicsWorld.Running)graph.RetainAll(RoomRetentionReason.CollisionEnvironment);
            if(applying||RuntimeGate.Held||WriteGate.Frozen)graph.RetainAll(RoomRetentionReason.WorkspaceBusy);
            return graph;
        }
        internal JObject ObserveRegionRetention(string area)
        {
            if(area==null||!isActiveAndEnabled)return null;
            var graph=ReadRetention();var members=graph?.Members(area);if(members==null)return null;
            var reasons=RoomRetentionReason.None;int resident=0,required=0,missing=0;
            foreach(var target in members){
                var item=Find(target);if(item&&item.isActiveAndEnabled)resident++;
                var demand=graph.Reasons(target);reasons|=demand;if(demand!=RoomRetentionReason.None)required++;
                if(graph.MissingDependency(target))missing++;
            }
            return new JObject { ["id"]=area,["memberCount"]=members.Length,["residentCount"]=resident,
                ["retainedCount"]=required,["missingDependencyCount"]=missing,["reasons"]=new JArray(RoomRetentionGraph.Names(reasons)),
                ["unloadingSupported"]=false };
        }
    }
}
