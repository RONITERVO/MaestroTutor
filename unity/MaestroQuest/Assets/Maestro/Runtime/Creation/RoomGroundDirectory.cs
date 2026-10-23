// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        readonly struct GroundCandidate {
            internal readonly (string target,bool terrain,bool imported) Definition;
            internal readonly RoomSpatialExtent Extent;
            internal readonly bool Cached;
            internal GroundCandidate((string target,bool terrain,bool imported) definition,RoomSpatialExtent extent,bool cached){Definition=definition;Extent=extent;Cached=cached;}
        }
        RoomJournal groundJournal;RoomSpatialIndex groundMetadata;
        (int revision,int count) groundStamp;
        int groundResidencyVersion,groundMetadataVersion;
        GroundCandidate[] groundEntries=Array.Empty<GroundCandidate>();
        int[] groundStableSlots=Array.Empty<int>(),groundLiveSlots=Array.Empty<int>();
        RoomSpatialLookup groundLookup;
        // Both callers finish synchronously, without invoking user callbacks.
        // This buffer never escapes or owns native/physical poses.
        readonly List<int> groundMatches=new();
        long groundReads,groundBuildWork,groundNodeVisits,groundQueryCandidates;
        internal (long reads,long build,long nodes,long candidates) GroundQueryWork=>(groundReads,groundBuildWork,groundNodeVisits,groundQueryCandidates);
        void ClearGroundDirectory(){groundJournal=null;groundMetadata=null;groundLookup=null;groundEntries=Array.Empty<GroundCandidate>();groundStableSlots=groundLiveSlots=Array.Empty<int>();groundMatches.Clear();}
        RoomSpatialExtent ReadGroundExtent(string id){
            groundReads++;var spatial=ReadSpatialBounds(id,out var source);return RoomGroundBounds.ForReadiness(spatial,source=="authored");
        }
        void RefreshGroundDirectory(){
            var stamp=journal.MovementCatalogStamp;int metadata=spatialIndex?.Generation??0;
            if(groundLookup!=null&&ReferenceEquals(groundJournal,journal)&&groundStamp==stamp&&groundResidencyVersion==collisionResidencyVersion&&
                ReferenceEquals(groundMetadata,spatialIndex)&&groundMetadataVersion==metadata)return;
            var definitions=journal.GroundCandidates();
            if(definitions.Length>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(definitions),"Spatial lookup capacity exceeded");
            var entries=new GroundCandidate[definitions.Length];var stable=new List<int>();var live=new List<int>();var bounds=new List<RoomSpatialExtent>();
            for(int i=0;i<definitions.Length;i++){
                var definition=definitions[i];bool cached=NativeEntityDormant(definition.target)&&!journal.ScanAnchored(definition.target);
                var extent=cached?ReadGroundExtent(definition.target):default;entries[i]=new(definition,extent,cached);
                if(cached){stable.Add(i);bounds.Add(extent);}else live.Add(i);
            }
            var lookup=new RoomSpatialLookup(bounds);
            // Publish a complete immutable partition. Current native readiness,
            // live poses and exact anchors are always evaluated by the leaf.
            groundEntries=entries;groundStableSlots=stable.ToArray();groundLiveSlots=live.ToArray();groundLookup=lookup;groundBuildWork+=lookup.BuildWork;
            groundJournal=journal;groundStamp=stamp;groundResidencyVersion=collisionResidencyVersion;groundMetadata=spatialIndex;groundMetadataVersion=metadata;
        }
        void SelectGroundCandidates(Bounds envelope){
            RefreshGroundDirectory();groundNodeVisits+=groundLookup.Query(envelope,null,groundMatches);
            for(int i=0;i<groundMatches.Count;i++)groundMatches[i]=groundStableSlots[groundMatches[i]];
            groundMatches.AddRange(groundLiveSlots);groundMatches.Sort();groundQueryCandidates+=groundMatches.Count;
        }
    }
}
