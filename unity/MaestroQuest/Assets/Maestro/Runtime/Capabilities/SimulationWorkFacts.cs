// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs {
 public static class SimulationWorkFacts {
  public static BehaviourCatalog.FactDefinition Ledger()=>new("runtime.simulationWorkLedger",
   ProgramDataType.Read(new JObject{["record"]=new JObject{["step"]="number",["resource"]="text",["committed"]="number",["consumed"]="number",["retired"]="number",["limit"]="number"}}),
   "Simulation opportunity ledger",
   "Read one process-wide resource allowance for the current simulation opportunity. Compare step when combining separate resource reads. Committed capacity includes current grants and consumed charges retained after shrinking or removing a request. Consumed is the largest allowance already used by each request in this opportunity; retired is its subset whose requests were removed. Retired charges cannot be used again until the clock advances. Unused reservations can be released. Repeated use of one allowance does not add a new reservation and does not authorize another automatic quantity/contact tick. These are logical reservation costs, not measured operations, native residency, memory, frame time or a PhysX budget. Existing host/body schedule facts describe current requests and may be empty while this ledger still retains consumed capacity. Reads do not collect, admit, consume, load, publish or advance the clock.",
   CapabilitySchema.Object(new JObject{["resource"]=CapabilitySchema.Choice("discoveryUnits","physicsUnits","liquidUnits","contactUnits","physicsBodies","liquidPasses","contactPasses")}),new JObject{["resource"]="liquidUnits"},
   (context,args)=>context.Editor&&context.Editor.GetComponentInParent<RuntimeDiagnostics>()?ProgramValue.Literal(SimulationWorkSchedule.ObserveResource((string)args["resource"])):null,features:new[]{RuntimeDiagnosticFacts.Feature});
 }
}
