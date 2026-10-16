// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Complete moving groups share the liquid opportunity clock. Logical work
    // units bound potential sampling; they are not a PhysX solver/CPU budget.
    internal static class PhysicsWorkResources
    {
        internal const int MaximumReservations=128,MaximumUnits=524288;
        internal const int MissingMediumChecks=32;
        internal const string Waiting="Physics is waiting for work capacity. Complete groups take turns; saved objects and valid motion are preserved.";
        sealed class Entry
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");
            internal RigidRoomItem Host;internal RoomResourceOwner Owner;internal int Units;
            internal RigidRoomItem[] Members;internal LiquidPouring Flow;internal SimulationWorkSchedule.Claim Work;
        }
        static readonly List<Entry> entries=new();
        // Logical allowances for displacement occupancy, native/missing medium
        // candidates, two participant environment proofs and shared media capture.
        // A proof/candidate may contain further bounded geometry work. Its internal
        // query count and engine cost are not represented by this score.
        internal static int Demand(int colliders)=>MediumDisplacement.Samples*(Math.Min(Math.Max(0,colliders),MediumDisplacement.MaximumColliders)+LiquidResources.MaximumInstances+MissingMediumChecks+2)+LiquidResources.MaximumInstances+MissingMediumChecks;
        static bool SameOwner(RoomResourceOwner a,RoomResourceOwner b)=>a.World==b.World&&a.Region==b.Region&&a.Target==b.Target;
        static bool Current(Entry e)=>e.Host&&e.Host.PhysicsWorkEligible&&e.Units==Demand(e.Host.PhysicsWorkColliders)&&SameOwner(e.Owner,e.Host.PhysicsWorkOwner);
        static void Remove(Entry e){entries.Remove(e);SimulationWorkSchedule.Remove(e.Work);}
        internal static void Release(RigidRoomItem host){foreach(var e in entries.Where(e=>ReferenceEquals(e.Host,host)).ToArray())Remove(e);}
        internal static void Collect()=>SimulationWorkSchedule.Collect();
        internal static void CollectLocal(){foreach(var e in entries.Where(e=>!e.Host||!e.Host.PhysicsWorkEligible).ToArray())Remove(e);}
        internal static void RefreshRequests(){foreach(var e in entries){e.Owner=e.Host.PhysicsWorkOwner;e.Units=Demand(e.Host.PhysicsWorkColliders);e.Work.ForceUnits=e.Units;}}
        internal static void LinkRequests(){
            foreach(var e in entries){
                var peers=e.Members.Where(x=>x&&x.PhysicsWorkEligible).Select(x=>entries.FirstOrDefault(p=>ReferenceEquals(p.Host,x))?.Work);
                if(!ReferenceEquals(e.Flow,null))peers=peers.Append(FlowWorkResources.FindClaim(e.Flow));
                e.Work.Dependencies=peers.ToArray();
            }
        }
        internal static bool Queued(RigidRoomItem host)=>SimulationWorkSchedule.Queued(entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host))?.Work);
        internal static bool TryAdmit(IEnumerable<RigidRoomItem> members,out string error)
        {
            Collect();error=Waiting;
            var wanted=members.Where(x=>x&&x.PhysicsWorkEligible).Distinct().ToArray();if(wanted.Length==0){error=null;return true;}
            var editor=wanted[0].PhysicsEditor;var flow=editor?editor.PhysicsFlowFor(wanted):null;
            var changes=wanted.Select(host=>(host,owner:host.PhysicsWorkOwner,units:Demand(host.PhysicsWorkColliders),old:entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host)))).ToArray();
            if(entries.Count+changes.Count(c=>c.old==null)>MaximumReservations){error="The physics work request queue is full; pause unused physics and retry";return false;}
            var previous=new List<(Entry e,RoomResourceOwner owner,int units)>();
            foreach(var change in changes){
                var e=change.old;
                if(e==null){e=new Entry{Host=change.host,Owner=change.owner,Units=change.units,Members=wanted,Flow=flow};var captured=e;e.Work=SimulationWorkSchedule.Register(()=>Current(captured),1,0);e.Work.ForceUnits=e.Units;entries.Add(e);}
                else previous.Add((e,e.Owner,e.Units));
                if(!e.Members.SequenceEqual(wanted)||!ReferenceEquals(e.Flow,flow)){e.Members=wanted;e.Flow=flow;SimulationWorkSchedule.Changed();}
                e.Owner=change.owner;e.Units=change.units;e.Work.ForceUnits=e.Units;
            }
            var claim=entries.First(e=>ReferenceEquals(e.Host,wanted[0])).Work;
            if(SimulationWorkSchedule.TryAdmit(claim)){error=null;return true;}
            error=SimulationWorkSchedule.Refusal(claim,Waiting);
            foreach(var old in previous){old.e.Owner=old.owner;old.e.Units=old.units;old.e.Work.ForceUnits=old.units;}
            return false;
        }
        internal static bool Ready(RigidRoomItem host,out string error)
        {
            error=Waiting;if(!host||!host.PhysicsWorkEligible){error="Physics or object ownership is unavailable";return false;}
            var entry=entries.FirstOrDefault(e=>ReferenceEquals(e.Host,host));if(entry==null)return false;
            if(!SimulationWorkSchedule.Ready(entry.Work)){error=SimulationWorkSchedule.Refusal(entry.Work,Waiting);return false;}
            error=null;return true;
        }
        internal static bool Consume(RigidRoomItem host,out string error){
            if(!Ready(host,out error))return false;
            var claim=entries.First(e=>ReferenceEquals(e.Host,host)).Work;
            if(SimulationWorkSchedule.Consume(claim))return true;
            error=Waiting;return false;
        }
        internal static JObject ObserveBudget()
        {
            Collect();return new JObject{["reservations"]=entries.Count,["units"]=entries.Where(e=>e.Work.Granted).Sum(e=>e.Units),["reservationLimit"]=MaximumReservations,["unitLimit"]=MaximumUnits,["samplesPerBody"]=MediumDisplacement.Samples,["collidersPerBody"]=MediumDisplacement.MaximumColliders,["nativeMediaPerSample"]=LiquidResources.MaximumInstances,["missingMediaPerSample"]=MissingMediumChecks};
        }
        internal static JObject ObserveSchedule(){
            Collect();int granted=entries.Count(e=>e.Work.Granted);
            return new JObject{["step"]=SimulationWorkSchedule.Step,["executions"]=granted,["executionLimit"]=MaximumReservations,["queued"]=entries.Count-granted,["queueLimit"]=MaximumReservations,["units"]=entries.Where(e=>e.Work.Granted).Sum(e=>e.Units),["unitLimit"]=MaximumUnits};
        }
        internal static JObject Observe(int index)
        {
            Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];bool current=Ready(e.Host,out _);
            return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["units"]=e.Units,["current"]=current};
        }
    }
}
