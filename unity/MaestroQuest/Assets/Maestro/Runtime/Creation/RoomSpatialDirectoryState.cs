// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Creation {
    // Shared invalidation contract for immutable dormant collision, visual and
    // ground partitions. Live native/anchor poses never enter these snapshots.
    internal sealed class RoomSpatialDirectoryState {
        readonly RoomJournal journal;readonly RoomSpatialIndex metadata;
        readonly (int revision,int count) catalog;readonly int residency;
        readonly HashSet<string> targets;
        long placements,publication;
        internal RoomSpatialDirectoryState(RoomJournal journal,RoomSpatialIndex metadata,int residency,IEnumerable<string> targets){
            this.journal=journal;this.metadata=metadata;this.residency=residency;
            this.targets=new HashSet<string>(targets,StringComparer.Ordinal);catalog=journal.SpatialCatalogStamp;
            placements=journal.SpatialPlacementVersion;publication=metadata?.Generation??0;
        }
        internal bool Current(RoomJournal current,RoomSpatialIndex index,int membership){
            if(!ReferenceEquals(journal,current)||!ReferenceEquals(metadata,index)||residency!=membership||catalog!=current.SpatialCatalogStamp)return false;
            if(current.SpatialPlacementsAffect(placements,targets)||index?.ChangesAffect(publication,targets)==true)return false;
            // Consume irrelevant identities without rebuilding or retaining an
            // ever-growing log. A future missed window still forces a refresh.
            placements=current.SpatialPlacementVersion;publication=index?.Generation??0;return true;
        }
    }
}
