// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
 internal sealed class FlowWorkGroupRequest {
  internal string[] Members;internal bool Active,Ready;
 }
 public sealed partial class LiquidPouring {
  string workScope;
  SimulationWorkSchedule.ServiceRecord discoveryService=new();
  void EnsureWorkScope(){
   var owner=FlowWorkOwner;string scope=owner.World+":"+owner.Region;
   if(workScope==scope)return;workScope=scope;discoveryService=new();
   foreach(var vessel in vessels.Values){vessel.WorkService=new();vessel.AutomaticStep=-1;}
  }
  internal SimulationWorkSchedule.ServiceRecord DiscoveryService {get{EnsureWorkScope();return discoveryService;}}
  internal SimulationWorkSchedule.ServiceRecord FlowService(string target){EnsureWorkScope();return vessels[target].WorkService;}
  internal bool FlowExecutionCurrent(string target)=>FlowPlacementCurrent()&&flowByTarget.TryGetValue(target,out var group)&&GroupMotionReady(group,false);
  internal RigidRoomItem FlowMovingMember(string target)=>vessels.TryGetValue(target,out var vessel)&&vessel.Rigid&&vessel.Rigid.PhysicsWorkEligible?vessel.Rigid:null;
  internal string[] FlowTargets=>vessels.Keys.ToArray();
  internal FlowWorkGroupRequest[] WorkGroups=>flowGroups.Values.Select(group=>new FlowWorkGroupRequest{
   Members=group.Plan.Members,Active=group.Plan.Active,Ready=group.Ready
  }).ToArray();
  internal bool GroupInputsReady(string target)=>flowByTarget.TryGetValue(target,out var group)&&group.Ready&&
   group.Inputs!=null&&!group.Inputs.SourceChanged&&group.Plan.Members.All(id=>vessels.TryGetValue(id,out var vessel)&&FlowVesselReady(vessel));
  internal bool PrepareWorkInputs(out string issue){
   issue=FlowWorkResources.Preparing;
   if(!FlowWorkResources.TryDiscover(this,out issue)||!FlowWorkResources.ConsumeDiscovery(this,out issue))return false;
   Physics.SyncTransforms();
   foreach(var vessel in vessels.Values){vessel.Ready=FlowVesselReady(vessel);if(vessel.Ready)vessel.Opening=new ContainerFlowGeometry.Opening(vessel.Live,vessel.Item.transform);}
   uint previous=flowPreparationVersion;
   bool ready=PrepareFlowInputs(Physics.gravity,-Physics.gravity.normalized);
   if(flowPreparationVersion!=unchecked(previous+1)){issue=FlowWorkResources.Preparing;return false;}
   FlowWorkResources.UpdateGroups(this);
   if(!ready){issue=FlowWorkResources.Preparing;return false;}
   issue=null;return true;
  }
  internal bool PreparingGroupMotion {get;private set;}
  internal void PrepareGroupMotion(){
   if(PreparingGroupMotion)return;PreparingGroupMotion=true;
   try{
    foreach(var body in vessels.Values.Select(v=>v.Rigid).Where(body=>body&&body.PhysicsWorkEligible).Distinct().ToArray())
     editor.PreparePhysicsIsland(body.GetComponent<RoomItem>());
   }finally{PreparingGroupMotion=false;}
  }
  internal bool GroupContains(string target,string[] members)=>flowByTarget.TryGetValue(target,out var group)&&group.Plan.Members.SequenceEqual(members);
  // Joint creation needs prepared inputs and an allowance before the native joint exists.
  // Actual liquid execution still requires every joint to be active.
  bool GroupMotionReady(FlowGroup group,bool joints=true){
   foreach(string id in group.Plan.Members){
    var vessel=vessels[id];
    if(vessel.Rigid&&vessel.Rigid.PhysicsWorkEligible&&!editor.PhysicsIslandReady(vessel.Item,joints,out _,false))return false;
   }
   return true;
  }
 }
}
