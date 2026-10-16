// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Fair independent opportunities for an entire room's contact pass. Charge worst-case
    // samples before geometry queries; never publish only an admitted prefix.
    internal static class ContactWorkResources
    {
        internal const int MaximumReservations=64,MaximumExecutions=32,MaximumUnits=524288;
        internal const int AvatarSamples=6,InputSamples=2;
        internal const string Waiting="Water contacts are waiting for sampling capacity. Complete room passes take turns; missed events are never replayed.";
        // Logical candidate/proof allowances, not instructions or frame time.
        // Contact samples test both occupancy and swept surface crossings, and
        // check missing media before and inside sampling. Proof internals excluded.
        internal const int PerPoint=2*LiquidResources.MaximumInstances+2*PhysicsWorkResources.MissingMediumChecks+4;
        internal const int Shared=LiquidResources.MaximumInstances+PhysicsWorkResources.MissingMediumChecks;
        internal static int BodyDemand(int colliders)=>MediumDisplacement.Samples*(Math.Min(Math.Max(0,colliders),MediumDisplacement.MaximumColliders)+PerPoint);
        internal const int BaseDemand=(AvatarSamples+InputSamples)*PerPoint+Shared;
        sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal LiquidPouring Host;internal RoomResourceOwner Owner;internal int Units;
            internal SimulationWorkSchedule.Claim Work;
        }
        static readonly List<Entry> entries=new();
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        static bool Current(Entry e)=>e.Host&&e.Host.ContactWorkEligible&&e.Units==e.Host.ContactWorkDemand&&SameOwner(e.Owner,e.Host.ContactWorkOwner);
        static void Remove(Entry e){entries.Remove(e);SimulationWorkSchedule.Remove(e.Work);}
        internal static void Release(LiquidPouring host){foreach(var e in entries.Where(e=>ReferenceEquals(e.Host,host)).ToArray())Remove(e);}
        internal static void Collect()=>SimulationWorkSchedule.Collect();
        internal static void CollectLocal(){foreach(var e in entries.Where(e=>!e.Host||!e.Host.ContactWorkEligible||e.Host.ContactWorkDemand>MaximumUnits).ToArray())Remove(e);}
        internal static void RefreshRequests(){foreach(var e in entries){e.Owner=e.Host.ContactWorkOwner;e.Units=e.Host.ContactWorkDemand;e.Work.ContactUnits=e.Units;}}
        internal static bool Queued(LiquidPouring host)=>SimulationWorkSchedule.Queued(entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host))?.Work);
        internal static bool TryAdmit(LiquidPouring host,out string error)
        {
            Collect();error=Waiting;if(!host||!host.ContactWorkEligible)return false;
            int demand=host.ContactWorkDemand;var owner=host.ContactWorkOwner;
            var old=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            if(demand>MaximumUnits){error="The complete contact pass exceeds sampling capacity; simplify collision shapes or reduce its participants";return false;}
            if(old==null){
                if(entries.Count>=MaximumReservations){error="The contact sampling request queue is full; pause unused rooms and retry";return false;}
                old=new Entry{Host=host,Owner=owner,Units=demand};var captured=old;
                old.Work=SimulationWorkSchedule.Register(()=>Current(captured),0,0,1);old.Work.ContactUnits=demand;entries.Add(old);
            }
            var previous=(old.Owner,old.Units);old.Owner=owner;old.Units=demand;old.Work.ContactUnits=demand;
            if(SimulationWorkSchedule.TryAdmit(old.Work)){error=null;return true;}
            (old.Owner,old.Units)=previous;old.Work.ContactUnits=old.Units;return false;
        }
        internal static bool Ready(LiquidPouring host,out string error)
        {
            error=Waiting;if(!host||!host.ContactWorkEligible){error="Contact sampling is paused";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(entry==null)return false;
            if(!Current(entry)){error="The complete contact sampling demand or world owner changed; waiting for admission";return false;}
            if(!SimulationWorkSchedule.Ready(entry.Work))return false;
            error=null;return true;
        }
        internal static bool Consume(LiquidPouring host,out string error){
            if(!Ready(host,out error))return false;
            var claim=entries.First(e=>ReferenceEquals(e.Host,host)).Work;
            if(SimulationWorkSchedule.Consume(claim))return true;
            error=Waiting;return false;
        }
        internal static JObject ObserveSchedule(){
            Collect();int granted=entries.Count(e=>e.Work.Granted);
            return new JObject{["step"]=SimulationWorkSchedule.Step,["executions"]=granted,["executionLimit"]=MaximumExecutions,["queued"]=entries.Count-granted,["queueLimit"]=MaximumReservations,["units"]=entries.Where(e=>e.Work.Granted).Sum(e=>e.Units),["unitLimit"]=MaximumUnits};
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=entries.Where(e=>e.Work.Granted).Sum(e=>e.Units),["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["samplesPerBody"]=MediumDisplacement.Samples,["collidersPerBody"]=MediumDisplacement.MaximumColliders,["avatarSamples"]=AvatarSamples,["inputSamples"]=InputSamples};
        }
        internal static JObject Observe(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["units"]=e.Units,["current"]=Ready(e.Host,out _)};
        }
    }
}
