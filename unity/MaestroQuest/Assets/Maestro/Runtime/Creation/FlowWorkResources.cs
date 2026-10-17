// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
 // Discovery belongs to the host. Execution belongs to complete quantity groups,
 // represented by stable canonical member claims, independent of native views.
 internal static class FlowWorkResources {
  internal const int MaximumReservations=64,MaximumExecutions=32,MaximumUnits=524288;
  internal const int QueryHits=64,ScoopSamples=5,RainSamples=5;
  internal const string Preparing="Liquid flow is preparing required inputs before requesting execution";
  internal const string Waiting="Liquid flow is waiting for work capacity. Ready groups take turns automatically; saved quantities are preserved.";
  internal static int Demand(int vessels){
   if(vessels<=0)return 0;if(vessels>1024)return int.MaxValue;long n=vessels;
   return (int)Math.Min(int.MaxValue,n*(16+LiquidPouring.Segments*(n+QueryHits+3)+2*n*(1+ScoopSamples*5)+RainSamples*(QueryHits+3)));
  }
  sealed class Member {
   internal string Target;internal string[] Peers=Array.Empty<string>();internal bool Requested,Discovered;
   internal int Units;internal SimulationWorkSchedule.Claim Work;
  }
  sealed class Entry {
   internal readonly string Id=Guid.NewGuid().ToString("N");
   internal LiquidPouring Host;internal RoomResourceOwner Owner,DiscoveryOwner;
   internal int DiscoveryVessels,Objects,DiscoveryUnits;
   internal long MembershipRevision;
   internal readonly Dictionary<string,Member> Members=new(StringComparer.Ordinal);
   internal SimulationWorkSchedule.Claim Discovery;
  }
  static readonly List<Entry> entries=new();
  internal static long Step=>SimulationWorkSchedule.Step;
  internal static float StepSeconds=>SimulationWorkSchedule.StepSeconds;
  static bool Granted(Entry e)=>e.Members.Values.Any(m=>m.Work.Granted);
  static long GrantedUnits=>entries.Sum(e=>e.Members.Values.Where(m=>m.Work.Granted).Sum(m=>(long)m.Units));
  internal static void Advance(float seconds)=>SimulationWorkSchedule.Advance(seconds);
  static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
  static bool Current(Entry e,Member m)=>e.Host&&e.Host.FlowWorkEligible&&m.Requested&&SameOwner(e.Owner,e.Host.FlowWorkOwner)&&e.Host.FlowTarget(m.Target)&&(!m.Discovered||e.Host.GroupContains(m.Target,m.Peers));
  static bool DiscoveryCurrent(Entry e)=>e.Host&&e.Host.FlowWorkEligible&&e.DiscoveryVessels==e.Host.FlowWorkVessels&&e.Objects==e.Host.FlowDiscoveryObjects&&SameOwner(e.DiscoveryOwner,e.Host.FlowWorkOwner);
  static Entry Find(LiquidPouring host)=>entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
  internal static SimulationWorkSchedule.Claim FindDiscoveryClaim(LiquidPouring host)=>Find(host)?.Discovery;
  internal static SimulationWorkSchedule.Claim FindClaim(LiquidPouring host,string target){var e=Find(host);return e!=null&&e.Members.TryGetValue(target,out var member)?member.Work:null;}
  internal static SimulationWorkSchedule.Claim[] ExecutionClaims(LiquidPouring host)=>Find(host)?.Members.Values.Select(m=>m.Work).ToArray()??Array.Empty<SimulationWorkSchedule.Claim>();
  internal static bool QueuedDiscovery(LiquidPouring host)=>SimulationWorkSchedule.Queued(FindDiscoveryClaim(host));
  internal static bool Queued(LiquidPouring host)=>ExecutionClaims(host).Any(SimulationWorkSchedule.Queued);
  static void Remove(Entry e){entries.Remove(e);foreach(var m in e.Members.Values)SimulationWorkSchedule.Remove(m.Work);SimulationWorkSchedule.Remove(e.Discovery);}
  internal static void Release(LiquidPouring host){foreach(var e in entries.Where(e=>ReferenceEquals(e.Host,host)).ToArray())Remove(e);}
  internal static void Collect()=>SimulationWorkSchedule.Collect();
  internal static void CollectLocal(){
   foreach(var e in entries.Where(e=>!e.Host||!e.Host.FlowWorkEligible||!SameOwner(e.Owner,e.Host.FlowWorkOwner)||FlowDiscoveryWork.Demand(e.Host.FlowWorkVessels,e.Host.FlowDiscoveryObjects)>FlowDiscoveryWork.MaximumUnits).ToArray())Remove(e);
  }
  internal static void RefreshRequests(){
   foreach(var e in entries){
    e.DiscoveryOwner=e.Host.FlowWorkOwner;e.DiscoveryVessels=e.Host.FlowWorkVessels;e.Objects=e.Host.FlowDiscoveryObjects;
    e.DiscoveryUnits=FlowDiscoveryWork.Demand(e.DiscoveryVessels,e.Objects);e.Discovery.DiscoveryUnits=e.DiscoveryUnits;
   }
  }
  static Entry Request(LiquidPouring host){
   if(!host||!host.FlowWorkEligible)return null;
   var entry=Find(host);if(entry!=null)return entry;
   int count=host.FlowWorkVessels,objects=host.FlowDiscoveryObjects,discovery=FlowDiscoveryWork.Demand(count,objects);
   if(entries.Count>=MaximumReservations||count>RoomContainer.MaximumPerRoom||discovery>FlowDiscoveryWork.MaximumUnits)return null;
   entry=new Entry{Host=host,Owner=host.FlowWorkOwner,DiscoveryOwner=host.FlowWorkOwner,DiscoveryVessels=count,Objects=objects,DiscoveryUnits=discovery};
   var captured=entry;entry.Discovery=SimulationWorkSchedule.Register(()=>DiscoveryCurrent(captured),0,0,service:host.DiscoveryService);
   entry.Discovery.DiscoveryUnits=discovery;entries.Add(entry);
   // Before discovery, retain a conservative queued dependency for moving
   // containers. It cannot execute, but a waiting discovery must retain motion.
   SetGroups(entry,new[]{new FlowWorkGroupRequest{Members=host.FlowTargets,Active=true}},false);
   return entry;
  }
  internal static bool TryDiscover(LiquidPouring host,out string error){
   Collect();error=Waiting;var e=Request(host);if(e==null)return false;
   var previous=(e.DiscoveryOwner,e.DiscoveryVessels,e.Objects,e.DiscoveryUnits);
   e.DiscoveryOwner=host.FlowWorkOwner;e.DiscoveryVessels=host.FlowWorkVessels;e.Objects=host.FlowDiscoveryObjects;e.DiscoveryUnits=FlowDiscoveryWork.Demand(e.DiscoveryVessels,e.Objects);e.Discovery.DiscoveryUnits=e.DiscoveryUnits;
   if(SimulationWorkSchedule.TryAdmit(e.Discovery)){error=null;return true;}
   error=SimulationWorkSchedule.Refusal(e.Discovery,Waiting);
   (e.DiscoveryOwner,e.DiscoveryVessels,e.Objects,e.DiscoveryUnits)=previous;e.Discovery.DiscoveryUnits=e.DiscoveryUnits;return false;
  }
  internal static bool DiscoveryReady(LiquidPouring host,out string error){
   error=Waiting;if(!host||!host.FlowWorkEligible){error="Liquid flow is paused or unavailable";return false;}
   var e=Find(host);if(e==null)return false;
   if(!SimulationWorkSchedule.Ready(e.Discovery)){error=SimulationWorkSchedule.Refusal(e.Discovery,Waiting);return false;}
   error=null;return true;
  }
  internal static void UpdateGroups(LiquidPouring host){
   var e=Find(host);if(e==null)return;
   SetGroups(e,host.WorkGroups,true);
  }
  static void SetGroups(Entry e,FlowWorkGroupRequest[] groups,bool discovered){
   bool membershipChanged=false;
   var host=e.Host;var wanted=new HashSet<string>(groups.SelectMany(g=>g.Members),StringComparer.Ordinal);
   foreach(var id in e.Members.Keys.Where(id=>!wanted.Contains(id)).ToArray()){SimulationWorkSchedule.Remove(e.Members[id].Work);e.Members.Remove(id);membershipChanged=true;}
   foreach(var group in groups){
    int units=Demand(group.Members.Length)/group.Members.Length;
    foreach(string id in group.Members){
     if(!e.Members.TryGetValue(id,out var member)){
      member=new Member{Target=id};var captured=member;membershipChanged=true;
      member.Work=SimulationWorkSchedule.Register(()=>Current(e,captured),0,0,service:host.FlowService(id));
      member.Work.Prepared=()=>captured.Discovered&&SimulationWorkSchedule.Ready(e.Discovery)&&host.GroupInputsReady(id);
      member.Work.ExecutionCurrent=()=>host.FlowExecutionCurrent(id);
      e.Members.Add(id,member);
     }
     bool changed=member.Units!=units||!member.Peers.SequenceEqual(group.Members);
     membershipChanged|=changed||member.Requested!=group.Active||member.Discovered!=discovered;
     if(changed)member.Work.Granted=false; // Release only unused booking; Consumed remains charged.
     member.Units=units;member.Peers=group.Members;member.Requested=group.Active;member.Discovered=discovered;
     member.Work.FlowUnits=units;member.Work.Rooms=id==group.Members[0]?1:0;
    }
   }

   if(membershipChanged)e.MembershipRevision++;
   SimulationWorkSchedule.Changed();
  }
  internal static void LinkRequests(){
   foreach(var e in entries)foreach(var member in e.Members.Values){
    var peers=member.Peers.Where(id=>id!=member.Target).Select(id=>e.Members[id].Work);
    var body=e.Host.FlowMovingMember(member.Target);
    if(body)peers=peers.Append(PhysicsWorkResources.FindClaim(body));
    member.Work.Dependencies=peers.ToArray();
   }
  }
  internal static void DeferExecution(LiquidPouring host){var e=Find(host);if(e!=null)foreach(var member in e.Members.Values)member.Requested=false;}
  internal static bool TryExecute(LiquidPouring host,out string error){
   Collect();if(!DiscoveryReady(host,out error))return false;
   var e=Find(host);bool ready=false;error=Waiting;
   foreach(var member in e.Members.Values.Where(m=>m.Peers.Length>0&&m.Target==m.Peers[0])){
    if(SimulationWorkSchedule.TryAdmit(member.Work)&&SimulationWorkSchedule.Ready(member.Work))ready=true;
    else if(!ready)error=SimulationWorkSchedule.Refusal(member.Work,Waiting);
   }
   if(ready)error=null;return ready;
  }
  internal static bool TryAdmit(LiquidPouring host,out string error){error=Waiting;if(!host||!host.PrepareWorkInputs(out error))return false;host.PrepareGroupMotion();return TryExecute(host,out error);}
  internal static bool Ready(LiquidPouring host,out string error){
   if(!DiscoveryReady(host,out error))return false;
   var e=Find(host);if(e.Members.Values.Any(m=>SimulationWorkSchedule.Ready(m.Work))){error=null;return true;}
   error=Waiting;return false;
  }
  internal static bool Ready(LiquidPouring host,string target,out string error){
   if(!DiscoveryReady(host,out error))return false;
   var claim=FindClaim(host,target);
   if(!SimulationWorkSchedule.Ready(claim)){error=SimulationWorkSchedule.Refusal(claim,Waiting);return false;}
   error=null;return true;
  }
  internal static bool ConsumeDiscovery(LiquidPouring host,out string error){
   if(!DiscoveryReady(host,out error))return false;
   if(SimulationWorkSchedule.Consume(FindDiscoveryClaim(host)))return true;error=Waiting;return false;
  }
  internal static bool Consume(LiquidPouring host,string[] members,out string error){
   error=Waiting;
   if(members.Length==0||!host.GroupContains(members[0],members)||!members.All(id=>Ready(host,id,out _)))return false;
   foreach(var id in members)if(!SimulationWorkSchedule.Consume(FindClaim(host,id)))return false;
   error=null;return true;
  }
  static (Entry entry,Member first)[] Groups()=>entries.SelectMany(e=>e.Members.Values.Where(m=>m.Peers.Length>0&&m.Target==m.Peers[0]).OrderBy(m=>m.Target,StringComparer.Ordinal).Select(m=>(e,m))).ToArray();
  static bool GroupBooked(Entry e,Member first)=>first.Peers.All(id=>e.Members[id].Work.Granted);
  internal static JObject ObserveGroups(){
   Collect();var groups=Groups();
   return new JObject{["step"]=Step,["groups"]=groups.Length,["active"]=groups.Count(g=>g.first.Requested),
    ["booked"]=groups.Count(g=>GroupBooked(g.entry,g.first)),["ready"]=groups.Count(g=>SimulationWorkSchedule.Ready(g.first.Work)),
    ["executionLimit"]=MaximumExecutions,["groupLimit"]=MaximumReservations*RoomContainer.MaximumPerRoom,["units"]=GrantedUnits,["unitLimit"]=MaximumUnits};
  }
  internal static JObject ObserveGroup(int index){
   Collect();var groups=Groups();if(index<0||index>=groups.Length)return null;var(e,m)=groups[index];
   return new JObject{["step"]=Step,["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,
    ["membershipRevision"]=e.MembershipRevision,["firstMember"]=m.Target,["members"]=m.Peers.Length,["units"]=m.Peers.Sum(id=>e.Members[id].Units),
    ["active"]=m.Requested,["discovered"]=m.Discovered,["booked"]=GroupBooked(e,m),["ready"]=SimulationWorkSchedule.Ready(m.Work),
    ["lastServed"]=m.Peers.Min(id=>e.Members[id].Work.Served)};
  }
  internal static JObject ObserveGroupMember(int index,int memberIndex){
   Collect();var groups=Groups();if(index<0||index>=groups.Length)return null;var(e,m)=groups[index];
   if(memberIndex<0||memberIndex>=m.Peers.Length)return null;var member=e.Members[m.Peers[memberIndex]];
   return new JObject{["step"]=Step,["reservationId"]=e.Id,["membershipRevision"]=e.MembershipRevision,["firstMember"]=m.Target,["target"]=member.Target,
    ["units"]=member.Units,["lastServed"]=member.Work.Served,["order"]=member.Work.Order};
  }
  internal static JObject ObserveDiscovery(){
   Collect();return new JObject{["step"]=Step,["reservations"]=SimulationWorkSchedule.DiscoveryRequests,["executions"]=SimulationWorkSchedule.DiscoveryExecutions,["units"]=SimulationWorkSchedule.DiscoveryUnits,["unitLimit"]=FlowDiscoveryWork.MaximumUnits,["objectLimit"]=FlowDiscoveryWork.MaximumObjects};
  }
  internal static JObject ObserveBudget(){
   Collect();return new JObject{["reservations"]=entries.Count,["units"]=GrantedUnits,["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["streamSegments"]=LiquidPouring.Segments,["queryHits"]=QueryHits,["scoopSamples"]=ScoopSamples,["rainSamples"]=RainSamples};
  }
  internal static JObject ObserveSchedule(){
   Collect();int granted=entries.Count(Granted);
   return new JObject{["step"]=Step,["executions"]=granted,["executionLimit"]=MaximumExecutions,["queued"]=entries.Count-granted,["queueLimit"]=MaximumReservations,["units"]=GrantedUnits,["unitLimit"]=MaximumUnits};
  }
  internal static JObject Observe(int index){
   Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
   return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["vessels"]=e.Host.FlowWorkVessels,["units"]=e.Members.Values.Where(m=>m.Requested).Sum(m=>m.Units),["current"]=Ready(e.Host,out _)};
  }
 }
}
