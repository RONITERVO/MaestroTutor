// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
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
        internal sealed class MovementCandidates {
            internal readonly struct Candidate {
                internal readonly string Id;internal readonly RoomSpatialExtent Collision;internal readonly bool Ground;
                internal Candidate(string id,RoomSpatialExtent collision,bool ground){Id=id;Collision=collision;Ground=ground;}
            }
            // Immutable entries/tree may be shared by synchronous snapshots. Query
            // scratch space and accounting belong to the individual snapshot.
            internal sealed class Set {
                readonly Candidate[] values;readonly RoomSpatialLookup lookup;
                internal int Count=>values.Length;
                internal int BuildWork=>lookup?.BuildWork??0;
                internal Set(Candidate[] source,bool indexed){
                    if(source==null)throw new ArgumentNullException(nameof(source));
                    if(source.Length>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(source));
                    values=(Candidate[])source.Clone();var bounds=indexed&&values.Length>0?new RoomSpatialExtent[values.Length]:null;
                    for(int i=0;i<values.Length;i++){
                        var value=values[i];var extent=value.Collision;
                        if(extent.Known&&extent.HasBounds&&!RoomSpatialLookup.Valid(extent.Bounds))extent=default;
                        values[i]=new(value.Id,extent,value.Ground);if(bounds!=null)bounds[i]=extent;
                    }
                    if(bounds!=null)lookup=new RoomSpatialLookup(bounds);
                }
                internal void Collect(Bounds body,Bounds? ground,string actor,List<string> result,List<int> selected,ref int visits,ref int nodes){
                    if(lookup!=null){nodes+=lookup.Query(body,ground,selected);selected.Sort();}
                    bool invalidBody=!RoomSpatialLookup.Valid(body),invalidGround=ground.HasValue&&!RoomSpatialLookup.Valid(ground.Value);
                    int count=lookup!=null?selected.Count:values.Length;
                    for(int i=0;i<count;i++){
                        var candidate=values[lookup!=null?selected[i]:i];visits++;if(candidate.Id==actor)continue;
                        var collision=candidate.Collision;
                        // Unknown collision geometry remains required regardless
                        // of the placement or size of its visual artwork.
                        bool needed=!collision.Known||collision.HasBounds&&(invalidBody||collision.Bounds.Intersects(body));
                        if(!needed&&ground.HasValue&&candidate.Ground)
                            needed=!collision.HasBounds||invalidGround||collision.Bounds.Intersects(ground.Value);
                        if(needed)result.Add(candidate.Id);
                    }
                }
            }
            readonly Set current,dormant;readonly bool includeGround;
            readonly List<int> selected=new();
            internal int Visits {get;private set;}
            internal int NodeVisits {get;private set;}
            internal int BoundsReads {get;}
            internal int DormantBoundsReads {get;}
            internal int IndexBuildWork {get;}
            internal MovementCandidates(Candidate[] source,bool repeatedQueries=true):this(new Set(source,repeatedQueries),null,0,0,true){}
            internal MovementCandidates(Set current,Set dormant,int dormantReads,int dormantBuildWork,bool includeGround){
                this.current=current??throw new ArgumentNullException(nameof(current));this.dormant=dormant;this.includeGround=includeGround;
                if((long)current.Count+(dormant?.Count??0)>RoomSpatialLookup.MaximumEntries)throw new ArgumentOutOfRangeException(nameof(current));
                DormantBoundsReads=dormantReads;BoundsReads=current.Count+dormantReads;IndexBuildWork=current.BuildWork+dormantBuildWork;
            }
            internal string[] Targets(Bounds body,Bounds? ground,string actor=null){
                var result=new List<string>();int visits=0,nodes=0;if(!includeGround)ground=null;
                current.Collect(body,ground,actor,result,selected,ref visits,ref nodes);
                dormant?.Collect(body,ground,actor,result,selected,ref visits,ref nodes);
                Visits+=visits;NodeVisits+=nodes;
                if(dormant!=null)result.Sort(StringComparer.Ordinal);
                return result.ToArray();
            }
        }
        internal MovementCandidates CaptureMovementCandidates(bool includeGround,bool repeatedQueries=true){
            if(journal==null)return new(Array.Empty<MovementCandidates.Candidate>(),repeatedQueries);
            RefreshMovementDirectory(out int dormantReads,out int dormantBuild);
            var current=new List<MovementCandidates.Candidate>(movementDynamic.Length);
            foreach(var value in movementDynamic){
                var item=Find(value.id);
                if(item&&item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true)continue;
                current.Add(new(value.id,ReadCollisionBounds(value.id),value.ground));
            }
            return new(new MovementCandidates.Set(current.ToArray(),repeatedQueries),movementDormant,dormantReads,dormantBuild,includeGround);
        }
        // One-off movement reuses the dormant tree, but does not build an index
        // for the current native/anchor partition. Repeated flow probes may do so.
        internal string[] SpatialMovementTargets(Bounds body,Bounds? ground)=>CaptureMovementCandidates(ground.HasValue,repeatedQueries:false).Targets(body,ground);
    }
}
