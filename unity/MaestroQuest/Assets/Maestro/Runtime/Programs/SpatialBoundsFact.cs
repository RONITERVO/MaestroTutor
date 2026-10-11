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
                "Read conservative axis-aligned visual and accepted non-trigger collision envelopes in authored room coordinates without loading, saving, editing or acquiring ownership. source is native (sampled instance), retained (value-only geometry captured before intentional retirement), authored (prepared canonical visual geometry, collision unknown), unknown (saved but no prepared envelope), or missing. Each extent has known and hasBounds: only known=true with hasBounds=false proves empty; unknown zero min/max is never empty space. Geometry edits invalidate retained envelopes; bounded room-owned preparation can replace them with authored visual envelopes including selection, surface ink, terrain, container fill and carried material. Recipe rotation tracks use conservative swept bounds. Unsupported imports remain unknown. Scanned ink uses the current exact saved room/anchor and plane fit for every projection, including retired geometry; missing, different, untracked or unsupported scan frames stay unknown rather than reusing an old pose. Preparation can read model bytes but this fact never starts preparation or I/O; root placement reprojects them using the same stopped-pose rule as native activation. Disabled child geometry may be included. Loaded supported skins include envelopes derived from current joints, single-frame morph deltas and the reset pose; reads do not bake meshes or change rendering. Unsupported renderers and pending imports are unknown. These native skin values are not cold-start envelopes for all future animation. Bounds are broad-phase candidates, not exact contact, visibility, walkability, clearance, collision policy, future motion or physics readiness. They do not prove an unloaded model file remains available. No automatic regional streaming is enabled by this read.",
                Object(new JObject{["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32))}),new JObject{["target"]="book"},
                (c,a)=>c.Editor&&c.Editor.ObserveSpatialBounds((string)a["target"]) is JObject value?ProgramValue.Literal(value,OutputType(output)):null);
        }
    }
}
