// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal readonly struct DormantVisual {
            internal readonly string Id;internal readonly RoomSpatialBounds Spatial;internal readonly bool Prepared,Placed;
            internal DormantVisual(string id,RoomSpatialBounds spatial,bool prepared,bool placed){Id=id;Spatial=spatial;Prepared=prepared;Placed=placed;}
        }
        RoomJournal visualJournal;RoomSpatialIndex visualMetadata;
        (int revision,int count) visualStamp;
        int visualResidencyVersion,visualMetadataVersion;
        DormantVisual[] visualDormant=Array.Empty<DormantVisual>();
        string[] visualAnchors=Array.Empty<string>(),visualLive=Array.Empty<string>();
        Dictionary<string,int> visualPositions=new(StringComparer.Ordinal);
        RoomSpatialLookup visualLookup;
        long visualReads,visualBuildWork,visualNodeVisits,visualCandidates;
        internal (long reads,long build,long nodes,long candidates) VisualQueryWork=>(visualReads,visualBuildWork,visualNodeVisits,visualCandidates);
        void ClearVisualDirectory(){visualJournal=null;visualMetadata=null;visualLookup=null;visualDormant=Array.Empty<DormantVisual>();visualAnchors=visualLive=Array.Empty<string>();visualPositions.Clear();}
        DormantVisual ReadDormantVisual(string id){
            visualReads++;var spatial=ReadDormantSpatial(id,dormantNative[id],out _,out var prepared,out var placed);
            return new(id,spatial,prepared,placed);
        }
        void RefreshVisualDirectory(){
            var stamp=journal.MovementCatalogStamp;int metadata=spatialIndex?.Generation??0;
            if(visualLookup!=null&&ReferenceEquals(visualJournal,journal)&&visualStamp==stamp&&visualResidencyVersion==collisionResidencyVersion&&
                ReferenceEquals(visualMetadata,spatialIndex)&&visualMetadataVersion==metadata)return;
            if(stamp.count>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(stamp),"Spatial lookup capacity exceeded");
            var stable=new List<DormantVisual>();var anchors=new List<string>();var live=new List<string>();
            foreach(var id in journal.SavedObjectIds()){
                if(!NativeEntityDormant(id)){live.Add(id);continue;}
                if(journal.ScanAnchored(id)){anchors.Add(id);continue;}
                stable.Add(ReadDormantVisual(id));
            }
            var values=stable.ToArray();var positions=values.Select((entry,index)=>(entry.Id,index)).ToDictionary(pair=>pair.Id,pair=>pair.index,StringComparer.Ordinal);
            var lookup=new RoomSpatialLookup(values.Select(entry=>entry.Spatial.Visual).ToArray());
            // Publish only a complete immutable partition; no live native pose
            // or physical scan projection enters the retained tree.
            visualDormant=values;visualAnchors=anchors.ToArray();visualLive=live.ToArray();visualPositions=positions;visualLookup=lookup;visualBuildWork+=lookup.BuildWork;
            visualJournal=journal;visualStamp=stamp;visualResidencyVersion=collisionResidencyVersion;visualMetadata=spatialIndex;visualMetadataVersion=metadata;
        }
        DormantVisual[] FindDormantVisual(Func<Vector3,Vector3,bool> intersects,IEnumerable<string> required=null,bool includeEmpty=false){
            RefreshVisualDirectory();var indices=new List<int>();visualNodeVisits+=visualLookup.Query(intersects,indices,includeEmpty);
            if(required!=null){var included=indices.ToHashSet();foreach(var id in required)if(visualPositions.TryGetValue(id,out var index)&&included.Add(index))indices.Add(index);}
            indices.Sort();var result=new List<DormantVisual>(indices.Count+visualAnchors.Length);
            foreach(var index in indices)result.Add(visualDormant[index]);
            // Exact anchors can move or disappear without an authored revision.
            // Fresh projection is intentionally independent of the stable tree.
            foreach(var id in visualAnchors)result.Add(ReadDormantVisual(id));
            visualCandidates+=result.Count;return result.ToArray();
        }
        internal DormantVisual[] QueryDormantView(IReadOnlyList<RoomObservationView> views,IEnumerable<string> required=null){
            var mapping=transform.localToWorldMatrix;bool valid=Frame.Valid;
            return FindDormantVisual((min,max)=>{
                if(!valid)return true;var envelope=RoomVisualEnvelope.Project(min,max,mapping);
                foreach(var view in views)if(view.MayInclude(envelope))return true;return false;
            },required,includeEmpty:!valid);
        }
        DormantVisual[] QueryDormantShadows(IReadOnlyList<RoomSpatialExtent> influences){
            var mapping=transform.localToWorldMatrix;bool valid=Frame.Valid;
            return FindDormantVisual((min,max)=>{
                if(!valid)return true;var envelope=RoomVisualEnvelope.Project(min,max,mapping);
                foreach(var influence in influences)if(RoomLightObservation.Intersects(influence,envelope))return true;return false;
            },includeEmpty:influences.Any(value=>!value.Known));
        }
        string[] CurrentVisualTargets(){RefreshVisualDirectory();return visualLive;}
    }
}
