// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Stable potential-work grants, not a stopwatch or a PhysX solver budget.
    // Reserve the complete moving group before gravity or medium forces. A
    // newcomer cannot evict accepted work or receive half a group's allowance.
    internal static class PhysicsWorkResources
    {
        internal const int MaximumReservations=128,MaximumUnits=524288;
        internal const int MissingMediumChecks=32;
        internal const string Waiting="Physics is waiting for liquid-force work capacity. Pause unused physics or simplify collision shapes, then retry; saved objects are unchanged.";
        sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal RigidRoomItem Host;internal RoomResourceOwner Owner;internal int Units;
        }
        static readonly List<Entry> entries=new();
        // Logical allowances for displacement occupancy, native/missing medium
        // candidates, two participant environment proofs and shared media capture.
        // A proof/candidate may contain further bounded geometry work. Its internal
        // query count and engine cost are not represented by this score.
        internal static int Demand(int colliders)=>MediumDisplacement.Samples*(Math.Min(Math.Max(0,colliders),MediumDisplacement.MaximumColliders)+LiquidResources.MaximumInstances+MissingMediumChecks+2)+LiquidResources.MaximumInstances+MissingMediumChecks;
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region&&a.Target==b.Target;
        internal static void Release(RigidRoomItem host)=>entries.RemoveAll(e=>ReferenceEquals(e.Host,host));
        internal static void Collect()=>entries.RemoveAll(e=>!e.Host||!e.Host.PhysicsWorkEligible);
        internal static bool TryAdmit(IEnumerable<RigidRoomItem> members,out string error)
        {
            Collect();error=null;
            var wanted=members.Where(x=>x&&x.PhysicsWorkEligible).Distinct().ToArray();
            var changes=wanted.Select(host=>(host,owner:host.PhysicsWorkOwner,units:Demand(host.PhysicsWorkColliders),old:entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host)))).ToArray();
            long units=entries.Sum(e=>(long)e.Units);int count=entries.Count;
            foreach(var change in changes){units+=change.units-(change.old?.Units??0);if(change.old==null)count++;}
            if(count>MaximumReservations||units>MaximumUnits){error=Waiting;return false;}
            foreach(var change in changes){var entry=change.old;if(entry==null){entry=new Entry{Host=change.host};entries.Add(entry);}entry.Owner=change.owner;entry.Units=change.units;}
            return true;
        }
        internal static bool Ready(RigidRoomItem host,out string error)
        {
            error=Waiting;if(!host||!host.PhysicsWorkEligible){error="Physics or object ownership is unavailable";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(entry==null)return false;
            if(entry.Units!=Demand(host.PhysicsWorkColliders)||!SameOwner(entry.Owner,host.PhysicsWorkOwner)){error="The physics work demand or world owner changed; waiting for complete group admission";return false;}
            error=null;return true;
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=entries.Sum(e=>e.Units),["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["samplesPerBody"]=MediumDisplacement.Samples,["collidersPerBody"]=MediumDisplacement.MaximumColliders,["nativeMediaPerSample"]=LiquidResources.MaximumInstances,["missingMediaPerSample"]=MissingMediumChecks};
        }
        internal static JObject Observe(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];bool current=Ready(e.Host,out _);
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["units"]=e.Units,["current"]=current};
        }
    }
}
