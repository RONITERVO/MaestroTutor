// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
 public sealed partial class LiquidPouring {
  sealed class FlowSeed {
   internal readonly HashSet<string> Members=new(StringComparer.Ordinal),Targets=new(StringComparer.Ordinal);
   internal readonly List<FlowProbe> Probes=new();internal bool Unknown,Ready;
  }
  sealed class FlowPlan {
   internal string Key;internal string[] Members,Targets;internal FlowProbe[] Probes;internal bool Unknown,Active;
  }
  void FlowProbeAt(FlowSeed seed,Bounds bounds,string actor,bool includeVessels,Dictionary<string,RoomSpatialExtent> cavities,RoomEditor.MovementCandidates spatial){
   discoveryWork.Probes++;bounds.Expand(.002f);seed.Probes.Add(new(bounds,actor));
   int before=spatial.Visits,nodesBefore=spatial.NodeVisits;var roomBounds=editor.SpatialInputRoomBounds(bounds);
   foreach(var id in spatial.Targets(roomBounds,roomBounds,actor))seed.Targets.Add(id);
   discoveryWork.CollisionCandidates+=spatial.Visits-before;discoveryWork.CollisionNodes+=spatial.NodeVisits-nodesBefore;
   if(!includeVessels)return;
   foreach(var pair in cavities){
    discoveryWork.CavityCandidates++;
    if(pair.Key==actor)continue;var extent=pair.Value;
    if(!extent.Known||!extent.HasBounds||extent.Bounds.Intersects(bounds)){seed.Members.Add(pair.Key);seed.Targets.Add(pair.Key);}
   }
  }
  FlowPlan[] CaptureFlowGroups(Vector3 gravity,Vector3 up){
   // This snapshot exists only inside the admitted synchronous discovery.
   // Never reuse collision placement across physics, loading or another tick.
   discoveryWork.Captures++;var spatial=editor.CaptureMovementCandidates(true);
   discoveryWork.CollisionReads+=spatial.BoundsReads;discoveryWork.CollisionIndexBuild+=spatial.IndexBuildWork;
   var cavities=new Dictionary<string,RoomSpatialExtent>(StringComparer.Ordinal);var poses=new Dictionary<string,(Matrix4x4 matrix,Quaternion rotation)>(StringComparer.Ordinal);
   foreach(var vessel in vessels.Values){discoveryWork.CavityReads++;cavities[vessel.Id]=editor.MissingLiquidExtent(vessel.Id,vessel.Live,out _,out var matrix,out var rotation);poses[vessel.Id]=vessel.Item?(vessel.Item.transform.localToWorldMatrix,vessel.Item.transform.rotation):(matrix,rotation);}
   var seeds=vessels.ToDictionary(pair=>pair.Key,_=>new FlowSeed(),StringComparer.Ordinal);var weather=editor.CurrentWeather;var rainVelocity=WeatherCover.Velocity(editor);
   foreach(var vessel in vessels.Values){
    var seed=seeds[vessel.Id];seed.Members.Add(vessel.Id);seed.Targets.Add(vessel.Id);seed.Ready=vessel.Ready||vessel.Rigid&&vessel.Rigid.PhysicsWorkEligible;var extent=cavities[vessel.Id];
    if(!extent.Known||!extent.HasBounds){seed.Unknown=true;seed.Members.UnionWith(vessels.Keys);seed.Targets.UnionWith(vessels.Keys);continue;}
    FlowProbeAt(seed,extent.Bounds,vessel.Id,true,cavities,spatial);
    var pose=poses[vessel.Id];var terrain=vessel.Item?RoomTerrainReservoir.Native(vessel.Live,vessel.Item.transform):null;
    if(vessel.Item&&vessel.Rigid&&vessel.Rigid.Dynamic){discoveryWork.IslandQueries++;foreach(var id in editor.PhysicsIslandMembers(vessel.Item)){discoveryWork.IslandMembers++;if(vessels.ContainsKey(id))seed.Members.Add(id);}}
    // Missing ordinary cavities still have a retained/anchored pose. Use the same
    // lip geometry as live pouring, so nearby independent circuits stay separate.
    // An unavailable terrain bed cannot prove retained volume or a precise lip.
    if(vessel.Live.TerrainFloor&&terrain==null){
     float duration=Segments*.025f;var possible=extent.Bounds;possible.Encapsulate(new Bounds(extent.Bounds.center+gravity*(.5f*duration*duration),extent.Bounds.size));possible.Expand(2*(3+.15f)*duration);
     FlowProbeAt(seed,possible,vessel.Id,true,cavities,spatial);
    }else if(vessel.Live.capacityMl-ContainerFlowGeometry.RetainedCapacity(vessel.Live,pose.rotation,up,terrain)>=.000001){
     var origin=ContainerFlowGeometry.Lip(vessel.Live,pose.matrix,pose.rotation,up,out var outward);var velocity=outward*.15f;
     if(vessel.Rigid)velocity+=Vector3.ClampMagnitude(vessel.Rigid.FlowPointVelocity(origin),3);
     var bounds=new Bounds(origin,Vector3.zero);
     for(int i=1;i<=Segments;i++){discoveryWork.PathSegments++;float t=i*.025f;var point=origin+velocity*t+gravity*(.5f*t*t);bounds.Encapsulate(point);if(vessel.Item&&!world.CanSimulate(point,vessel.Item))break;}
     FlowProbeAt(seed,bounds,vessel.Id,true,cavities,spatial);
    }
    if(!vessel.Ready)continue; // Loading must finish and rebuild proof before collection.
    // Earlier transfers may drain a full or incompatible vessel before rain.
    if(weather.Rain<=0)continue;var normal=vessel.Item.transform.rotation*vessel.Live.frame.rotation*Vector3.up;
    if(Vector3.Dot(normal,-rainVelocity)/6<=.001f)continue;var rainBounds=new Bounds(RainOpeningPoint(vessel,0),Vector3.zero);
    for(int sample=0;sample<5;sample++){var point=RainOpeningPoint(vessel,sample);rainBounds.Encapsulate(point);rainBounds.Encapsulate(point-rainVelocity.normalized*WeatherCover.Distance);}
    FlowProbeAt(seed,rainBounds,vessel.Id,false,cavities,spatial);
   }
   // Quantity paths are undirected dependencies even though the actual transfer
   // is directional. Shared read-only obstacles/roofs do not merge circuits.
   foreach(var pair in seeds)foreach(var id in pair.Value.Members.ToArray()){discoveryWork.GraphEdges++;seeds[id].Members.Add(pair.Key);}
   var visited=new HashSet<string>(StringComparer.Ordinal);var plans=new List<FlowPlan>();
   foreach(var id in vessels.Keys){
    if(!visited.Add(id))continue;var pending=new Queue<string>();pending.Enqueue(id);var members=new List<string>();
    while(pending.Count>0){var next=pending.Dequeue();members.Add(next);foreach(var peer in seeds[next].Members){discoveryWork.GraphEdges++;if(visited.Add(peer))pending.Enqueue(peer);}}
    var ordered=members.OrderBy(x=>x,StringComparer.Ordinal).ToArray();var chosen=ordered.Select(x=>seeds[x]).ToArray();
    plans.Add(new FlowPlan{Key=string.Join(":",ordered),Members=ordered,Targets=chosen.SelectMany(x=>x.Targets).Concat(ordered).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),Probes=chosen.SelectMany(x=>x.Probes).ToArray(),Unknown=chosen.Any(x=>x.Unknown),Active=chosen.Any(x=>x.Ready)});
   }
   return plans.ToArray();
  }
 }
}
