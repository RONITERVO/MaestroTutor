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
        sealed class MovementDirectoryCursor {
            internal readonly RoomJournal Journal;internal readonly RoomSpatialIndex Metadata;
            internal readonly long Authored,Placements,Publication,Membership;internal readonly int Reset;
            internal bool MatchesCurrent(RoomJournal journal,RoomSpatialIndex metadata,RoomSpatialChanges membership,int reset)=>
                ReferenceEquals(Journal,journal)&&ReferenceEquals(Metadata,metadata)&&Reset==reset&&
                Authored==journal.SpatialAuthoredVersion&&Placements==journal.SpatialPlacementVersion&&
                Publication==(metadata?.Generation??0)&&Membership==membership.Version;
            internal MovementDirectoryCursor(RoomJournal journal,RoomSpatialIndex metadata,RoomSpatialChanges membership,int reset){
                Journal=journal;Metadata=metadata;Reset=reset;Authored=journal.SpatialAuthoredVersion;
                Placements=journal.SpatialPlacementVersion;Publication=metadata?.Generation??0;Membership=membership.Version;
            }
        }
        MovementDirectoryCursor movementDirectoryState;
        int collisionResidencyVersion,movementResidencyReset;
        readonly RoomSpatialChanges collisionResidencyChanges=new();
        MovementCandidates.Set movementDormant;
        (string id,bool ground)[] movementDynamic=Array.Empty<(string,bool)>();
        void InvalidateMovementResidency(string id=null){
            collisionResidencyVersion++;
            if(id==null)movementResidencyReset++;else collisionResidencyChanges.Record(id);
        }
        void ClearMovementDirectory(){movementDirectoryState=null;movementDormant=null;movementDynamic=Array.Empty<(string,bool)>();}
        bool MovementChanges(MovementDirectoryCursor cursor,out HashSet<string> dirty){
            dirty=new(StringComparer.Ordinal);
            if(cursor==null||!ReferenceEquals(cursor.Journal,journal)||!ReferenceEquals(cursor.Metadata,spatialIndex)||cursor.Reset!=movementResidencyReset)return false;
            // Validate all complete windows before filtering by the old stable
            // set. Authored/membership insertions may not be in that set at all.
            if(!journal.TrySpatialAuthoredChanges(cursor.Authored,out var authored,out _)||
                !collisionResidencyChanges.TryCollect(cursor.Membership,out var membership,out _)||
                !journal.TrySpatialPlacementChanges(cursor.Placements,out var placements,out _))return false;
            string[] publication=Array.Empty<string>();
            if(spatialIndex!=null&&!spatialIndex.TryChanges(cursor.Publication,out publication,out _))return false;
            dirty.UnionWith(authored);dirty.UnionWith(membership);
            foreach(var id in placements)if(movementDormant.Contains(id))dirty.Add(id);
            foreach(var id in publication)if(movementDormant.Contains(id))dirty.Add(id);
            return true;
        }
        void RefreshMovementDirectory(out int dormantReads,out int dormantBuild,out int overlayUpdates,out int compactions)
        {
            dormantReads=dormantBuild=overlayUpdates=compactions=0;
            if(journal.SavedObjectCount>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(journal),"Spatial lookup capacity exceeded");
            // A warm directory needs no cursor or delta collection allocations.
            // CaptureMovementCandidates still reads current native/anchor bounds.
            if(movementDirectoryState?.MatchesCurrent(journal,spatialIndex,collisionResidencyChanges,movementResidencyReset)==true)return;
            // Capture end cursors before any projection. Publish them only with
            // the fully constructed next snapshot; exceptions consume no changes.
            var state=new MovementDirectoryCursor(journal,spatialIndex,collisionResidencyChanges,movementResidencyReset);
            MovementCandidates.Set snapshot; (string id,bool ground)[] liveEntries;
            if(MovementChanges(movementDirectoryState,out var dirty)){
                if(dirty.Count==0){movementDirectoryState=state;return;}
                var live=movementDynamic.ToDictionary(value=>value.id,value=>value.ground,StringComparer.Ordinal);
                var changes=new List<RoomSpatialOverlay<MovementCandidates.Candidate>.Change>();
                foreach(var id in dirty.OrderBy(value=>value,StringComparer.Ordinal)){
                    bool exists=journal.ContainsObject(id);
                    bool ground=exists&&journal.GroundCandidate(id,out _);
                    if(exists&&NativeEntityDormant(id)&&!journal.ScanAnchored(id)){
                        var collision=ReadCollisionBounds(id);dormantReads++;
                        var candidate=new MovementCandidates.Candidate(id,collision,ground);
                        changes.Add(new(new RoomSpatialOverlay<MovementCandidates.Candidate>.Entry(id,collision,candidate)));live.Remove(id);
                    }else{
                        if(movementDormant.Contains(id))changes.Add(new(id));
                        if(exists)live[id]=ground;else live.Remove(id);
                    }
                }
                snapshot=movementDormant.Apply(changes,out overlayUpdates,out compactions,out dormantBuild);
                liveEntries=live.OrderBy(value=>value.Key,StringComparer.Ordinal).Select(value=>(value.Key,value.Value)).ToArray();
            }else{
                var stable=new List<MovementCandidates.Candidate>();var live=new List<(string,bool)>();
                foreach(var id in journal.SavedObjectIds()){
                    bool ground=journal.GroundCandidate(id,out _);
                    // Expected-but-missing native instances and scan bindings
                    // remain fresh dynamic reads, never cached absence.
                    if(!NativeEntityDormant(id)||journal.ScanAnchored(id)){live.Add((id,ground));continue;}
                    stable.Add(new(id,ReadCollisionBounds(id),ground));dormantReads++;
                }
                snapshot=MovementCandidates.Set.Dormant(stable.ToArray());dormantBuild=snapshot.BuildWork;liveEntries=live.ToArray();
            }
            if((long)snapshot.Count+liveEntries.Length>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(snapshot));
            movementDynamic=liveEntries;movementDormant=snapshot;movementDirectoryState=state;
        }
    }
}
