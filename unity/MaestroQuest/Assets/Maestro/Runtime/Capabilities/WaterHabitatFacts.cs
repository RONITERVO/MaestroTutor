// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class WaterHabitatFacts {
        internal const string Feature="waterHabitat.v1";
        static ProgramDataType Type()=>OutputType(Object(MediumFacts.AvailabilityFields(new JObject{
            ["bodyId"]=Text(null,32),["contained"]=new JObject{["type"]="boolean"},["reason"]=Text(null,128)
        })));
        internal static BehaviourCatalog.FactDefinition Path()=>new("object.water.habitat.path",Type(),"Inspect a water habitat path",
            "Check whether the target's entire conservative native collision envelope remains inside one exact liquid habitat while translating its root to destination in authored room coordinates. The current orientation is held; the envelope is world-axis-aligned and includes accepted root colliders, not animated visual limbs. Every corner must fit below the live free surface and within the cylindrical or rectangular cavity. The complete swept body also checks the actual terrain bed, so wet endpoints cannot skip a dry ridge or island. bodyId always names the requested habitat; overlapping water never substitutes for it. Empty liquid is a known contained=false result. This reads the same current pouring/rain quantities and terrain geometry used by rendering and water contact. contained proves water-volume containment only: it does not check other solid objects, real-room readiness, ownership, swimming gait or a connected route. It neither moves nor swims, starts physics, changes an actor's water policy, loads geometry nor reserves an answer for later movement. Recheck when the body, water or terrain changes. Invalid, missing or unready geometry is unavailable, never permission to cross it. "+MediumFacts.AvailabilityDescription+" The explicitly chosen habitat is also a required dependency even when its known bounds are distant.",
            Object(new JObject{["target"]=Resource(Text("^(maestro|[a-fA-F0-9]{32})$",32)),["habitat"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["destination"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)})}),
            new JObject{["target"]="maestro",["habitat"]=new string('0',32),["destination"]=new JObject{["x"]=1,["y"]=0,["z"]=1}},
            (c,a)=>c.Editor?.ObserveHabitatPath((string)a["target"],(string)a["habitat"],new Vector3((float)a["destination"]["x"],(float)a["destination"]["y"],(float)a["destination"]["z"])) is JObject value?ProgramValue.Literal(value,Type()):null,features:new[]{Feature});
    }
}
