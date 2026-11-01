// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class NativePhysicsResourceFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.physicsBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""bodies"":""number"",""joints"":""number"",""preparing"":""number"",""ready"":""number"",""retiring"":""number"",""bodyLimit"":""number"",""jointLimit"":""number""}}")),
            "Native physics budget","Process-wide authored Rigidbody/material and potential connection-joint reservations across workspaces. Counts include private preparation, inactive bodies, paused or broken enabled connections and deferred native destruction. One joint slot survives retirement and recreation without overlapping native joints. Independent of saved quotas. Logical native ownership, not measured PhysX memory or frame time. Built-in book/avatar/tools, tracked pushers, system bodies, broadphase/contact data and simulation query work are excluded. Reading never creates bodies, starts physics or evicts content.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(NativePhysicsResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.physicsReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""target"":""text"",""kind"":""text"",""phase"":""text"",""active"":""boolean"",""allocated"":""boolean""}}")),
            "Native physics owner","Read one runtime.physicsBudget reservation by index. Indices may shift; reservationId is runtime-only. World, region and target identify its copied owner. Kind is body or joint. Phase is preparing, ready or retiring. Ready means native ownership transferred, including unpublished candidates or a potential joint waiting for alignment and physics inputs; it does not certify simulation. Active describes the host GameObject. Allocated reports a still-existing Rigidbody or Joint, which may be kinematic, disabled or awaiting destruction. Body slots also retain their owned physics material until destruction. Reads are detached and cannot change saved data or start physics.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,NativePhysicsResources.MaximumBodies+NativePhysicsResources.MaximumJoints-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:NativePhysicsResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
