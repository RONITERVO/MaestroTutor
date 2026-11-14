// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class ProceduralResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.proceduralBudget",ProgramDataType.Read(JObject.Parse("{\"record\":{\"instances\":\"number\",\"preparing\":\"number\",\"ready\":\"number\",\"retiring\":\"number\",\"parts\":\"number\",\"customVertices\":\"number\",\"instanceLimit\":\"number\",\"partLimit\":\"number\",\"customVertexLimit\":\"number\"}}")),
            "Live procedural geometry budget","Process-wide enforced admission for recipe instances, part hierarchies and generated custom-mesh source vertices. Includes private preparation, disabled instances and replaced or closed-workspace geometry until Unity destroys its roots and owned meshes. Limits are independent of saved-world capacity and leave bounded headroom for replacement. Counts are not measured RAM/VRAM or frame time; customVertices excludes shared primitive meshes, imported models, terrain, drawing, collision, material and texture costs. Reading never loads, evicts, retries or changes saved objects.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(RecipeResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.proceduralReservation",ProgramDataType.Read(JObject.Parse("{\"record\":{\"reservationId\":\"text\",\"worldId\":\"text\",\"regionId\":\"text\",\"target\":\"text\",\"role\":\"text\",\"phase\":\"text\",\"parts\":\"number\",\"customVertices\":\"number\",\"active\":\"boolean\"}}")),
            "Procedural geometry owner","Read one runtime.proceduralBudget instance by index. Indices can shift; reservationId identifies this native allocation, not a saved object. Copied world, region and target identify its owner. Phase is preparing, ready or retiring. Ready means built geometry, not visible, audible or running behaviour; active only describes the root hierarchy. Disabled geometry and pending destruction retain their reservation. Reads are detached and cannot activate, unload or delete anything.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,RecipeResources.MaximumInstances-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:RecipeResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
