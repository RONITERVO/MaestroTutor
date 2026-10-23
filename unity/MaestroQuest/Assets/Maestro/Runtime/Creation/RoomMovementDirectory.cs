// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        internal bool ScanAnchored(string target)=>items.TryGetValue(target,out var value)&&ScanDrawingAnchor.Has(value);
    }
    public sealed partial class RoomEditor
    {
        RoomSpatialDirectoryState movementDirectoryState;
        int collisionResidencyVersion;
        MovementCandidates.Set movementDormant;
        (string id,bool ground)[] movementDynamic=Array.Empty<(string,bool)>();
        void InvalidateMovementResidency(){collisionResidencyVersion++;}
        void ClearMovementDirectory(){movementDirectoryState=null;movementDormant=null;movementDynamic=Array.Empty<(string,bool)>();}
        void RefreshMovementDirectory(out int dormantReads,out int dormantBuild)
        {
            dormantReads=dormantBuild=0;if(movementDirectoryState?.Current(journal,spatialIndex,collisionResidencyVersion)==true)return;
            var stamp=journal.SpatialCatalogStamp;
            if(stamp.count>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(stamp),"Spatial lookup capacity exceeded");
            var ground=journal.GroundCandidates().Select(value=>value.target).ToHashSet(StringComparer.Ordinal);
            var stable=new List<MovementCandidates.Candidate>();var live=new List<(string,bool)>();
            foreach(var id in journal.SavedObjectIds()){
                // Resident/unexpectedly missing objects can change without an
                // authored edit. Physical anchors must resolve on every capture.
                if(!NativeEntityDormant(id)||journal.ScanAnchored(id)){live.Add((id,ground.Contains(id)));continue;}
                stable.Add(new(id,ReadCollisionBounds(id),ground.Contains(id)));dormantReads++;
            }
            var snapshot=new MovementCandidates.Set(stable.ToArray(),true);dormantBuild=snapshot.BuildWork;
            // Publish a whole immutable partition. Old synchronous captures keep
            // their own values; no native object or scan pose enters this cache.
            var state=new RoomSpatialDirectoryState(journal,spatialIndex,collisionResidencyVersion,stable.Select(value=>value.Id));
            movementDynamic=live.ToArray();movementDormant=snapshot;movementDirectoryState=state;
        }
    }
}
