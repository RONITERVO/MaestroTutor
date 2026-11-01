// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        readonly struct GroundCandidate {
            internal readonly (string target,bool terrain,bool imported) Definition;
            internal readonly RoomSpatialExtent Extent;
            internal readonly bool Cached;
            internal GroundCandidate((string target,bool terrain,bool imported) definition,RoomSpatialExtent extent,bool cached){
                Definition=definition;Cached=cached;
                Extent=extent.Known&&extent.HasBounds&&!RoomSpatialLookup.Valid(extent.Bounds)?default:extent;
            }
        }
        RoomSpatialDirectoryCursor groundDirectoryState;
        RoomSpatialOverlay<GroundCandidate> groundDormant;
        GroundCandidate[] groundDynamic=Array.Empty<GroundCandidate>();
        // Both callers finish synchronously, without invoking user callbacks.
        // These buffers never escape or own native/physical poses.
        readonly List<RoomSpatialOverlay<GroundCandidate>.Entry> groundMatches=new();
        readonly List<int> groundScratch=new();
        long groundReads,groundDormantReads,groundBuildWork,groundNodeVisits,groundQueryCandidates,groundOverlayUpdates,groundCompactions;
        internal (long reads,long build,long nodes,long candidates,long dormantReads,long overlayUpdates,long compactions,int overrides) GroundQueryWork=>
            (groundReads,groundBuildWork,groundNodeVisits,groundQueryCandidates,groundDormantReads,groundOverlayUpdates,groundCompactions,groundDormant?.OverrideCount??0);
        void ClearGroundDirectory(){groundDirectoryState=null;groundDormant=null;groundDynamic=Array.Empty<GroundCandidate>();groundMatches.Clear();groundScratch.Clear();}
        RoomSpatialExtent ReadGroundExtent(string id,bool dormant=false){
            groundReads++;if(dormant)groundDormantReads++;
            var spatial=ReadSpatialBounds(id,out var source);return RoomGroundBounds.ForReadiness(spatial,source=="authored");
        }
        void RefreshGroundDirectory(){
            // Keep warm support checks allocation-free. Current native readiness
            // and exact scan-anchor bounds still belong to MissingGround.
            if(groundDirectoryState?.MatchesCurrent(journal,spatialIndex,collisionResidencyChanges,movementResidencyReset)==true)return;
            var state=new RoomSpatialDirectoryCursor(journal,spatialIndex,collisionResidencyChanges,movementResidencyReset);
            RoomSpatialOverlay<GroundCandidate> snapshot;GroundCandidate[] liveEntries;
            int updates=0,compactions=0,build=0;
            if(groundDirectoryState!=null&&groundDirectoryState.TryChanges(journal,spatialIndex,collisionResidencyChanges,movementResidencyReset,groundDormant.Contains,out var dirty)){
                if(dirty.Count==0){groundDirectoryState=state;return;}
                var live=groundDynamic.ToDictionary(value=>value.Definition.target,value=>value,StringComparer.Ordinal);
                var changes=new List<RoomSpatialOverlay<GroundCandidate>.Change>();
                foreach(var id in dirty.OrderBy(value=>value,StringComparer.Ordinal)){
                    // Classify every authored/membership identity, including IDs
                    // absent from the previous ground partition.
                    bool ground=journal.GroundCandidate(id,out var definition);
                    if(ground&&NativeEntityDormant(id)&&!journal.ScanAnchored(id)){
                        var candidate=new GroundCandidate(definition,ReadGroundExtent(id,true),true);
                        changes.Add(new(new RoomSpatialOverlay<GroundCandidate>.Entry(id,candidate.Extent,candidate)));live.Remove(id);
                    }else{
                        if(groundDormant.Contains(id))changes.Add(new(id));
                        if(ground)live[id]=new(definition,default,false);else live.Remove(id);
                    }
                }
                snapshot=groundDormant.Apply(changes,out updates,out compactions,out build);
                liveEntries=live.OrderBy(value=>value.Key,StringComparer.Ordinal).Select(value=>value.Value).ToArray();
            }else{
                var definitions=journal.GroundCandidates();
                if(definitions.Length>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(definitions),"Spatial lookup capacity exceeded");
                var stable=new List<RoomSpatialOverlay<GroundCandidate>.Entry>();var live=new List<GroundCandidate>();
                foreach(var definition in definitions){
                    // Expected-but-missing native instances and scan bindings
                    // remain fresh leaf reads, never cached absence/readiness.
                    if(!NativeEntityDormant(definition.target)||journal.ScanAnchored(definition.target)){live.Add(new(definition,default,false));continue;}
                    var candidate=new GroundCandidate(definition,ReadGroundExtent(definition.target,true),true);
                    stable.Add(new(definition.target,candidate.Extent,candidate));
                }
                snapshot=new(stable);build=snapshot.BuildWork;liveEntries=live.ToArray();
            }
            if((long)snapshot.Count+liveEntries.Length>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(snapshot));
            // Publish the complete immutable partition and end cursors together.
            // Exceptions retain the previous snapshot and all unconsumed changes.
            groundDormant=snapshot;groundDynamic=liveEntries;groundDirectoryState=state;
            groundBuildWork+=build;groundOverlayUpdates+=updates;groundCompactions+=compactions;
        }
        void SelectGroundCandidates(Bounds envelope){
            RefreshGroundDirectory();groundNodeVisits+=groundDormant.Query(envelope,null,groundMatches,groundScratch);
            foreach(var candidate in groundDynamic)groundMatches.Add(new(candidate.Definition.target,candidate.Extent,candidate));
            groundMatches.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));groundQueryCandidates+=groundMatches.Count;
        }
    }
}
