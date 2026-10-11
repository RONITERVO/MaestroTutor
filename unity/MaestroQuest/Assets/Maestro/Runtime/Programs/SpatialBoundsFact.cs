// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    public static class SpatialBoundsFact
    {
        public static BehaviourCatalog.FactDefinition Definition()
        {
            JObject Triple()=>Object(new JObject{["x"]=Number(-float.MaxValue,float.MaxValue),["y"]=Number(-float.MaxValue,float.MaxValue),["z"]=Number(-float.MaxValue,float.MaxValue)});
            JObject Extent()=>Object(new JObject{["known"]=new JObject{["type"]="boolean"},["hasBounds"]=new JObject{["type"]="boolean"},["min"]=Triple(),["max"]=Triple()});
            var output=Object(new JObject{["target"]=Text(null,32),["revision"]=Number(0,int.MaxValue,true),["source"]=Text(null,16),["coordinates"]=Text(null,8),["visual"]=Extent(),["collision"]=Extent()});
            return new("object.spatialBounds",OutputType(output),"Object spatial bounds",
                "Read conservative axis-aligned visual and accepted non-trigger collision envelopes in authored room coordinates without loading, saving, editing or acquiring ownership. source is native (sampled instance), retained (value-only geometry captured before intentional retirement), unknown (saved but no valid envelope), or missing. Each extent has known and hasBounds: only known=true with hasBounds=false proves empty; unknown zero min/max is never empty space. Geometry edits invalidate retained envelopes; root placement reprojects them using the same stopped-pose rule as native activation. Disabled child geometry may be included. Skinned/unsupported renderers, pending imports and scan-anchored geometry are unknown. Bounds are broad-phase candidates, not exact contact, visibility, walkability, clearance, collision policy, future motion or physics readiness. They do not prove an unloaded model file remains available. No automatic regional streaming is enabled by this read.",
                Object(new JObject{["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32))}),new JObject{["target"]="book"},
                (c,a)=>c.Editor&&c.Editor.ObserveSpatialBounds((string)a["target"]) is JObject value?ProgramValue.Literal(value,OutputType(output)):null);
        }
    }
}
