// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Fair, complete-host opportunities. The current bounded pass still scans all
    // vessels; charging only its smaller spatial groups would undercount that work.
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
            internal LiquidPouring Host;internal RoomResourceOwner Owner;internal int Vessels,Units;
            internal bool Granted;internal long Served,Order;
        }
        static readonly List<Entry> entries=new();
        static long order;
        internal static long Step {get;private set;}
        internal static float StepSeconds {get;private set;}=.05f;
        static int GrantedCount=>entries.Count(e=>e.Granted);
        static long GrantedUnits=>entries.Where(e=>e.Granted).Sum(e=>(long)e.Units);
        // Only the early fixed-step driver advances selection. Neither requests,
        // diagnostics nor a different ordering of callers advances this clock.
        internal static void Advance(float seconds){
            if(!float.IsFinite(seconds)||seconds<=0)return;
            Collect();Step++;StepSeconds=UnityEngine.Mathf.Clamp(seconds,.001f,.1f);
            foreach(var entry in entries){entry.Granted=false;entry.Owner=entry.Host.FlowWorkOwner;entry.Vessels=entry.Host.FlowWorkVessels;entry.Units=Demand(entry.Vessels);}
            long remaining=MaximumUnits;int slots=MaximumExecutions;
            foreach(var entry in entries.OrderBy(e=>e.Served).ThenBy(e=>e.Order)){
                if(slots==0||entry.Units>remaining)continue;
                entry.Granted=true;entry.Served=Step;remaining-=entry.Units;slots--;
            }
        }
        internal static bool Queued(LiquidPouring host)=>host&&host.FlowWorkEligible&&entries.Any(e=>ReferenceEquals(e.Host,host)&&e.Vessels==host.FlowWorkVessels&&SameOwner(e.Owner,host.FlowWorkOwner));
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        internal static void Release(LiquidPouring host)=>entries.RemoveAll(e=>ReferenceEquals(e.Host,host));
        internal static void Collect()=>entries.RemoveAll(e=>!e.Host||!e.Host.FlowWorkEligible||Demand(e.Host.FlowWorkVessels)>MaximumUnits);
        internal static bool TryAdmit(LiquidPouring host,out string error){
            Collect();error=Waiting;if(!host||!host.FlowWorkEligible)return false;
            int count=host.FlowWorkVessels,demand=Demand(count);var owner=host.FlowWorkOwner;
            var old=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(old==null){
                if(entries.Count>=MaximumReservations||demand>MaximumUnits)return false;
                old=new Entry{Host=host,Owner=owner,Vessels=count,Units=demand,Served=Step,Order=++order};entries.Add(old);
            }
            long total=GrantedUnits+demand-(old.Granted?old.Units:0);
            if(GrantedCount+(old.Granted?0:1)>MaximumExecutions||total>MaximumUnits)return false;
            // Credit a resize within this opportunity without evicting a peer.
            old.Owner=owner;old.Vessels=count;old.Units=demand;old.Granted=true;old.Served=Step;error=null;return true;
        }
        internal static bool Ready(LiquidPouring host,out string error){
            error=Waiting;if(!host||!host.FlowWorkEligible){error="Liquid flow is paused or unavailable";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(entry==null||!entry.Granted)return false;
            if(entry.Vessels!=host.FlowWorkVessels||!SameOwner(entry.Owner,host.FlowWorkOwner)){error="The complete liquid-flow demand or world owner changed; waiting for admission";return false;}
            error=null;return true;
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
