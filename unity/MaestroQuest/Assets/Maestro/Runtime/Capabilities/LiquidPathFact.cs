// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class LiquidPathFact {
        internal const string Feature="liquidPaths.v1";
        static ProgramDataType Type()=>OutputType(Object(MediumFacts.AvailabilityFields(new JObject{
            ["pathMetres"]=Number(0,10000),["wetMetres"]=Number(0,10000),["dryMetres"]=Number(0,10000),
            ["targetMetres"]=Number(0,10000),["bodyCount"]=Number(0,16,true),["reason"]=Text("^.{0,256}$",256)})));
        internal static BehaviourCatalog.FactDefinition Definition()=>new("world.medium.path",Type(),
            "Liquid along a line",
            "Measure a straight finite line between from/to within the authored 25-metre sphere using current native liquid quantities and the same cylinder, rectangular cavity and triangulated terrain as water contact/rendering. Distances are physical metres. Dry gaps remain dry; overlapping water is counted once, assigned to the smallest cavity then stable object ID. target optionally reports that saved cavity's assigned metres; empty, absent or nonintersecting target returns zero. bodyCount counts cavities assigned a positive fraction, including at a zero-length wet point whose distances are all zero. availability must be ready before using any distances; unavailable/pending returns zero placeholders, never a clear path. Complete native cavity geometry is required along the whole segment, even beyond a first wet hit. Physics may be paused; visibility, view tint and real-room collision policy do not alter liquid geometry. Reading changes no audio, physics, quantities, ownership or loading. This is straight geometry, not acoustic transmission/refraction, visibility, floor support, swimming or route clearance. A relevant dormant cavity is pending only while native loading is queued/running, otherwise unavailable. Unknown cavity bounds are relevant; known distant or accepted empty cavities are excluded. dependencyCount counts missing relevant cavities; dependencies lists at most four stable IDs. Reasons explain the current failure; branch on availability.",
            Object(new JObject{["from"]=Position(),["to"]=Position(),["target"]=Text("^([a-zA-Z0-9_-]{1,64})?$",64)}),
            new JObject{["from"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["to"]=new JObject{["x"]=1,["y"]=1,["z"]=0},["target"]=""},
            (c,a)=>c.Editor?.Liquids?.ObserveLiquidPath(Point(a["from"]),Point(a["to"]),(string)a["target"]) is JObject value?ProgramValue.Literal(value,Type()):null,features:new[]{Feature});
        static JObject Position()=>Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        static Vector3 Point(JToken p)=>new((float)p["x"],(float)p["y"],(float)p["z"]);
    }
}
