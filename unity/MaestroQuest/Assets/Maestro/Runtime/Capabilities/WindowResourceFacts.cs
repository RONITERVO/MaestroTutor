// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class WindowResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.windowBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""masks"":""number"",""bundles"":""number"",""preparing"":""number"",""ready"":""number"",""retiring"":""number"",""maskLimit"":""number"",""bundleLimit"":""number""}}")),
            "Native window budget","Process-wide authored window mask and shared mesh/material bundle reservations across workspaces. Includes private preparation, inactive hosts and deferred destruction. One bundle serves a native view's masks and is released when its last window is removed. Limits are independent of saved-room quotas. Logical admission costs, not measured GPU memory or frame time; internal rendering, compositor and passthrough resources are excluded. Reading never allocates a mask, enables passthrough or evicts content.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(WindowResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.windowReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""kind"":""text"",""window"":""text"",""phase"":""text"",""active"":""boolean""}}")),
            "Native window owner","Read one runtime.windowBudget reservation by index. Indices may shift; reservationId is runtime-only. World, region and target identify its copied owner. Kind is mask or bundle; window is empty for a shared bundle. Phase is preparing, ready or retiring. Ready means native ownership transferred, including unpublished activation candidates; it does not certify publication or visibility. Active describes the host GameObject, not passthrough readiness. Resources remain charged until their owned native allocations are destroyed. Reads are detached and cannot change saved data or request passthrough.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,WindowResources.MaximumMasks+WindowResources.MaximumBundles-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:WindowResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
