// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class FollowObjectCapability:NativeResourceInputsCapability {
        internal const string Feature="groundedFollowing.v1";
        public override string Id=>"object.follow";
        public override string Label=>"Follow someone";
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"locomotion"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","physics.running","navigation.environmentReady"};
        public override string Description=>"Starting near dormant virtual ground prepares the actor’s local native inputs before displacement; admission itself performs no loading. Physics must already be started, and virtual-only actors never borrow the scanned physical floor. Follow the tracked user or one exact native object for the requested seconds using the same walking engine as object.navigate. target is the follower; leader chooses the user or an object. Only the follower's movement is owned. A held or independently moving leader can be observed; missing/replaced leaders end the action. Distance is horizontal foot-to-foot metres: following approaches when farther away and holds position inside that distance, without pushing either party apart. The user's horizontal position is projected at the follower's current ground height; an object leader's foot comes from its live root collision bounds. Only connected accepted ground is used; a hovering leader or a different floor may be unreachable. Physics, user tracking, native geometry and the follower's saved real-room/water rules are required. Solid/water detours and every actual step use shared clearance checks. A blocked path holds the last accepted pose and retries at a bounded cadence; elapsed following time includes that wait, never a later jump. Completion means the requested time ended, not arrival. Inspect object.navigation and the result state/reason. Stop, grabbing the follower, editing, tracking loss, workspace changes or lost physics end movement without restarting. Other programs can animate recipe parts while the root follows; imported humanoids use an explicitly assigned exact walking motion when configured, and a separate whole-target imported animation conflicts. Unconfigured models do not receive an invented gait. Maestro uses its selected gait. Ground mode does not enter water; no mode invents foot planting, crowd steering or head/limb posing. Four concurrent walking, following and swimming actions share one budget. Use ordinary program loops/events for a longer behaviour; no activity resumes from saved data."+FollowingNavigationSchema.Description;
        static JObject Point()=>Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        internal static string Leader(JObject args)=>(string)args["leader"]?["kind"]=="object"?(string)args["leader"]["objectId"]:null;
        public override JObject InputSchema {get {
            var person=Object(new JObject{["kind"]=Choice("user")});person["title"]="The user";
            var entity=Object(new JObject{["kind"]=Choice("object"),["objectId"]=AnimationTargets.TargetSchema()});entity["title"]="Another object";
            var leader=new JObject{["oneOf"]=new JArray(person,entity),["x-discriminators"]=new JArray("kind"),["title"]="Leader"};
            var schema=Object(new JObject{["target"]=AnimationTargets.TargetSchema(),["leader"]=leader,["distance"]=Number(.8,5),["speed"]=Number(.2,1.2),["seconds"]=Number(.1,30)});schema["x-features"]=new JArray(Feature);return FollowingNavigationSchema.Input(schema);
        }}
        public override JObject OutputSchema=>Object(new JObject{["position"]=Point(),["distance"]=Number(0,600),["state"]=Text(null,32),["reason"]=Text(null,256)});
        public override JObject Example=>new(){["target"]="maestro",["leader"]=new JObject{["kind"]="user"},["distance"]=1.3,["speed"]=.65,["seconds"]=10};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>FollowingNavigationSchema.Selected(args)&&ImportedLocomotionPose.Selected(args["gait"] as JObject)?new[]{new BehaviourCatalog.Claim((string)args["target"],"wholeTarget")}:(string)args["target"]=="maestro"?new[]{new BehaviourCatalog.Claim("maestro","locomotion"),new BehaviourCatalog.Claim("maestro","gaze")}:base.Claims(args);
        public override bool Validate(JObject args,out string error){error="An object cannot follow itself";if(Leader(args)==(string)args["target"])return false;error=null;return true;}
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out var item,out error,allowUpperBody:!FollowingNavigationSchema.Selected(args)||!ImportedLocomotionPose.Selected(args["gait"] as JObject))&&(FollowingNavigationSchema.Selected(args)?WaterFollowing.CanBegin(context.Editor,item,args,out error):GroundedFollowing.CanBegin(context.Editor,item,Leader(args),(float)args["distance"],out error));
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;
            if(FollowingNavigationSchema.Selected(args)){var water=new WaterFollowing(context.Editor,context.Editor.Find((string)args["target"]),args);operation=water;return water.Begin(out error);}
            var value=new GroundedFollowing(context.Editor,context.Editor.Find((string)args["target"]),Leader(args),(float)args["distance"],(float)args["speed"],(float)args["seconds"]);operation=value;return value.Begin(out error);
        }
        internal static BehaviourCatalog.FactDefinition Fact(){
            var type=OutputType(Object(new JObject{["active"]=new JObject{["type"]="boolean"},["mode"]=Choice("none","walk","follow","swim","surface","shore"),["movement"]=Choice("none","walk","swim","surface","shore"),["state"]=Text(null,32),["reason"]=Text(null,256),["position"]=Point(),["distance"]=Number(0,600)}));
            return new("object.navigation",type,"Movement action status","Passive state of the current shared object.navigate or object.follow owner, including the movement medium, preparingWorld, preparingAnimation, surfaceAdjusting, turning, planning, waitingForActor, moving, keepingDistance, arrived, blocked or failed. position is the native root in authored room metres; distance is accepted travel in this operation. Inactive means no shared movement owner (not proof that every other kind of motion has stopped). Legacy Maestro controls and animation use their existing facts. Missing native objects are unavailable. Reading does not load, move, start physics or retry an action.",Object(new JObject{["target"]=AnimationTargets.TargetSchema()}),new JObject{["target"]="maestro"},(c,a)=>{var item=c.Editor?c.Editor.Find((string)a["target"]):null;return item&&item.isActiveAndEnabled?ProgramValue.Literal(ShoreMovement.Observe(c.Editor,item)??AquaticMovement.Observe(c.Editor,item)??GroundedMovement.Observe(c.Editor,item),type):null;},features:new[]{Feature,FollowingNavigationSchema.Feature});
        }
    }
}
