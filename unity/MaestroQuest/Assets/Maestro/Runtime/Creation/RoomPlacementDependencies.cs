// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal string[] WorldPlacementTargets(RoomViewpoint target)
        {
            bool needsGround=GetComponent<WorkspaceViewpoint>()?.NeedsAuthoredGround==true;
            if(journal==null||target==null||!target.Valid)return Array.Empty<string>();
            // Covers the largest admitted tracked-body capsule, including its
            // skin. Loading must remain valid if the user stands up meanwhile.
            var body=new Bounds(target.position+Vector3.up*1.12f,new Vector3(.4f,2.16f,.4f));
            var support=RoomGroundQuery.SupportEnvelope(target.position,.2f,.025f,.025f);
            return SpatialMovementTargets(body,needsGround?support:null);
        }
        internal sealed class MovementCandidates {
            internal readonly struct Candidate {
                internal readonly string Id;internal readonly RoomSpatialExtent Collision;internal readonly bool Ground;
                internal Candidate(string id,RoomSpatialExtent collision,bool ground){Id=id;Collision=collision;Ground=ground;}
            }
            internal readonly Candidate[] Values;
            internal int Visits {get;private set;}
            internal int BoundsReads=>Values.Length;
            internal MovementCandidates(Candidate[] values){Values=values;}
            internal string[] Targets(Bounds body,Bounds? ground,string actor=null){
                var result=new List<string>();
                foreach(var candidate in Values){
                    Visits++;if(candidate.Id==actor)continue;
                    var collision=candidate.Collision;
                    // Unknown cold geometry is required even when its artwork
                    // appears distant; visual bounds never prove clearance.
                    bool needed=!collision.Known||collision.HasBounds&&collision.Bounds.Intersects(body);
                    if(!needed&&ground.HasValue&&candidate.Ground)
                        needed=!collision.HasBounds||collision.Bounds.Intersects(ground.Value);
                    if(needed)result.Add(candidate.Id);
                }
                return result.ToArray(); // Canonical snapshot order is already ordinal.
            }
        }
        internal MovementCandidates CaptureMovementCandidates(bool includeGround){
            if(journal==null)return new(Array.Empty<MovementCandidates.Candidate>());
            var groundIds=includeGround?journal.GroundCandidates().Select(c=>c.target).ToHashSet(StringComparer.Ordinal):null;
            var candidates=new List<MovementCandidates.Candidate>();
            foreach(var id in journal.SavedObjectIds()){
                var item=Find(id);
                if(item&&item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)continue;
                candidates.Add(new(id,ReadCollisionBounds(id),groundIds?.Contains(id)==true));
            }
            return new(candidates.ToArray());
        }
        internal string[] SpatialMovementTargets(Bounds body,Bounds? ground)=>CaptureMovementCandidates(ground.HasValue).Targets(body,ground);
    }
}
