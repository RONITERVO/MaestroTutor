// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Independent discovery precedes complete-host execution opportunities.
    // Preparation/publication and execution charges remain host-wide.
    // This allowance does not own a journal write lease or native residency.
    internal static class FlowWorkResources
    {
        internal const int MaximumReservations=64,MaximumExecutions=32,MaximumUnits=524288;
        internal const int QueryHits=64,ScoopSamples=5,RainSamples=5;
        internal const string Preparing="Liquid flow is preparing required inputs before requesting execution";
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
            internal LiquidPouring Host;internal RoomResourceOwner Owner,DiscoveryOwner;
            internal int Vessels,Units,DiscoveryVessels,Objects,DiscoveryUnits;
            internal bool ExecutionRequested;
            internal SimulationWorkSchedule.Claim Work,Discovery;
        }
        static readonly List<Entry> entries=new();
        internal static long Step=>SimulationWorkSchedule.Step;
        internal static float StepSeconds=>SimulationWorkSchedule.StepSeconds;
        static int GrantedCount=>entries.Count(e=>e.Work.Granted);
        static long GrantedUnits=>entries.Where(e=>e.Work.Granted).Sum(e=>(long)e.Units);
        internal static void Advance(float seconds)=>SimulationWorkSchedule.Advance(seconds);
        static bool Current(Entry e)=>e.Host&&e.Host.FlowWorkEligible&&e.ExecutionRequested&&e.Vessels==e.Host.FlowWorkVessels&&SameOwner(e.Owner,e.Host.FlowWorkOwner);
        static bool DiscoveryCurrent(Entry e)=>e.Host&&e.Host.FlowWorkEligible&&e.DiscoveryVessels==e.Host.FlowWorkVessels&&e.Objects==e.Host.FlowDiscoveryObjects&&SameOwner(e.DiscoveryOwner,e.Host.FlowWorkOwner);
        internal static bool QueuedDiscovery(LiquidPouring host)=>SimulationWorkSchedule.Queued(FindDiscoveryClaim(host));
        internal static SimulationWorkSchedule.Claim FindDiscoveryClaim(LiquidPouring host)=>entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host))?.Discovery;
        internal static bool Queued(LiquidPouring host)=>SimulationWorkSchedule.Queued(FindClaim(host));
        internal static SimulationWorkSchedule.Claim FindClaim(LiquidPouring host)=>entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host))?.Work;
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        static void Remove(Entry e){entries.Remove(e);SimulationWorkSchedule.Remove(e.Work);SimulationWorkSchedule.Remove(e.Discovery);}
        internal static void Release(LiquidPouring host){foreach(var e in entries.Where(e=>ReferenceEquals(e.Host,host)).ToArray())Remove(e);}
        internal static void Collect()=>SimulationWorkSchedule.Collect();
        internal static void CollectLocal(){foreach(var e in entries.Where(e=>!e.Host||!e.Host.FlowWorkEligible||Demand(e.Host.FlowWorkVessels)>MaximumUnits||FlowDiscoveryWork.Demand(e.Host.FlowWorkVessels,e.Host.FlowDiscoveryObjects)>FlowDiscoveryWork.MaximumUnits).ToArray())Remove(e);}
        internal static void RefreshRequests(){
            foreach(var e in entries){
                e.Owner=e.DiscoveryOwner=e.Host.FlowWorkOwner;
                e.Vessels=e.DiscoveryVessels=e.Host.FlowWorkVessels;e.Objects=e.Host.FlowDiscoveryObjects;
                e.Units=Demand(e.Vessels);e.DiscoveryUnits=FlowDiscoveryWork.Demand(e.DiscoveryVessels,e.Objects);
                e.Work.FlowUnits=e.Units;e.Discovery.DiscoveryUnits=e.DiscoveryUnits;
            }
        }
        static Entry Request(LiquidPouring host){
            if(!host||!host.FlowWorkEligible)return null;
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(entry!=null)return entry;
            int count=host.FlowWorkVessels,objects=host.FlowDiscoveryObjects;
            int units=Demand(count),discovery=FlowDiscoveryWork.Demand(count,objects);
            if(entries.Count>=MaximumReservations||units>MaximumUnits||discovery>FlowDiscoveryWork.MaximumUnits)return null;
            entry=new Entry{Host=host,Owner=host.FlowWorkOwner,DiscoveryOwner=host.FlowWorkOwner,Vessels=count,DiscoveryVessels=count,Units=units,Objects=objects,DiscoveryUnits=discovery};
            var captured=entry;
            entry.Discovery=SimulationWorkSchedule.Register(()=>DiscoveryCurrent(captured),0,0);
            entry.Discovery.DiscoveryUnits=discovery;
            entry.Work=SimulationWorkSchedule.Register(()=>Current(captured),0,1);
            entry.Work.FlowUnits=units;
            entry.Work.Prepared=()=>SimulationWorkSchedule.Ready(captured.Discovery);
            entries.Add(entry);return entry;
        }
        internal static bool TryDiscover(LiquidPouring host,out string error){
            Collect();error=Waiting;var e=Request(host);if(e==null)return false;
            var previous=(e.DiscoveryOwner,e.DiscoveryVessels,e.Objects,e.DiscoveryUnits);
            e.DiscoveryOwner=host.FlowWorkOwner;e.DiscoveryVessels=host.FlowWorkVessels;e.Objects=host.FlowDiscoveryObjects;
            e.DiscoveryUnits=FlowDiscoveryWork.Demand(e.DiscoveryVessels,e.Objects);e.Discovery.DiscoveryUnits=e.DiscoveryUnits;
            if(SimulationWorkSchedule.TryAdmit(e.Discovery)){error=null;return true;}
            error=SimulationWorkSchedule.Refusal(e.Discovery,Waiting);
            (e.DiscoveryOwner,e.DiscoveryVessels,e.Objects,e.DiscoveryUnits)=previous;e.Discovery.DiscoveryUnits=e.DiscoveryUnits;return false;
        }
        internal static bool DiscoveryReady(LiquidPouring host,out string error){
            error=Waiting;if(!host||!host.FlowWorkEligible){error="Liquid flow is paused or unavailable";return false;}
            var e=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(e==null)return false;
            if(!SimulationWorkSchedule.Ready(e.Discovery)){error=SimulationWorkSchedule.Refusal(e.Discovery,Waiting);return false;}
            error=null;return true;
        }
        internal static void DeferExecution(LiquidPouring host){
            var e=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(e!=null)e.ExecutionRequested=false;
            // Already booked units remain until the opportunity advances. Input
            // waiting must not refund a grant while peers may still be using it.
        }
        internal static bool TryExecute(LiquidPouring host,out string error){
            Collect();if(!DiscoveryReady(host,out error))return false;
            var e=entries.First(v=>ReferenceEquals(v.Host,host));
            var previous=(e.Owner,e.Vessels,e.Units);
            e.ExecutionRequested=true;e.Owner=host.FlowWorkOwner;e.Vessels=host.FlowWorkVessels;e.Units=Demand(e.Vessels);e.Work.FlowUnits=e.Units;
            if(SimulationWorkSchedule.TryAdmit(e.Work)){error=null;return true;}
            error=SimulationWorkSchedule.Refusal(e.Work,Waiting);
            (e.Owner,e.Vessels,e.Units)=previous;e.Work.FlowUnits=e.Units;return false;
        }
        // Moving physics can request both stages before the quantity tick. Its
        // complete execution group still waits if discovery has no opportunity.
        internal static bool TryAdmit(LiquidPouring host,out string error){
            Collect();error=Waiting;var e=Request(host);if(e==null)return false;
            // Physics has already requested execution. Keep that request queued
            // while discovery waits, so a scheduled hold retains valid momentum.
            e.ExecutionRequested=true;
            return TryDiscover(host,out error)&&TryExecute(host,out error);
        }
        internal static bool Ready(LiquidPouring host,out string error){
            if(!DiscoveryReady(host,out error))return false;
            var entry=entries.First(e=>ReferenceEquals(e.Host,host));
            if(!entry.ExecutionRequested){error=Preparing;return false;}
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
