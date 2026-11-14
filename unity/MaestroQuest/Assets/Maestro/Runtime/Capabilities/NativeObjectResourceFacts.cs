// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class NativeObjectResourceFacts
    {
        public static BehaviourCatalog.FactDefinition SavedBudget()=>new("world.creationBudget",ProgramDataType.Read(JObject.Parse("{\"record\":{\"saved\":\"number\",\"savedLimit\":\"number\",\"remaining\":\"number\"}}")),
            "Saved creation capacity","Saved creations in the active world, including unloaded objects; excludes the included book and Maestro. Remaining counts creation identities only. Native roots, geometry, physics, components and file-size admission retain independent limits, so this is not a guarantee that any requested creation fits. Reading never loads objects or edits the world. Native object reservations are separately reported by runtime.objectBudget when diagnostic observations are available.",null,null,
            (context,args)=>context.Editor?ProgramValue.Literal(context.Editor.ObserveCreationBudget()):null,features:new[]{CreationLoading.Feature});
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.objectBudget",ProgramDataType.Read(JObject.Parse("{\"record\":{\"instances\":\"number\",\"preparing\":\"number\",\"ready\":\"number\",\"retiring\":\"number\",\"instanceLimit\":\"number\"}}")),
            "Live object budget","Process-wide native root reservations for user-created primitives, drawings, assemblies and imported-model hosts. Includes private preparation, disabled objects and deferred destruction across workspaces. The limit is independent of saved-object capacity, not measured memory or frame time. Book, Maestro, tools and other system roots are excluded; meshes, textures, lights, windows, ink, collision and simulation keep separate costs. Reading never creates, loads, retires or evicts a saved object.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(NativeObjectResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.objectReservation",ProgramDataType.Read(JObject.Parse("{\"record\":{\"reservationId\":\"text\",\"worldId\":\"text\",\"regionId\":\"text\",\"target\":\"text\",\"role\":\"text\",\"kind\":\"text\",\"phase\":\"text\",\"active\":\"boolean\"}}")),
            "Native object owner","Read one runtime.objectBudget reservation by index. Indices may shift; reservationId is ephemeral, not saved identity. Copied world/region/target identify the owner. Kind is the saved creation kind. Phase is preparing, ready or retiring. Ready means the native host was built, not published identity, visible geometry, imported-model readiness or running physics. Active only describes the root hierarchy. Disabled and retiring roots remain charged until destroyed. Reads are detached and cannot change saved data or load geometry.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,NativeObjectResources.MaximumInstances-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:NativeObjectResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
