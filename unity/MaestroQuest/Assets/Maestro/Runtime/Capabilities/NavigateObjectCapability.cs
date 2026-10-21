// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class NavigateObjectCapability:NativeResourceInputsCapability {
        internal const string Feature="groundedTravel.v1";
        public override string Id=>"object.navigate";
        public override string Label=>"Move to a place";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>330;
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"locomotion"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","physics.running","navigation.environmentReady"};
        public override string Description=>"Starting near dormant virtual ground prepares the actor’s local native inputs before displacement; admission itself performs no loading. Physics must already be started, and virtual-only actors never borrow the scanned physical floor. Walk Maestro or a created object to a supported foot position in authored room coordinates. mode=walk follows the same accepted floor, slope/step, water and per-object real-room policies as Maestro following; no room scan or physics starts implicitly. The destination is fixed in the authored world, so moving the viewed world does not move the requested place. Speed is metres per second. Four concurrent walking, following and swimming actions share this budget. An upright body proxy is derived from root colliders (Maestro uses its existing body proxy); missing/oversized bodies, physical anchors, terrain owners and connected assemblies refuse. Sweeps check the body proxy, not each visual limb. Root placement changes gradually; created characters keep their own part animations, while Maestro uses its selected walk. Whole-root animation conflicts. Arrival requires accepted movement to the sampled destination, never just elapsed time. The shared bounded route search tries detours around solid objects and finite water; actual steps still check current collisions. Occupied destinations or exhausted searches normally fail at the last accepted place. If a blocking object still has an active walking owner, wait up to three real-time seconds with retries no faster than every 0.35 seconds; object.navigation reports waitingForActor. A new allowance requires at least 0.1 metre of accepted travel. Waiting never claims arrival, moves the other actor, or overrides either actor’s ownership or collision policy. A persistent obstruction fails after the wait. Conservative obstacle footprints do not guarantee a route through every interior or crowd. Preparation holds the last accepted pose and never replays elapsed movement. Stop, grab, authoring, edits, lost physics or workspace replacement stop at the last accepted position. Runtime placement is remembered without an Undo per step; movement never restarts from saved data. A route has a five-minute lifetime and a bounded native-input wait. The result reports actual root position and travelled world metres. It does not promise foot planting or collision checks for animated limbs."+AquaticNavigationSchema.Description;
        static JObject Point()=>Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        public override JObject InputSchema {get {var schema=Object(new JObject{["target"]=AnimationTargets.TargetSchema(),["mode"]=Choice("walk"),["destination"]=Point(),["speed"]=Number(.2,1.2)});schema["x-features"]=new JArray(Feature);schema["title"]="Walk on accepted ground";return new JObject{["oneOf"]=new JArray(schema,AquaticNavigationSchema.Input()),["x-discriminators"]=new JArray("mode"),["title"]="Movement"};}}
        public override JObject OutputSchema=>Object(new JObject{["position"]=Point(),["distance"]=Number(0,600)});
        public override JObject Example=>new(){["target"]="maestro",["mode"]="walk",["destination"]=new JObject{["x"]=1,["y"]=0,["z"]=1},["speed"]=.65};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>AquaticNavigationSchema.Swim(args)&&ImportedLocomotionPose.Selected(args["gait"] as JObject)?new[]{new BehaviourCatalog.Claim((string)args["target"],"wholeTarget")}:(string)args["target"]=="maestro"?new[]{new BehaviourCatalog.Claim("maestro","locomotion"),new BehaviourCatalog.Claim("maestro","gaze")}:base.Claims(args);
        internal static Vector3 Destination(JObject args)=>ObjectCapabilityData.Position((JObject)args["destination"]);
        public override bool Validate(JObject args,out string error){error="The movement destination must be within 25 metres of the authored origin";if(Destination(args).sqrMagnitude>625)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out var item,out error,allowUpperBody:!AquaticNavigationSchema.Swim(args)||!ImportedLocomotionPose.Selected(args["gait"] as JObject))&&(AquaticNavigationSchema.Swim(args)?AquaticMovement.CanBegin(context.Editor,item,args,out error):GroundedTravel.CanBegin(context.Editor,item,out error));
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;
            if(AquaticNavigationSchema.Swim(args)){var swim=new AquaticMovement(context.Editor,context.Editor.Find((string)args["target"]),args);operation=swim;return swim.Begin(args,out error);}
            var travel=new GroundedTravel(context.Editor,context.Editor.Find((string)args["target"]),Destination(args),(float)args["speed"]);operation=travel;
            return travel.Begin(out error);
        }
    }
}
