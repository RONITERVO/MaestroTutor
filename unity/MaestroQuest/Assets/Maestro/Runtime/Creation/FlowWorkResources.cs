// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Fair, complete-host opportunities. Discovery has a distinct allowance, but
    // both are granted together; preparation/publication still have host-wide work.
    // This allowance does not own a journal write lease or native residency.
    internal static class FlowWorkResources
    {
        internal const int MaximumReservations=64,MaximumExecutions=32,MaximumUnits=524288;
        internal const int QueryHits=64,ScoopSamples=5,RainSamples=5;
        internal const string Waiting="Liquid flow is waiting for work capacity. Ready rooms take turns automatically; saved quantities are preserved.";
        // Potential pairwise immersion/scooping, bounded stream candidates/hits,
        // exposure samples and participant-proof allowances. Geometry/proof/engine
        // internals are excluded: this score is not instructions or elapsed time.
        internal static int Demand(int vessels){
            if(vessels<=0)return 0;if(vessels>1024)return int.MaxValue;long n=vessels;
            return (int)Math.Min(int.MaxValue,n*(16+LiquidPouring.Segments*(n+QueryHits+3)+2*n*(1+ScoopSamples*5)+RainSamples*(QueryHits+3)));
        }
        sealed class Entry {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal LiquidPouring Host;internal RoomResourceOwner Owner;internal int Vessels,Units,Objects,DiscoveryUnits;
            internal SimulationWorkSchedule.Claim Work;
        }
        static readonly List<Entry> entries=new();
        internal static long Step=>SimulationWorkSchedule.Step;
        internal static float StepSeconds=>SimulationWorkSchedule.StepSeconds;
        static int GrantedCount=>entries.Count(e=>e.Work.Granted);
        static long GrantedUnits=>entries.Where(e=>e.Work.Granted).Sum(e=>(long)e.Units);
        internal static void Advance(float seconds)=>SimulationWorkSchedule.Advance(seconds);
        static bool Current(Entry e)=>e.Host&&e.Host.FlowWorkEligible&&e.Vessels==e.Host.FlowWorkVessels&&e.Objects==e.Host.FlowDiscoveryObjects&&SameOwner(e.Owner,e.Host.FlowWorkOwner);
        internal static bool Queued(LiquidPouring host)=>SimulationWorkSchedule.Queued(FindClaim(host));
        internal static SimulationWorkSchedule.Claim FindClaim(LiquidPouring host)=>entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host))?.Work;
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        static void Remove(Entry e){entries.Remove(e);SimulationWorkSchedule.Remove(e.Work);}
        internal static void Release(LiquidPouring host){foreach(var e in entries.Where(e=>ReferenceEquals(e.Host,host)).ToArray())Remove(e);}
        internal static void Collect()=>SimulationWorkSchedule.Collect();
        internal static void CollectLocal(){foreach(var e in entries.Where(e=>!e.Host||!e.Host.FlowWorkEligible||Demand(e.Host.FlowWorkVessels)>MaximumUnits||FlowDiscoveryWork.Demand(e.Host.FlowWorkVessels,e.Host.FlowDiscoveryObjects)>FlowDiscoveryWork.MaximumUnits).ToArray())Remove(e);}
        internal static void RefreshRequests(){foreach(var e in entries){e.Owner=e.Host.FlowWorkOwner;e.Vessels=e.Host.FlowWorkVessels;e.Units=Demand(e.Vessels);e.Objects=e.Host.FlowDiscoveryObjects;e.DiscoveryUnits=FlowDiscoveryWork.Demand(e.Vessels,e.Objects);e.Work.FlowUnits=e.Units;e.Work.DiscoveryUnits=e.DiscoveryUnits;}}
        internal static bool TryAdmit(LiquidPouring host,out string error){
            Collect();error=Waiting;if(!host||!host.FlowWorkEligible)return false;
            int count=host.FlowWorkVessels,demand=Demand(count),objects=host.FlowDiscoveryObjects,discovery=FlowDiscoveryWork.Demand(count,objects);var owner=host.FlowWorkOwner;
            var old=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(old==null){
                if(entries.Count>=MaximumReservations||demand>MaximumUnits||discovery>FlowDiscoveryWork.MaximumUnits)return false;
                old=new Entry{Host=host,Owner=owner,Vessels=count,Units=demand,Objects=objects,DiscoveryUnits=discovery};var captured=old;
                old.Work=SimulationWorkSchedule.Register(()=>Current(captured),0,1);old.Work.FlowUnits=demand;old.Work.DiscoveryUnits=discovery;entries.Add(old);
            }
            var previous=(old.Owner,old.Vessels,old.Units,old.Objects,old.DiscoveryUnits);old.Owner=owner;old.Vessels=count;old.Units=demand;old.Objects=objects;old.DiscoveryUnits=discovery;old.Work.FlowUnits=demand;old.Work.DiscoveryUnits=discovery;
            if(SimulationWorkSchedule.TryAdmit(old.Work)){error=null;return true;}
            error=SimulationWorkSchedule.Refusal(old.Work,Waiting);
            (old.Owner,old.Vessels,old.Units,old.Objects,old.DiscoveryUnits)=previous;old.Work.FlowUnits=old.Units;old.Work.DiscoveryUnits=old.DiscoveryUnits;return false;
        }
        internal static bool Ready(LiquidPouring host,out string error){
            error=Waiting;if(!host||!host.FlowWorkEligible){error="Liquid flow is paused or unavailable";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(entry==null)return false;
            if(!SimulationWorkSchedule.Ready(entry.Work)){error=SimulationWorkSchedule.Refusal(entry.Work,Waiting);return false;}
            error=null;return true;
        }
        internal static JObject ObserveDiscovery(){
            Collect();return new JObject{["step"]=Step,["reservations"]=SimulationWorkSchedule.DiscoveryRequests,["executions"]=SimulationWorkSchedule.DiscoveryExecutions,["units"]=SimulationWorkSchedule.DiscoveryUnits,["unitLimit"]=FlowDiscoveryWork.MaximumUnits,["objectLimit"]=FlowDiscoveryWork.MaximumObjects};
        }
        internal static JObject ObserveBudget(){
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=GrantedUnits,["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["streamSegments"]=LiquidPouring.Segments,["queryHits"]=QueryHits,["scoopSamples"]=ScoopSamples,["rainSamples"]=RainSamples};
        }
        internal static JObject ObserveSchedule(){
            Collect();return new JObject{["step"]=Step,["executions"]=GrantedCount,["executionLimit"]=MaximumExecutions,["queued"]=entries.Count-GrantedCount,["queueLimit"]=MaximumReservations,["units"]=GrantedUnits,["unitLimit"]=MaximumUnits};
        }
        internal static JObject Observe(int index){
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["vessels"]=e.Vessels,["units"]=e.Units,["current"]=Ready(e.Host,out _)};
        }
    }
}
