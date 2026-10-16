// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // A stable allowance for an entire room's contact pass. Reserve worst-case
    // samples before geometry queries; never publish only an admitted prefix.
    internal static class ContactWorkResources
    {
        internal const int MaximumReservations=32,MaximumUnits=524288;
        internal const int AvatarSamples=6,InputSamples=2;
        internal const string Waiting="Water contacts are waiting for sampling capacity. Pause unused rooms or simplify collision shapes, then retry; no contact events are inferred while waiting.";
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
        }
        static readonly List<Entry> entries=new();
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region;
        internal static void Release(LiquidPouring host)=>entries.RemoveAll(e=>ReferenceEquals(e.Host,host));
        internal static void Collect()=>entries.RemoveAll(e=>!e.Host||!e.Host.ContactWorkEligible);
        internal static bool TryAdmit(LiquidPouring host,out string error)
        {
            Collect();error=Waiting;if(!host||!host.ContactWorkEligible)return false;
            int demand=host.ContactWorkDemand;var owner=host.ContactWorkOwner;
            var old=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));
            long total=entries.Sum(e=>(long)e.Units)+demand-(old?.Units??0);
            if(entries.Count+(old==null?1:0)>MaximumReservations||total>MaximumUnits)return false;
            var entry=old??new Entry{Host=host};if(old==null)entries.Add(entry);
            entry.Owner=owner;entry.Units=demand;error=null;return true;
        }
        internal static bool Ready(LiquidPouring host,out string error)
        {
            error=Waiting;if(!host||!host.ContactWorkEligible){error="Contact sampling is paused";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(entry==null)return false;
            if(entry.Units!=host.ContactWorkDemand||!SameOwner(entry.Owner,host.ContactWorkOwner)){error="The complete contact sampling demand or world owner changed; waiting for admission";return false;}
            error=null;return true;
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=entries.Sum(e=>e.Units),["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["samplesPerBody"]=MediumDisplacement.Samples,["collidersPerBody"]=MediumDisplacement.MaximumColliders,["avatarSamples"]=AvatarSamples,["inputSamples"]=InputSamples};
        }
        internal static JObject Observe(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["units"]=e.Units,["current"]=Ready(e.Host,out _)};
        }
    }
}
