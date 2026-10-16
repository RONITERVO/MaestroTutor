// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    public static class ContactWorkFacts
    {
        static bool Available(BehaviourCatalog.FactContext context)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>();
        static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
        public static BehaviourCatalog.FactDefinition Budget()=>new("runtime.contactWorkBudget",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservations"":""number"",""units"":""number"",""reservationLimit"":""number"",""unitLimit"":""number"",""samplesPerBody"":""number"",""collidersPerBody"":""number"",""avatarSamples"":""number"",""inputSamples"":""number""}}")),
            "Water contact work budget","Process-wide stable potential-work reservations for complete room water-contact passes, including held/animated objects, six avatar joints and two tracked inputs. Admission occurs before displacement capture and contact queries. Capacity refusal makes contacts unknown and discards their baseline; resuming cannot manufacture enter/exit/crossing events or old ripples. Logical units cover candidate/proof allowances, not measured CPU time or frame rate. Proof internals, force sampling, pouring, scooping, rain, external observations and engine costs are excluded. Independent of saved quotas and native allocation budgets. Reads may retire ineligible grants, but never admit sampling, start physics or publish events.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(ContactWorkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.contactWorkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""units"":""number"",""current"":""boolean""}}")),
            "Water contact work reservation","Read one runtime.contactWorkBudget grant by index. Indices may shift; reservationId is runtime-only. World and region identify the copied room owner. Current means the complete potential demand and owner still match, not that geometry, tracking or any contact is known. Demand growth cannot evict another grant or admit a room prefix. Paused, inactive or destroyed sampling hosts release work; held and animated objects remain contact participants. Reads are detached and cannot resume sampling.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,ContactWorkResources.MaximumReservations-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:ContactWorkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
