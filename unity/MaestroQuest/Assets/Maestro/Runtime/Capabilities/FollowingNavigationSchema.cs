// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class FollowingNavigationSchema {
        internal const string Feature="waterFollowing.v1";
        internal static bool Selected(JObject args)=>(string)args["mode"] is "swim" or "surface" or "shore";
        internal static JObject Input(JObject ground){
            ground["properties"]["mode"]=Choice("walk");ground["properties"]["mode"]["x-features"]=new JArray(Feature);ground["title"]="Follow on accepted ground";
            JObject Water(string mode,string title){
                var fields=(JObject)ground["properties"].DeepClone();
                fields["mode"]=Choice(mode);fields["habitat"]=Resource(Text("^[a-fA-F0-9]{32}$",32));
                fields["facing"]=Choice("travel","keep");fields["gait"]=AquaticNavigationSchema.Gait();
                if(mode!="swim")fields["immersion"]=Number(.1,.95);
                var schema=Object(fields,"gait");schema["title"]=title;schema["x-features"]=new JArray(Feature);return schema;
            }
            return new JObject{["title"]="Following movement",["oneOf"]=new JArray(ground,Water("swim","Follow underwater"),Water("surface","Follow at the water surface"),Water("shore","Follow across ground and water")),["x-discriminators"]=new JArray("mode")};
        }
        internal const string Description=" Omitted mode or mode=walk keeps grounded following. mode=swim, surface or shore uses the corresponding object.navigate body and movement engine, with one exact habitat and the same owned lifetime. Submerged following measures root-to-root distance in three dimensions (the tracked head for a user leader); surface and shore measure horizontal distance. Surface follows the live waterline. Shore samples accepted ground near the leader's height within the configured distance and supported slope, or the selected live surface; its continuous route must prove the connection. A leader may be outside the chosen habitat: an unreachable following goal waits at the last accepted place and retries at most once each 0.35 seconds, without changing habitats or entering water through an invalid body. Current body, physics, tracking and habitat readiness remain required even while waiting. Drainage, leader loss or replacement, editing and interruption end the action. Optional exact imported swimming gait retains its clock across retargeting; shore keeps the assigned walk on ground. mode=shore explicitly permits only its chosen water for this action, preserving saved water avoidance. Timed completion reports actual state, distance and position; it never claims that a blocked or moving leader was reached. object.navigation exposes mode=follow and movement=walk/swim/surface/shore.";
    }
}
