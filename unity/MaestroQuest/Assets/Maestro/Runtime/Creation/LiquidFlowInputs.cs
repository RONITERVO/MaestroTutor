// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
 public sealed partial class LiquidPouring {
  readonly struct FlowProbe {
   internal readonly Bounds Bounds;internal readonly string Actor;
   internal FlowProbe(Bounds bounds,string actor){Bounds=bounds;Actor=actor;}
  }
  sealed class FlowGroup {
   internal FlowPlan Plan;internal RoomSpatialInputs Inputs;internal string Issue;internal bool Ready;
  }
  Dictionary<string,FlowGroup> flowGroups=new(StringComparer.Ordinal);
  readonly Dictionary<string,FlowGroup> flowByTarget=new(StringComparer.Ordinal);
  string flowPassIssue;int flowInputIntent;uint flowPreparationVersion;
  string FlowInputIssue(string id)=>FlowWorkEligible?flowPassIssue??(flowByTarget.TryGetValue(id,out var group)?group.Issue:null):null;
  void ReleaseFlowInputs(){flowPreparationVersion++;foreach(var group in flowGroups.Values)group.Inputs?.Dispose();flowGroups.Clear();flowByTarget.Clear();flowPassIssue=null;flowPlacementFence=null;}
  bool FlowVesselReady(Vessel vessel)=>Available(vessel)&&editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(vessel.Id)!=true;
  void FlowGroupIssue(FlowGroup group,string reason){
   group.Ready=false;group.Issue=reason;
   foreach(var id in group.Plan.Members)if(vessels.TryGetValue(id,out var vessel)){vessel.Exposure=0;vessel.RainReady=false;vessel.RainReason=reason;}
  }
  RoomSpatialReadiness CheckFlowInputs(FlowGroup group,string[] targets,out string reason){
   var state=editor.SpatialCollisionReady(targets,out reason);if(state!=RoomSpatialReadiness.Ready)return state;
   if(group.Plan.Unknown){reason="Required liquid-flow cavity bounds are unavailable";return RoomSpatialReadiness.Unavailable;}
   foreach(var id in group.Plan.Members)if(!vessels.TryGetValue(id,out var vessel)||!FlowVesselReady(vessel)){
    reason="Required liquid-flow receiving or donor geometry is unavailable";return editor.LiquidNativePending(id)?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable;
   }
   foreach(var probe in group.Plan.Probes){state=editor.SpatialInputsReady(Array.Empty<string>(),probe.Bounds,probe.Bounds,false,probe.Actor,out reason);if(state!=RoomSpatialReadiness.Ready)return state;}
   reason=null;return RoomSpatialReadiness.Ready;
  }
  Vessel[][] CaptureFlowExecution(bool automatic){
   // Execute only complete, prepared quantity groups. Shared read-only solids
   // remain in spatial proofs/PhysX; they do not become quantity candidates.
   var result=new List<Vessel[]>();
   foreach(var group in flowGroups.Values){
    if(!group.Ready||!GroupMotionReady(group)||automatic&&group.Plan.Members.Any(id=>vessels[id].AutomaticStep==FlowWorkResources.Step))continue;
    var members=group.Plan.Members.Select(id=>vessels.TryGetValue(id,out var vessel)?vessel:null).ToArray();
    if(members.Any(vessel=>vessel==null||!vessel.Ready)){
     FlowGroupIssue(group,"Liquid-flow members changed; preparing a fresh pass");
     return Array.Empty<Vessel[]>(); // Never execute a prefix of a stale plan.
    }
    if(!FlowWorkResources.Consume(this,group.Plan.Members,out var workIssue)){
     foreach(var id in group.Plan.Members){var waiting=vessels[id];waiting.Exposure=0;waiting.RainReady=false;waiting.RainReason=workIssue;}
     continue;
    }
    if(automatic)foreach(var member in members)member.AutomaticStep=FlowWorkResources.Step;
    result.Add(members);
   }
   flowExecutionWork.Groups=result.Count;flowExecutionWork.Members=result.Sum(members=>members.Length);
   return result.ToArray();
  }
  bool PrepareFlowInputs(Vector3 gravity,Vector3 up){
   uint version=++flowPreparationVersion;flowPassIssue=null;flowInputIntent=world.SimulationIntent;
   CaptureFlowPlacementFence();
   var plans=CaptureFlowGroups(gravity,up);var next=new Dictionary<string,FlowGroup>(StringComparer.Ordinal);bool close=false;
   foreach(var plan in plans){var group=flowGroups.TryGetValue(plan.Key,out var old)?old:new FlowGroup();group.Plan=plan;group.Ready=false;next.Add(plan.Key,group);}
   foreach(var pair in flowGroups)if(!next.ContainsKey(pair.Key)){pair.Value.Inputs?.Dispose();}
   flowGroups=next;flowByTarget.Clear();foreach(var group in next.Values)foreach(var id in group.Plan.Members)flowByTarget[id]=group;
   foreach(var group in next.Values){
    var state=CheckFlowInputs(group,group.Plan.Targets,out var issue);
    bool changed=group.Inputs==null||group.Inputs.SourceChanged||!group.Plan.Targets.SequenceEqual(group.Inputs.Targets);
    bool recovered=state==RoomSpatialReadiness.Ready&&group.Issue!=null&&group.Inputs?.Pending!=true;
    bool affected=state!=RoomSpatialReadiness.Ready;
    if(affected&&group.Plan.Members.Any(Owns)){
     close=true;group.Inputs?.Dispose();group.Inputs=null;FlowGroupIssue(group,issue??"Liquid-flow dependencies changed; preparing a fresh pass");
    }else{
     if(changed||recovered){group.Inputs?.Dispose();group.Inputs=null;}
     group.Issue=state==RoomSpatialReadiness.Ready?null:issue;
    }
   }
   // Regrouping ready inputs preserves the live episode, including a bucket
   // moving between basins. An unavailable owned group closes it once. Return
   // immediately: listeners may edit the world or begin fresh independent flow.
   if(close&&Active){Finish(out _);return false;}
   uint before=flowEpoch;
   foreach(var group in next.Values){
    if(!group.Plan.Active){group.Inputs?.Dispose();group.Inputs=null;FlowGroupIssue(group,group.Issue??"Container is unavailable for physics");continue;}
    group.Inputs??=new RoomSpatialInputs(editor,"",()=>FlowWorkEligible,false,purpose:"Liquid flow",followPhysicsPlacements:true);
    group.Ready=group.Inputs.Require(group.Plan.Targets,(string[] ids,out string issue)=>CheckFlowInputs(group,ids,out issue),out var reason);
    if(version!=flowPreparationVersion)return false; // A nested pass/lifecycle now owns the state.
    if(!group.Ready){
     FlowGroupIssue(group,reason??"Preparing liquid-flow geometry");
     if(group.Plan.Members.Any(Owns)){group.Inputs.Dispose();group.Inputs=null;Finish(out _);return false;}
    }else group.Issue=null;
    if(flowEpoch!=before){flowPassIssue="Liquid-flow geometry changed; preparing a fresh pass";InvalidateRain(flowPassIssue);return false;}
   }
   if(!FlowPlacementCurrent()){
    flowPassIssue="Liquid-flow placement changed; preparing a fresh pass";
    foreach(var group in next.Values)FlowGroupIssue(group,flowPassIssue);
    return false;
   }
   foreach(var vessel in vessels.Values)vessel.Ready&=flowByTarget.TryGetValue(vessel.Id,out var group)&&group.Ready;
   return vessels.Values.Any(v=>v.Ready);
  }
 }
}
