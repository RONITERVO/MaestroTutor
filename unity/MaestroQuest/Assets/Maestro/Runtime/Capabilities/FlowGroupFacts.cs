// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs {
 public static class FlowGroupFacts {
  static bool Available(BehaviourCatalog.FactContext c)=>c.Editor&&c.Editor.GetComponentInParent<RuntimeDiagnostics>();
  static readonly string[] Features={RuntimeDiagnosticFacts.Feature};
  static JObject Index()=>new(){["index"]=CapabilitySchema.Number(0,FlowWorkResources.MaximumReservations*RoomContainer.MaximumPerRoom-1,true)};
  public static BehaviourCatalog.FactDefinition Schedule()=>new("runtime.flowGroupSchedule",
   ProgramDataType.Read(JObject.Parse(@"{""record"":{""step"":""number"",""groups"":""number"",""active"":""number"",""booked"":""number"",""ready"":""number"",""executionLimit"":""number"",""groupLimit"":""number"",""units"":""number"",""unitLimit"":""number""}}")),
   "Liquid group opportunities","Complete quantity groups, separately from the host counts in runtime.flowWorkSchedule. Groups include inactive dormant cavities; active counts requested groups. Booked means all member allowances are reserved, possibly stale; ready additionally requires current discovery, placement, native input readiness and complete physics dependencies. This permits joint preparation; physical motion and quantity transfer separately require active joints. At most 32 group allowances coexist, under the unchanged process liquid-unit limit. A conservative undiscovered placeholder never executes. Indices may shift at regrouping; inspect step, reservationId, membershipRevision and firstMember together. Reading cannot advance service, load geometry or execute quantities. These are logical allowances, not measured CPU or headset capacity.",null,null,
   (c,a)=>Available(c)?ProgramValue.Literal(FlowWorkResources.ObserveGroups()):null,features:Features);
  public static BehaviourCatalog.FactDefinition Group()=>new("runtime.flowGroup",
   ProgramDataType.Read(JObject.Parse(@"{""record"":{""step"":""number"",""reservationId"":""text"",""worldId"":""text"",""regionId"":""text"",""membershipRevision"":""number"",""firstMember"":""text"",""members"":""number"",""units"":""number"",""active"":""boolean"",""discovered"":""boolean"",""booked"":""boolean"",""ready"":""boolean"",""lastServed"":""number""}}")),
   "Liquid quantity group","Read one complete quantity group by runtime.flowGroupSchedule index. membershipRevision changes when this host's group definitions change, including within one step; compare it on member reads and restart the read if it differs. Readiness is a momentary observation, not an atomic snapshot across queries. firstMember is a canonical object ID, not a stable group ID across merges/splits. members includes empty and missing receiving/donor cavities; shared read-only geometry does not couple independent groups. discovered=false is a conservative queue placeholder, never permission to execute. Service age belongs to canonical members and survives regrouping and native retirement; explicit canonical removal or world replacement creates new service identity. All groups share the host's atomic publication and Undo. Automatic quantity work cannot repeat a canonical member in one opportunity, even if it changes groups or its work request is recreated. No missed-time catch-up.",CapabilitySchema.Object(Index()),new JObject{["index"]=0},
   (c,a)=>Available(c)&&FlowWorkResources.ObserveGroup((int)a["index"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
  public static BehaviourCatalog.FactDefinition Member(){
   var schema=Index();schema["memberIndex"]=CapabilitySchema.Number(0,RoomContainer.MaximumPerRoom-1,true);
   return new("runtime.flowGroupMember",
    ProgramDataType.Read(JObject.Parse(@"{""record"":{""step"":""number"",""reservationId"":""text"",""membershipRevision"":""number"",""firstMember"":""text"",""target"":""text"",""units"":""number"",""lastServed"":""number"",""order"":""number""}}")),
    "Liquid group member","Read a single member of runtime.flowGroup without exceeding the program record budget. Check step, reservationId, membershipRevision and firstMember against the group read because indices may change. units is this member's share of complete-group demand; it cannot be admitted independently of its group. order is the canonical service registration order, not array order. Passive reads do not serve the group.",CapabilitySchema.Object(schema),new JObject{["index"]=0,["memberIndex"]=0},
    (c,a)=>Available(c)&&FlowWorkResources.ObserveGroupMember((int)a["index"],(int)a["memberIndex"]) is JObject value?ProgramValue.Literal(value):null,features:Features);
  }
 }
}
