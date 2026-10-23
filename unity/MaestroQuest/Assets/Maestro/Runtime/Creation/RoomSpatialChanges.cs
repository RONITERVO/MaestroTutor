// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Creation {
    // A bounded identity log, not another authored model. A lagging consumer must
    // refresh when changes have fallen out of the window; absence is never guessed.
    internal sealed class RoomSpatialChanges {
        internal const int Capacity=256;
        readonly string[] targets=new string[Capacity];
        internal long Version {get;private set;}
        internal void Record(string target){
            if(string.IsNullOrEmpty(target))throw new ArgumentException("A spatial change needs an identity",nameof(target));
            targets[(int)(Version%Capacity)]=target;Version++;
        }
        internal bool Affects(long since,HashSet<string> observed){
            if(observed==null)throw new ArgumentNullException(nameof(observed));
            if(observed.Count==0||since==Version)return false;
            if(since<0||since>Version||Version-since>Capacity)return true;
            for(long version=Version;version>since;version--)
                if(observed.Contains(targets[(int)((version-1)%Capacity)]))return true;
            return false;
        }
    }
    public sealed partial class RoomJournal {
        int spatialCatalogRevision;
        readonly RoomSpatialChanges spatialPlacements=new();
        // All authored object fields invalidate the catalog, including future
        // components. Placement alone can be checked against cached identities.
        internal (int revision,int count) SpatialCatalogStamp=>(spatialCatalogRevision,items.Count);
        internal long SpatialPlacementVersion=>spatialPlacements.Version;
        internal bool SpatialPlacementsAffect(long since,HashSet<string> targets)=>spatialPlacements.Affects(since,targets);
    }
}
