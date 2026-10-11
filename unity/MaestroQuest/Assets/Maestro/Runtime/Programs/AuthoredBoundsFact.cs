// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class AuthoredBoundsFact
    {
        internal static BehaviourCatalog.FactDefinition Definition()
        {
            JObject Triple()=>Object(new JObject{["x"]=Number(-float.MaxValue,float.MaxValue),["y"]=Number(-float.MaxValue,float.MaxValue),["z"]=Number(-float.MaxValue,float.MaxValue)});
            var output=Object(new JObject{["target"]=Text(null,32),["revision"]=Number(0,int.MaxValue,true),["prepared"]=new JObject{["type"]="boolean"},["coordinates"]=Text(null,8),["visual"]=Object(new JObject{["known"]=new JObject{["type"]="boolean"},["hasBounds"]=new JObject{["type"]="boolean"},["min"]=Triple(),["max"]=Triple()})});
            var type=OutputType(output);
            return new("object.authoredBounds",type,"Authored object bounds",
                "Read the room-owned derived visual envelope in object-local metres before root placement/scale. Preparation starts from accepted world data, independently of native presence, with bounded work and owned model-byte inspection. This read never starts I/O, loads objects, acquires ownership or edits/saves the room. prepared=false means this exact geometry is pending. prepared=true with visual.known=false means unsupported or unavailable geometry, never empty space. Includes primitives, recipes, selection outlines, spatial/curved surface ink, height surfaces, container fill and carried material. Recipe rotation tracks use conservative swept envelopes. Scanned drawing layers include their local canvas/selection and ink geometry without requiring the physical scan; room projection separately requires the exact saved room/anchor and a fitting plane. Built-in avatars/books and unsupported/animated/skinned imports remain unknown. Color/opacity and visibility do not shrink geometry. This is not current native pose, contact, collision/navigation readiness, asset availability, future root motion or an automatic streaming claim. Same derivation supplies conservative dormant observation after geometry edits; known retained native bounds take precedence.",
                Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},
                (c,a)=>c.Editor&&c.Editor.ObserveAuthoredBounds((string)a["target"]) is JObject value?ProgramValue.Literal(value,type):null);
        }
    }
}
