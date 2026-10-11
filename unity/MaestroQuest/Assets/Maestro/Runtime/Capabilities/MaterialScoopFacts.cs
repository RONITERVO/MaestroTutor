// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class MaterialScoopFacts {
        internal static BehaviourCatalog.FactDefinition Live()=>new("object.material.capture",OutputType(Object(new JObject{
            ["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("idle","contact","unsaved"),["tool"]=Text("^(|[a-f0-9]{32})$",32),["field"]=Text("^(|[a-f0-9]{32})$",32),["direction"]=Choice("none","take","deposit"),
            ["balance"]=Object(new JObject{["requestedLitres"]=Number(0,20),["savedLitres"]=Number(0,8000),["previewLitres"]=Number(0,8000),["removedLitres"]=Number(0,20.001),["addedLitres"]=Number(0,20.001),["roundingLitres"]=Number(-.0001,.0001)}),["temporary"]=new JObject{["type"]="boolean"},["error"]=Text("^.{0,128}$",128)})),
            "Physical material draft","Current contact or retained measured scoop/deposit, exact session and both saved/preview carrier amounts. Field geometry and carried heap preview one bounded transfer. No volume is published until contact ends and one atomic save succeeds. Use object.field.resolve with this sessionId to retry/discard an interrupted draft; read object.field.capture.path for its one local centre. Reads do not begin, retry or repeat a gesture. Local litres do not imply world-space volume or rigid mass.",null,null,
            (c,a)=>c.Editor&&c.Editor.GetComponent<SpatialSculpting>() is SpatialSculpting capture?ProgramValue.Literal(capture.ObserveMaterial()):null,features:new[]{SculptTipCapability.MaterialFeature});
    }
}
