// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class InkResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.inkBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""surfaces"":""number"",""strokes"":""number"",""vertices"":""number"",""preparing"":""number"",""ready"":""number"",""retiring"":""number"",""surfaceLimit"":""number"",""strokeLimit"":""number"",""vertexLimit"":""number""}}")),
            "Native ink budget","Process-wide authored drawing-surface, stroke and source-vertex reservations across workspaces, including live pencil previews, private preparation, inactive hosts and deferred destruction. Each source point reserves three vertices; duplicate points may render fewer. Independent of saved-room quotas. Logical admission costs, not measured GPU memory or frame time. System artwork and selection outlines are excluded. Reading never creates or evicts ink.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(InkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.inkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""role"":""text"",""source"":""text"",""kind"":""text"",""phase"":""text"",""vertices"":""number"",""active"":""boolean""}}")),
            "Native ink owner","Read one runtime.inkBudget reservation by index across surfaces and strokes. Indices may shift; reservationId is runtime-only. World, region, target, role and source identify its copied owner. Phase is preparing, ready or retiring. Ready means a native host exists, including a private candidate; it does not certify publication or visibility. Active describes the host GameObject, not whether it is visible. Retiring resources remain charged until owned native allocations are destroyed. Reads are detached and cannot change saved data or load resources.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,InkResources.MaximumSurfaces+InkResources.MaximumStrokes-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:InkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
