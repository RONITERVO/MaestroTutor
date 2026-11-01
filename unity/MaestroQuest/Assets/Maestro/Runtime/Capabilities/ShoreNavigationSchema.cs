// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class ShoreNavigationSchema {
        internal const string Feature="shoreTravel.v1";
        internal static bool Selected(JObject a)=>(string)a["mode"]=="shore";
        internal static bool Surface(JObject a)=>(string)a["destination"]?["kind"]=="surface";
        internal static JObject Input(){
            var ground=Object(new JObject{["kind"]=Choice("ground"),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});ground["title"]="Supported ground foot";
            var water=Object(new JObject{["kind"]=Choice("surface"),["x"]=Number(-25,25),["z"]=Number(-25,25)});water["title"]="Live water surface";
            var value=Object(new JObject{["target"]=AnimationTargets.TargetSchema(),["mode"]=Choice("shore"),["habitat"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),
                ["destination"]=new JObject{["title"]="Shore destination",["oneOf"]=new JArray(ground,water),["x-discriminators"]=new JArray("kind")},
                ["immersion"]=Number(.1,.95),["speed"]=Number(.2,1.2),["facing"]=Choice("travel","keep"),["gait"]=AquaticNavigationSchema.Gait()},"gait");
            value["title"]="Walk and swim across a shore";value["x-features"]=new JArray(Feature);return value;
        }
        internal const string Description=" For mode=shore, one upright capsule follows accepted ground into or out of one exact upright liquid cavity. Choose a typed destination: kind=ground uses a supported foot x/y/z; kind=surface uses x/z and the live immersion-derived foot height. This is a continuous supported/wading/buoyant journey, not teleportation, jumping off a cliff, flying across a dry gap or swapping habitats. It retains one movement owner, collision policy and capacity slot. Immersion is the fraction of the capsule's actual collision height below water. The native ground capsule, with its existing foot skin, is used throughout. Saved water avoidance remains unchanged: this action explicitly permits its selected liquid, while other water policies still apply. Supported shores retain existing slope/step limits; tilted or nonuniform liquid frames refuse. Solid sweeps include the body above water. A changed waterline adjusts gradually before horizontal motion. The bounded route search preserves support height and current native geometry; exhausted or blocked routes fail without moving to a guessed position. Ground gait uses the assigned walk; optional gait selects the exact imported swimming pose. Stop, edits, lost physics/tracking and workspace interruption are terminal. The same actual root position/distance result and object.navigation fact apply.";
    }
}
