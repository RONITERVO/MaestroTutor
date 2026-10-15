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
        internal string[] SpatialMovementTargets(Bounds body,Bounds? ground)
        {
            if(journal==null)return Array.Empty<string>();
            var groundIds=ground.HasValue?journal.GroundCandidates().Select(c=>c.target).ToHashSet(StringComparer.Ordinal):null;
            var targets=new List<string>();
            foreach(var id in journal.SavedObjectIds()){
                var item=Find(id);
                if(item&&item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)continue;
                var collision=ReadCollisionBounds(id);
                // Visual bounds cannot prove collision clearance. Unknown cold
                // colliders are loaded even when their artwork appears distant.
                bool needed=!collision.Known||collision.HasBounds&&collision.Bounds.Intersects(body);
                if(!needed&&ground.HasValue&&groundIds.Contains(id))
                    needed=!collision.Known||!collision.HasBounds||collision.Bounds.Intersects(ground.Value);
                if(needed)targets.Add(id);
            }
            return targets.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        }
    }
}
