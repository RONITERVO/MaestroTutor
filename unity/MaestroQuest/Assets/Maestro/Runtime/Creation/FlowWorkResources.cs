// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Stable complete-pass allowance; it does not own a journal write lease.
    internal static class FlowWorkResources
    {
        internal const int MaximumReservations=32,MaximumUnits=524288;
        internal const int QueryHits=64,ScoopSamples=5,RainSamples=5;
        internal const string Waiting="Liquid flow is waiting for work capacity. Pause unused rooms or reduce active liquid work, then retry; saved quantities are preserved.";
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
        }
        static readonly List<Entry> entries=new();
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        internal static void Release(LiquidPouring host)=>entries.RemoveAll(e=>ReferenceEquals(e.Host,host));
        internal static void Collect()=>entries.RemoveAll(e=>!e.Host||!e.Host.FlowWorkEligible);
        internal static bool TryAdmit(LiquidPouring host,out string error){
            Collect();error=Waiting;if(!host||!host.FlowWorkEligible)return false;
            int count=host.FlowWorkVessels,demand=Demand(count);var owner=host.FlowWorkOwner;
            var old=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            long total=entries.Sum(e=>(long)e.Units)+demand-(old?.Units??0);
            if(entries.Count+(old==null?1:0)>MaximumReservations||total>MaximumUnits)return false;
            var entry=old??new Entry{Host=host};if(old==null)entries.Add(entry);
            entry.Owner=owner;entry.Vessels=count;entry.Units=demand;error=null;return true;
        }
        internal static bool Ready(LiquidPouring host,out string error){
            error=Waiting;if(!host||!host.FlowWorkEligible){error="Liquid flow is paused or unavailable";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(entry==null)return false;
            if(entry.Vessels!=host.FlowWorkVessels||!SameOwner(entry.Owner,host.FlowWorkOwner)){error="The complete liquid-flow demand or world owner changed; waiting for admission";return false;}
            error=null;return true;
        }
        internal static JObject ObserveBudget(){
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=entries.Sum(e=>e.Units),["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["streamSegments"]=LiquidPouring.Segments,["queryHits"]=QueryHits,["scoopSamples"]=ScoopSamples,["rainSamples"]=RainSamples};
        }
        internal static JObject Observe(int index){
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["vessels"]=e.Vessels,["units"]=e.Units,["current"]=Ready(e.Host,out _)};
        }
    }
}
