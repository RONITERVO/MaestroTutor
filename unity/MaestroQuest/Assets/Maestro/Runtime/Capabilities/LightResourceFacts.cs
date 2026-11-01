// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class LightResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.lightBudget",ProgramDataType.Read(JObject.Parse("{\"record\":{\"lights\":\"number\",\"shadowFaces\":\"number\",\"preparing\":\"number\",\"ready\":\"number\",\"retiring\":\"number\",\"lightLimit\":\"number\",\"shadowFaceLimit\":\"number\"}}")),
            "Native lamp budget","Process-wide authored lamp and shadow-face reservations across workspaces. Includes private preparation, disabled lamps and deferred destruction. Limits are independent of saved-room quotas. Point shadows reserve six faces; spot shadows reserve one. Counts are logical admission costs, not measured GPU memory or frame time; Unity may retain internal shadow render targets after a Light is destroyed. Sun and other system lighting are excluded. Reading never creates, enables or evicts a lamp.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(LightResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.lightReservation",ProgramDataType.Read(JObject.Parse("{\"record\":{\"reservationId\":\"text\",\"worldId\":\"text\",\"regionId\":\"text\",\"target\":\"text\",\"light\":\"text\",\"kind\":\"text\",\"phase\":\"text\",\"shadowFaces\":\"number\",\"active\":\"boolean\"}}")),
            "Native lamp owner","Read one runtime.lightBudget reservation by index. Indices may shift; reservationId is runtime-only. World, region, target and light identify its copied owner. Kind is point or spot. Phase is preparing, ready or retiring. Ready means ownership transferred to a native view, not that the lamp is visible or emitting. Active describes the Light component. Disabled and retiring lamps remain charged until their roots are destroyed. Reads are detached and cannot change saved data or load resources.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,LightResources.MaximumLights-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:LightResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
