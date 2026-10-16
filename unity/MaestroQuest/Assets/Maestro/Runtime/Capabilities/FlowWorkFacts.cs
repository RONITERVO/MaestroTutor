// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class FlowWorkFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.flowWorkBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservations"":""number"",""units"":""number"",""reservationLimit"":""number"",""unitLimit"":""number"",""streamSegments"":""number"",""queryHits"":""number"",""scoopSamples"":""number"",""rainSamples"":""number""}}")),
            "Liquid transfer work budget","Process-wide stable potential-work reservations for complete room pouring/scooping/rain passes. All registered vessels, including empty receivers and missing native views, contribute to demand. Admission precedes quantity changes; free moving vessels and their connected groups also require it before gravity. Hands and authored animations retain ownership. Refusal closes prior accepted flow and releases its writer, hides streams, invalidates rain exposure and never accrues skipped time. Logical units cover pairwise cavities, bounded stream candidates/hits, exposure and participant-proof allowances; they are not measured CPU time, frame rate, memory or a PhysX budget. Geometry/proof internals, forces, contacts, external observations and engine costs are excluded. Reads may retire ineligible grants but never admit work, load geometry, start physics or publish a flow episode.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(FlowWorkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.flowWorkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""vessels"":""number"",""units"":""number"",""current"":""boolean""}}")),
            "Liquid transfer work reservation","Read one runtime.flowWorkBudget grant by index. Indices may shift; reservationId is runtime-only. World/region identify the copied room owner. Current means complete potential demand and owner still match, not that native geometry, a transfer or simulation is ready. Resizing credits the old grant and cannot evict peers or admit a room prefix. Paused, inactive or destroyed hosts release potential work; explicit internal ticks can step a disabled automatic loop for desktop verification. Reads are detached and cannot resume transfers or physics.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,FlowWorkResources.MaximumReservations-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:FlowWorkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
