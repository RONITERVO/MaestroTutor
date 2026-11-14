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
            "Water contact work budget","Process-wide bounded requests for complete room water-contact passes, including held/animated objects, six avatar joints and two tracked inputs. Reservations include queued passes; units count booked allowances. Fair admission occurs before displacement capture and contact queries on the shared simulation clock, independently of physics/flow execution. Scheduled waits make contacts unknown and discard crossing continuity; existing ripples finish naturally. Resuming cannot manufacture enter/exit/crossing events or old ripples. Oversize passes and a full request queue are explicit refusals. Consumed charges survive request removal until the next opportunity; inspect runtime.simulationWorkLedger for committed capacity. Logical units cover candidate/proof allowances, not measured CPU time or frame rate. Proof internals, force sampling, pouring, scooping, rain, external observations and engine costs are excluded. Independent of saved quotas and native allocation budgets. Reads may retire ineligible grants, but never admit sampling, start physics or publish events.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(ContactWorkResources.ObserveBudget()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Schedule()=>new("runtime.contactWorkSchedule",ProgramDataType.Read(JObject.Parse(@"{""record"":{""step"":""number"",""executions"":""number"",""executionLimit"":""number"",""queued"":""number"",""queueLimit"":""number"",""units"":""number"",""unitLimit"":""number""}}")),
            "Contact sampling opportunities","Read the same opportunity clock as runtime.physicsWorkSchedule and runtime.flowWorkSchedule. Complete contact passes take independent fair turns; booked allowances may await refreshed demand. A finite unchanged fitting pass gets a turn under continuing demand. Automatic contact sampling runs at most once per opportunity. A missed opportunity breaks crossing continuity, and stale tracking/geometry still makes observations unknown. Existing ripples may finish; missed events and accumulated time are never replayed. Reading cannot advance the clock, acquire sampling, start physics or emit contacts. Logical units are not measured device performance.",null,null,
            (context,args)=>Available(context)?ProgramValue.Literal(ContactWorkResources.ObserveSchedule()):null,features:Features);
        public static BehaviourCatalog.FactDefinition Reservation()=>new("runtime.contactWorkReservation",ProgramDataType.Read(JObject.Parse(@"{""record"":{""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""units"":""number"",""current"":""boolean""}}")),
            "Water contact work reservation","Read one runtime.contactWorkBudget request by index, including waiting passes. Indices may shift; reservationId is runtime-only. World and region identify the copied room owner. Current means a complete sampling allowance is booked and its demand and owner still match, not that geometry, tracking or any contact is known. Demand growth cannot evict another grant or admit a room prefix. Paused, inactive or destroyed sampling hosts release work; held and animated objects remain contact participants. Reads are detached and cannot resume sampling.",
            CapabilitySchema.Object(new JObject{["index"]=CapabilitySchema.Number(0,ContactWorkResources.MaximumReservations-1,true)}),new JObject{["index"]=0},
            (context,args)=>!Available(context)?null:ContactWorkResources.Observe((int)args["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
    }
}
