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
            "Physics work budget","Process-wide stable reservations for potential fixed-step free-body liquid-force work, including standalone bodies. Connected moving groups are admitted atomically before gravity or fluid forces; existing grants are not evicted by newcomers. Logical units cover bounded displacement samples, candidate media and participant environment proofs, not measured CPU time, GPU memory, frame rate or PhysX solver cost. Proof internals, pouring, scooping, rain, contact events, navigation and external observation queries are excluded. Independent of saved quotas and native allocation budgets. Reads only retire ineligible grants; they never admit work, load geometry or start physics.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(PhysicsWorkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.physicsWorkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""units"":""number"",""current"":""boolean""}}")),
            "Physics work reservation","Read one runtime.physicsWorkBudget grant by index. Indices may shift; reservationId is runtime-only. World, region and target identify the copied owner; empty scope denotes a standalone body. Current means this member still matches its reserved demand and owner, not that its complete group, geometry or physics is ready. Paused, held, inactive, animated or destroyed bodies release their potential work. Reads are detached and cannot admit work or resume physics.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,PhysicsWorkResources.MaximumReservations-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:PhysicsWorkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
