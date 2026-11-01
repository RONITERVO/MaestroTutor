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
        // Collection is independent of a consumer's old partition: an empty
        // partition can acquire new identities, and lost history is never empty.
        internal bool TryCollect(long since,out string[] changed,out long end){
            end=Version;changed=Array.Empty<string>();
            if(since<0||since>end||end-since>Capacity)return false;
            if(since==end)return true;
            var found=new HashSet<string>(StringComparer.Ordinal);
            for(long version=since;version<end;version++)found.Add(targets[(int)(version%Capacity)]);
            changed=new string[found.Count];found.CopyTo(changed);Array.Sort(changed,StringComparer.Ordinal);return true;
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
        readonly RoomSpatialChanges spatialPlacements=new(),spatialAuthored=new();
        // All authored object fields invalidate the catalog, including future
        // components. Placement alone can be checked against cached identities.
        internal (int revision,int count) SpatialCatalogStamp=>(spatialCatalogRevision,items.Count);
        internal long SpatialAuthoredVersion=>spatialAuthored.Version;
        internal bool TrySpatialAuthoredChanges(long since,out string[] changed,out long end)=>spatialAuthored.TryCollect(since,out changed,out end);
        internal bool TrySpatialPlacementChanges(long since,out string[] changed,out long end)=>spatialPlacements.TryCollect(since,out changed,out end);
        internal long SpatialPlacementVersion=>spatialPlacements.Version;
        internal bool SpatialPlacementsAffect(long since,HashSet<string> targets)=>spatialPlacements.Affects(since,targets);
    }
}
