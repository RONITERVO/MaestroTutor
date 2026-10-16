// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
 // One bounded opportunity for simulation work, with independent contact passes. Dependency closure
 // is admitted atomically: a liquid pass and all of its registered moving groups
 // cannot hold opposite halves of the allowance. Residency has a separate owner.
 internal static class SimulationWorkSchedule {
  internal sealed class Claim {
   internal int ForceUnits,FlowUnits,ContactUnits,Bodies,Rooms,Samplers;
   internal bool Granted;
   internal long Served,Order;
   internal Func<bool> Current;
   internal Claim[] Dependencies=Array.Empty<Claim>();
   internal Group Group;
  }
  internal sealed class Group {
   internal readonly List<Claim> Claims=new();
   internal bool Complete=true;
   internal long Force=>Claims.Sum(c=>(long)c.ForceUnits);
   internal long Flow=>Claims.Sum(c=>(long)c.FlowUnits);
   internal long Contact=>Claims.Sum(c=>(long)c.ContactUnits);
   internal int Contacts=>Claims.Sum(c=>c.Samplers);
   internal int Bodies=>Claims.Sum(c=>c.Bodies);
   internal int Rooms=>Claims.Sum(c=>c.Rooms);
   internal bool Fits=>Force<=PhysicsWorkResources.MaximumUnits&&Flow<=FlowWorkResources.MaximumUnits&&Bodies<=PhysicsWorkResources.MaximumReservations&&Rooms<=FlowWorkResources.MaximumExecutions&&Contact<=ContactWorkResources.MaximumUnits&&Contacts<=ContactWorkResources.MaximumExecutions;
   internal bool Current=>Complete&&Claims.All(c=>c.Current());
  }
  static readonly List<Claim> claims=new();
  static List<Group> groups=new();
  static long order;static bool dirty=true;
  internal static long Step {get;private set;}
  internal static float StepSeconds {get;private set;}=.05f;
  internal static Claim Register(Func<bool> current,int bodies,int rooms,int samplers=0){
   var claim=new Claim{Current=current,Bodies=bodies,Rooms=rooms,Samplers=samplers,Served=Step,Order=++order};claims.Add(claim);Changed();FlowWorkClock.Ensure();return claim;
  }
  internal static void Remove(Claim claim){claims.Remove(claim);Changed();}
  internal static void Changed()=>dirty=true;
  internal static void Collect(){FlowWorkResources.CollectLocal();PhysicsWorkResources.CollectLocal();ContactWorkResources.CollectLocal();}
  static void BuildGroups(){
   if(!dirty)return;dirty=false;PhysicsWorkResources.LinkRequests();groups=new();
   var index=claims.Select((c,i)=>(c,i)).ToDictionary(x=>x.c,x=>x.i);int[] parent=Enumerable.Range(0,claims.Count).ToArray();
   int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
   foreach(var claim in claims)foreach(var peer in claim.Dependencies)if(peer!=null&&index.TryGetValue(peer,out int other))parent[Root(other)]=Root(index[claim]);
   var byRoot=new Dictionary<int,Group>();
   foreach(var claim in claims){int id=Root(index[claim]);if(!byRoot.TryGetValue(id,out var group)){group=new Group();byRoot.Add(id,group);groups.Add(group);}group.Claims.Add(claim);claim.Group=group;}
   foreach(var claim in claims)if(claim.Dependencies.Any(peer=>peer==null||!index.ContainsKey(peer)))claim.Group.Complete=false;
  }
  internal static bool Queued(Claim claim){BuildGroups();return claim!=null&&claims.Contains(claim)&&claim.Group.Fits&&claim.Group.Current;}
  internal static bool Ready(Claim claim)=>Queued(claim)&&claim.Group.Claims.All(c=>c.Granted);
  internal static bool TryAdmit(Claim claim){
   BuildGroups();if(!Queued(claim))return false;var group=claim.Group;
   var outside=claims.Where(c=>c.Granted&&!ReferenceEquals(c.Group,group)).ToArray();
   if(outside.Sum(c=>(long)c.ForceUnits)+group.Force>PhysicsWorkResources.MaximumUnits||outside.Sum(c=>(long)c.FlowUnits)+group.Flow>FlowWorkResources.MaximumUnits||outside.Sum(c=>c.Bodies)+group.Bodies>PhysicsWorkResources.MaximumReservations||outside.Sum(c=>c.Rooms)+group.Rooms>FlowWorkResources.MaximumExecutions||outside.Sum(c=>(long)c.ContactUnits)+group.Contact>ContactWorkResources.MaximumUnits||outside.Sum(c=>c.Samplers)+group.Contacts>ContactWorkResources.MaximumExecutions)return false;
   Grant(group);return true;
  }
  static void Grant(Group group){foreach(var claim in group.Claims){claim.Granted=true;claim.Served=Step;}}
  internal static string Refusal(Claim claim,string waiting){
   BuildGroups();if(claim==null||!claims.Contains(claim))return waiting;
   if(!claim.Group.Fits)return "The complete physics/liquid group exceeds work capacity; simplify shapes or containers. Saved objects are unchanged.";
   if(!claim.Group.Current)return "A complete physics/liquid work dependency changed or is unavailable";
   return waiting;
  }
  // The clock alone changes age. Repeated calls, passive reads and caller order
  // cannot rotate the queue. A finite, unchanged group that fits gets a turn.
  internal static void Advance(float seconds){
   if(!float.IsFinite(seconds)||seconds<=0)return;
   Collect();Step++;StepSeconds=Mathf.Clamp(seconds,.001f,.1f);
   FlowWorkResources.RefreshRequests();PhysicsWorkResources.RefreshRequests();ContactWorkResources.RefreshRequests();
   foreach(var claim in claims)claim.Granted=false;
   BuildGroups();long force=0,flow=0,contact=0;int bodies=0,rooms=0,samplers=0;
   foreach(var group in groups.OrderBy(g=>g.Claims.Min(c=>c.Served)).ThenBy(g=>g.Claims.Min(c=>c.Order))){
    if(!group.Fits||!group.Current||force+group.Force>PhysicsWorkResources.MaximumUnits||flow+group.Flow>FlowWorkResources.MaximumUnits||bodies+group.Bodies>PhysicsWorkResources.MaximumReservations||rooms+group.Rooms>FlowWorkResources.MaximumExecutions||contact+group.Contact>ContactWorkResources.MaximumUnits||samplers+group.Contacts>ContactWorkResources.MaximumExecutions)continue;
    Grant(group);force+=group.Force;flow+=group.Flow;bodies+=group.Bodies;rooms+=group.Rooms;contact+=group.Contact;samplers+=group.Contacts;
   }
  }
 }
}
