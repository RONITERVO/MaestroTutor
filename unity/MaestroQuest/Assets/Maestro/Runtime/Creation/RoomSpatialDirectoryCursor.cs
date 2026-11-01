// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Creation {
    // Immutable end cursors shared by exact-identity dormant directories. Publish
    // only with a fully constructed partition; failed projection consumes no log.
    internal sealed class RoomSpatialDirectoryCursor {
        readonly RoomJournal journal;readonly RoomSpatialIndex metadata;
        readonly RoomSpatialChanges membership;
        readonly long authored,placements,publication,members;readonly int reset;
        internal RoomSpatialDirectoryCursor(RoomJournal journal,RoomSpatialIndex metadata,RoomSpatialChanges membership,int reset){
            this.journal=journal;this.metadata=metadata;this.membership=membership;this.reset=reset;
            authored=journal.SpatialAuthoredVersion;placements=journal.SpatialPlacementVersion;
            publication=metadata?.Generation??0;members=membership.Version;
        }
        bool SameOwners(RoomJournal current,RoomSpatialIndex index,RoomSpatialChanges residents,int epoch)=>
            ReferenceEquals(journal,current)&&ReferenceEquals(metadata,index)&&ReferenceEquals(membership,residents)&&reset==epoch;
        internal bool MatchesCurrent(RoomJournal current,RoomSpatialIndex index,RoomSpatialChanges residents,int epoch)=>
            SameOwners(current,index,residents,epoch)&&authored==current.SpatialAuthoredVersion&&
            placements==current.SpatialPlacementVersion&&publication==(index?.Generation??0)&&members==residents.Version;
        internal bool TryChanges(RoomJournal current,RoomSpatialIndex index,RoomSpatialChanges residents,int epoch,
            Func<string,bool> stableContains,out HashSet<string> dirty){
            dirty=null;if(!SameOwners(current,index,residents,epoch))return false;
            // Validate every complete window before filtering by the old stable
            // set. Authored/membership insertions may not be in that set at all.
            if(!current.TrySpatialAuthoredChanges(authored,out var edits,out _)||
                !residents.TryCollect(members,out var residency,out _)||
                !current.TrySpatialPlacementChanges(placements,out var moved,out _))return false;
            string[] published=Array.Empty<string>();
            if(index!=null&&!index.TryChanges(publication,out published,out _))return false;
            dirty=new(StringComparer.Ordinal);dirty.UnionWith(edits);dirty.UnionWith(residency);
            foreach(var id in moved)if(stableContains(id))dirty.Add(id);
            foreach(var id in published)if(stableContains(id))dirty.Add(id);
            return true;
        }
    }
}
