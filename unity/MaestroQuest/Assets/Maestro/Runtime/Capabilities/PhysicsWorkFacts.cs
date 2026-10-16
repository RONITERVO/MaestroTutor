// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class PhysicsWorkFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.physicsWorkBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservations"":""number"",""units"":""number"",""reservationLimit"":""number"",""unitLimit"":""number"",""samplesPerBody"":""number"",""collidersPerBody"":""number"",""nativeMediaPerSample"":""number"",""missingMediaPerSample"":""number""}}")),
            "Physics work budget","Process-wide bounded requests for potential fixed-step free-body liquid-force work, including standalone bodies. Reservations include queued bodies; units count booked allowances. Complete connected groups and their whole-host liquid pass share one fair opportunity before gravity or fluid forces. Liquid passes conservatively couple all registered moving vessel groups because current transfer loops scan every vessel. A scheduled delay preserves native inputs, valid velocity/spin and live quantities. Real motion/source/ownership changes invalidate held motion. New requests cannot evict unrelated allowances within the opportunity. Oversize complete groups or a full queue are explicitly refused. Logical units cover bounded displacement samples, candidate media and participant environment proofs, not measured CPU time, GPU memory, frame rate or PhysX solver cost. Proof internals, pouring, scooping, rain, contact events, navigation and external observation queries are excluded. Independent of saved quotas and native allocation budgets. Reads only retire ineligible grants; they never admit work, load geometry or start physics.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(PhysicsWorkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Schedule()=>new("runtime.physicsWorkSchedule",ProgramDataType.Read(JObject.Parse(@"{""record"":{""step"":""number"",""executions"":""number"",""executionLimit"":""number"",""queued"":""number"",""queueLimit"":""number"",""units"":""number"",""unitLimit"":""number""}}")),
            "Physics simulation opportunities","Read the same opportunity clock as runtime.flowWorkSchedule. Executions count bodies with booked force allowances, including stale allowances awaiting refresh; readiness still requires the entire coupled group and its native inputs. Complete physics/liquid dependency groups take turns by oldest unserved opportunity, then stable registration order. A finite unchanged group fitting both work limits receives a turn under continuing demand. Oversize groups and a full request queue are refused. Scheduled waits preserve valid motion without accumulating force/time debt. Reading cannot advance the clock, acquire work, load geometry or restart physics. Logical allowances are not measured device or PhysX performance.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(PhysicsWorkResources.ObserveSchedule()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.physicsWorkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""units"":""number"",""current"":""boolean""}}")),
            "Physics work reservation","Read one runtime.physicsWorkBudget request by index, including queued bodies. Indices may shift; reservationId is runtime-only. World, region and target identify the copied owner; empty scope denotes a standalone body. Current means its complete coupled work group has an allowance and all member demand/owners still match, not that native geometry or physics is ready. Paused, held, inactive, animated or destroyed bodies release their potential work. Reads are detached and cannot admit work or resume physics.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,PhysicsWorkResources.MaximumReservations-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:PhysicsWorkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
