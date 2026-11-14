// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class AquaticNavigationSchema {
        internal const string Feature="aquaticTravel.v1";
        internal const string SurfaceFeature="surfaceTravel.v1";
        internal static bool Swim(JObject args)=>(string)args["mode"] is "swim" or "surface";
        internal static bool Surface(JObject args)=>(string)args["mode"]=="surface";
        internal static JObject Gait(){
            var none=Object(new JObject{["source"]=Choice("none")});none["title"]="Keep independent part animation";
            JObject Common(string source)=>new(){["source"]=Choice(source),["modelHash"]=Text("^[a-fA-F0-9]{64}$",64),["anchorNode"]=Number(0,511,true),["rate"]=Number(.1,3)};
            var embedded=Common("embedded");embedded["clip"]=Number(0,63,true);var clip=Object(embedded);clip["title"]="Embedded animation";
            var library=Common("library");library["motionId"]=Text("^[a-fA-F0-9]{32}$",32);var motion=Object(library);motion["title"]="Exact library motion";
            var result=new JObject{["oneOf"]=new JArray(none,clip,motion),["x-discriminators"]=new JArray("source")};
            result["description"]="Optional full imported gait. Exact modelHash plus anchorNode from object.animation.nodes fixes the translation anchor while sampling every source node and blend shape. clip is zero-based; library keeps the exact motionId and rig. rate scales animation time, not travel speed. Omitted/none preserves independent recipe-part animation and does not select a walk clip.";
            return result;
        }
        internal static JObject Input(){
            var value=Object(new JObject{["target"]=Resource(Text("^(maestro|[a-fA-F0-9]{32})$",32)),["mode"]=Choice("swim"),
                ["habitat"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["destination"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),
                ["speed"]=Number(.2,1.2),["facing"]=Choice("travel","keep"),["gait"]=Gait()},"gait");
            value["x-features"]=new JArray(Feature);value["title"]="Swim inside one habitat";return value;
        }
        internal static JObject SurfaceInput(){
            var value=Object(new JObject{["target"]=Resource(Text("^(maestro|[a-fA-F0-9]{32})$",32)),["mode"]=Choice("surface"),
                ["habitat"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["destination"]=Object(new JObject{["x"]=Number(-25,25),["z"]=Number(-25,25)}),
                ["immersion"]=Number(.1,.95),["speed"]=Number(.2,1.2),["facing"]=Choice("travel","keep"),["gait"]=Gait()},"gait");
            value["x-features"]=new JArray(Feature,SurfaceFeature);value["title"]="Swim at the live surface";return value;
        }
        internal const string SurfaceDescription=" For mode=surface, choose destination x/z in authored room metres and immersion (0.1–0.95 of the accepted collision envelope height below water). The current measured liquid chooses root height, not an authored y. The actor must already intersect that exact liquid; a submerged actor rises gradually before planar travel. This never enters from dry ground, leaves onto a shore, crosses a dry gap or switches habitats. Current vertical gravity is required. The immersed convex swept body must fit its chosen cavity; the full body still clears terrain and solids. The bounded route searches in the surface plane instead of diving around obstacles. A changed water level is followed at the chosen speed while the actor remains wet; a drained or receded-away surface stops travel. Yaw preserves existing tilt. Arrival is checked against the current surface height, and the result remains the actual root position.";
        internal const string Description=" For mode=swim, destination is the object's root in authored room coordinates, not its walking foot. Choose one exact habitat; the complete accepted root-collision envelope must already fit below its current liquid surface. This is submerged travel, not automatic entry, surfacing, walking on water or teleporting to a nearby pool. A bounded three-dimensional search attempts a route within two metres of the direct corridor. Every actual translation and shortest-arc turn rechecks current live liquid, terrain and solid geometry, with the same real-room participation as this actor. Missing native inputs prepare under an owned deadline. Accepted world or quantity changes refresh prepared inputs and replan from the current body; every next step must still fit. Drained or unavailable habitat, saturated queries and blocked routes cannot report arrival. facing=travel turns before each segment; keep preserves orientation. Optional gait samples an exact imported source and translation anchor; no gait leaves recipe parts independently animatable. A selected full imported gait owns the complete target; no gait owns locomotion (and Maestro gaze). Ground and water actions share four slots. A route lasts at most five minutes and never resumes from saved data. Query envelopes are conservative boxes, not animated skin/limb collisions.";
    }
}
