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
   internal readonly Bounds Bounds;internal readonly string Actor;internal readonly bool Vessels;
   internal FlowProbe(Bounds bounds,string actor,bool vessels){Bounds=bounds;Actor=actor;Vessels=vessels;}
  }
  readonly List<FlowProbe> flowProbes=new();
  readonly HashSet<string> flowTargets=new(StringComparer.Ordinal);
  RoomSpatialInputs flowInputs;string flowInputIssue;int flowInputRevision,flowInputIntent;
  string FlowInputIssue=>FlowWorkEligible?flowInputIssue:null;
  void ReleaseFlowInputs(){flowInputs?.Dispose();flowInputs=null;flowInputIssue=null;flowProbes.Clear();flowTargets.Clear();}
  void WaitForFlowInputs(string reason){flowInputIssue=reason;InvalidateRain(reason);Finish(out _);}
  bool FlowVesselReady(Vessel vessel)=>Available(vessel)&&editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(vessel.Id)!=true;
  void FlowProbeAt(Bounds bounds,string actor,bool includeVessels){
   bounds.Expand(.002f);flowProbes.Add(new(bounds,actor,includeVessels));flowTargets.Add(actor);
   foreach(var id in editor.SpatialInputTargets(bounds,bounds,false,actor))flowTargets.Add(id);
   if(!includeVessels)return;
   // Empty receiving cavities matter too, and can extend outside their artwork.
   foreach(var candidate in vessels.Values){
    if(candidate.Id==actor)continue;var extent=editor.MissingLiquidExtent(candidate.Id,candidate.Live,out _);
    if(!extent.Known||!extent.HasBounds||extent.Bounds.Intersects(bounds))flowTargets.Add(candidate.Id);
   }
  }
  void CaptureFlowInputs(Vector3 gravity,Vector3 up){
   flowProbes.Clear();flowTargets.Clear();var weather=editor.CurrentWeather;var rainVelocity=WeatherCover.Velocity(editor);
   foreach(var vessel in vessels.Values){
    if(!vessel.Ready)continue;
    var extent=editor.MissingLiquidExtent(vessel.Id,vessel.Live,out _);
    if(extent.Known&&extent.HasBounds)FlowProbeAt(extent.Bounds,vessel.Id,true);
    // Earlier donors can fill this vessel before its own turn in the same pass.
    // Prove every possible stream at this pose, independent of initial quantity.
    if(vessel.Live.capacityMl-ContainerFlowGeometry.RetainedCapacity(vessel.Live,vessel.Item.transform.rotation,up,RoomTerrainReservoir.Native(vessel.Live,vessel.Item.transform))>=.000001){
     var origin=ContainerFlowGeometry.Lip(vessel.Live,vessel.Item.transform,up,out var outward);var velocity=outward*.15f;
     if(vessel.Body&&!vessel.Body.isKinematic)velocity+=Vector3.ClampMagnitude(vessel.Body.GetPointVelocity(origin),3);
     var bounds=new Bounds(origin,Vector3.zero);
     for(int i=1;i<=Segments;i++){float t=i*.025f;var next=origin+velocity*t+gravity*(.5f*t*t);bounds.Encapsulate(next);if(!world.CanSimulate(next,vessel.Item))break;}
     FlowProbeAt(bounds,vessel.Id,true);
    }
    // Pouring/scooping can make room or empty a different liquid before rain.
    // Actual collection still checks the resulting amount and liquid identity.
    if(weather.Rain<=0)continue;
    var normal=vessel.Item.transform.rotation*vessel.Live.frame.rotation*Vector3.up;
    if(Vector3.Dot(normal,-rainVelocity)/6<=.001f)continue;
    var rainBounds=new Bounds(RainOpeningPoint(vessel,0),Vector3.zero);
    for(int sample=0;sample<5;sample++){var point=RainOpeningPoint(vessel,sample);rainBounds.Encapsulate(point);rainBounds.Encapsulate(point-rainVelocity.normalized*WeatherCover.Distance);}
    FlowProbeAt(rainBounds,vessel.Id,false);
   }
  }
  RoomSpatialReadiness CheckFlowInputs(string[] targets,out string reason){
   var state=editor.SpatialMovementReady(targets,null,out reason);if(state!=RoomSpatialReadiness.Ready)return state;
   foreach(var id in targets){
    var item=editor.Find(id);var rigid=item?item.GetComponent<RigidRoomItem>():null;
    if(rigid&&!rigid.GeometryReady){reason="Required liquid-flow collision geometry is unavailable";return RoomSpatialReadiness.Unavailable;}
   }
   foreach(var probe in flowProbes){
    state=editor.SpatialInputsReady(Array.Empty<string>(),probe.Bounds,probe.Bounds,false,probe.Actor,out reason);if(state!=RoomSpatialReadiness.Ready)return state;
    if(!probe.Vessels)continue;
    foreach(var vessel in vessels.Values){
     if(vessel.Id==probe.Actor)continue;
     var extent=editor.MissingLiquidExtent(vessel.Id,vessel.Live,out _);
     if(extent.Known&&extent.HasBounds&&!extent.Bounds.Intersects(probe.Bounds))continue;
     if(!FlowVesselReady(vessel)){reason="Required liquid-flow receiving or donor geometry is unavailable";return editor.LiquidNativePending(vessel.Id)?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable;}
    }
   }
   reason=null;return RoomSpatialReadiness.Ready;
  }
  bool PrepareFlowInputs(Vector3 gravity,Vector3 up){
   CaptureFlowInputs(gravity,up);var targets=flowTargets.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
   if(targets.Length==0){flowInputs?.Dispose();flowInputs=null;flowInputIssue=null;return true;}
   var state=CheckFlowInputs(targets,out var issue);
   bool changed=flowInputs==null||flowInputRevision!=editor.Revision||flowInputIntent!=world.SimulationIntent||flowInputs.SourceChanged||!targets.SequenceEqual(flowInputs.Targets);
   // Loading must not run while an earlier accepted episode owns the writer.
   // Publish it and return: callbacks may replace the world or start fresh flow.
   if(Active&&(changed||state!=RoomSpatialReadiness.Ready)){
    flowInputs?.Dispose();flowInputs=null;WaitForFlowInputs(issue??"Liquid-flow dependencies changed; preparing a fresh pass");return false;
   }
   bool recovered=state==RoomSpatialReadiness.Ready&&flowInputIssue!=null&&flowInputs?.Pending!=true;
   if(changed||recovered){flowInputs?.Dispose();flowInputs=null;}
   flowInputs??=new RoomSpatialInputs(editor,"",()=>FlowWorkEligible&&FlowWorkResources.Ready(this,out _),false,purpose:"Liquid flow");flowInputRevision=editor.Revision;flowInputIntent=world.SimulationIntent;
   uint before=flowEpoch;
   if(!flowInputs.Require(targets,CheckFlowInputs,out issue)){WaitForFlowInputs(issue??"Preparing liquid-flow geometry");return false;}
   // Native publication may have introduced new pouring/scooping sources.
   // Build the complete dependency set from those live views on the next pass.
   if(flowEpoch!=before){flowInputIssue="Liquid-flow geometry changed; preparing a fresh pass";InvalidateRain(flowInputIssue);return false;}
   flowInputIssue=null;return true;
  }
 }
}
