// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class LiquidResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.liquidBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""instances"":""number"",""preparing"":""number"",""ready"":""number"",""retiring"":""number"",""instanceLimit"":""number"",""streamPointLimit"":""number""}}")),
            "Native liquid budget","Process-wide authored liquid surface and potential stream owners across workspaces, including empty containers, private preparation, inactive hosts and deferred destruction. Each owner reserves one surface mesh, two materials and its two renderers before acceptance; live filling and pouring reuse them. Independent of saved container quotas. Logical ownership, not measured GPU memory, fluid solver cost or frame time. Per-tick simulation/query work, managed geometry buffers and engine internals are excluded. Reads never create water, load a view or start physics.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(LiquidResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.liquidReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""phase"":""text"",""active"":""boolean"",""surfaceAllocated"":""boolean"",""streamAllocated"":""boolean""}}")),
            "Native liquid owner","Read one runtime.liquidBudget reservation by index. Indices may shift; reservationId is runtime-only. World, region and target identify its copied owner. Phase is preparing, ready or retiring. Ready means native ownership transferred, including an unpublished or empty container; it does not certify water availability or simulation. Active describes the host GameObject. SurfaceAllocated and streamAllocated report still-existing native roots, which can be hidden or awaiting destruction. Reads are detached and cannot change saved quantities or start physics.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,LiquidResources.MaximumInstances-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:LiquidResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
