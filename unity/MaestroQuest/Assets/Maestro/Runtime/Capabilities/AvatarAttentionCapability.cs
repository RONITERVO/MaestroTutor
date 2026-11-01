// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class AvatarAttentionCapability:NativeResourceInputsCapability {
        internal const string Feature="avatarAttention.v1";
        public override string Id=>"avatar.look";
        public override string Label=>"Look toward something";
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"gaze"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","avatar.spatialReady"};
        public override string Description=>"Direct Maestro's existing head-gaze layer toward the tracked user, the live collision-bounds centre of one exact object, or an authored room point. This supports the included and selected compatible Maestro avatar, not arbitrary created character rigs. Object focus follows normal motion and may stay held by the user; it never claims or moves that object. Missing/replaced objects or unavailable bounds end the action. Points follow the authored world when its view moves. Head yaw is limited to 60 degrees and pitch to 30 degrees, with smooth approach; the body never turns or walks for this action. Reduced motion leaves the head unchanged and reports applied=false. The result and avatar.attention report the latest sampled focus and bounded angles, not eye contact, visual recognition, visibility through walls or guaranteed alignment. Gaze coexists with the existing upper-body gesture channel; walking and whole-body animation retain their existing conflicts. Stop, editing, grabbing Maestro, tracking/focus loss and room/model changes end attention. No saved pose, program, automatic restart or new provider request is created.";
        static JObject Point(bool bounded)=>Object(new JObject{["x"]=bounded?Number(-25,25):Number(-float.MaxValue,float.MaxValue),["y"]=bounded?Number(-25,25):Number(-float.MaxValue,float.MaxValue),["z"]=bounded?Number(-25,25):Number(-float.MaxValue,float.MaxValue)});
        internal static JObject FocusSchema(){
            var user=Object(new JObject{["kind"]=Choice("user")});user["title"]="The user";
            var entity=Object(new JObject{["kind"]=Choice("object"),["objectId"]=AnimationTargets.TargetSchema()});entity["title"]="An object";
            var point=Object(new JObject{["kind"]=Choice("point"),["point"]=Point(true)});point["title"]="A place in the world";
            return new JObject{["oneOf"]=new JArray(user,entity,point),["x-discriminators"]=new JArray("kind"),["title"]="Attention"};
        }
        public override JObject InputSchema {get{
            var schema=Object(new JObject{["target"]=AnimationTargets.AvatarSchema(),["focus"]=FocusSchema(),["seconds"]=Number(.1,30)});schema["x-features"]=new JArray(Feature);return schema;
        }}
        internal static JObject SampleSchema()=>Object(new JObject{["active"]=new JObject{["type"]="boolean"},["focus"]=Choice("none","user","object","point"),["objectId"]=Text(null,32),["point"]=Point(false),["yaw"]=Number(-60,60),["pitch"]=Number(-30,30),["limited"]=new JObject{["type"]="boolean"},["applied"]=new JObject{["type"]="boolean"}});
        public override JObject OutputSchema=>SampleSchema();
        public override JObject Example=>new(){["target"]="maestro",["focus"]=new JObject{["kind"]="object",["objectId"]="book"},["seconds"]=5};
        public override bool Validate(JObject args,out string error){
            error="Maestro cannot use itself as an attention object";
            if((string)args["focus"]["kind"]=="object"&&(string)args["focus"]["objectId"]==(string)args["target"])return false;error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            if(!context.Target(args,out var target,out error,allowUpperBody:true))return false;
            var spatial=target.GetComponent<AvatarSpatialMotion>();error="Maestro attention controls are unavailable";
            return spatial&&spatial.CanBegin(AvatarSpatialMode.Look,out error)&&new AvatarAttentionTarget(context.Editor,(JObject)args["focus"]).Read(context.Editor,context.Editor.Viewer,out _,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;
            var spatial=context.Editor.Find((string)args["target"]).GetComponent<AvatarSpatialMotion>();
            var value=new AttentionOperation(spatial,runId,(float)args["seconds"],(string)args["focus"]["objectId"]);operation=value;
            return spatial.BeginAttention(runId,new AvatarAttentionTarget(context.Editor,(JObject)args["focus"]),out error);
        }
        sealed class AttentionOperation:CapabilityOperation {
            readonly AvatarSpatialMotion spatial;readonly string owner,observed;readonly float seconds;bool stopped;JObject completed;
            internal AttentionOperation(AvatarSpatialMotion spatial,string owner,float seconds,string observed){this.spatial=spatial;this.owner=owner;this.seconds=seconds;this.observed=observed;}
            public override float Seconds=>seconds;
            public override bool WaitsThroughGrab(string target)=>target==observed;
            public override RuleActionState State(out string error){
                error=spatial?spatial.Status:"Maestro attention controls were removed";
                return !stopped&&spatial&&spatial.OwnedBy(owner)&&spatial.AttentionReady(out error)?RuleActionState.Ready:RuleActionState.Failed;
            }
            public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;completed=spatial.ObserveAttention();return true;}
            public override JObject Result=>completed??(spatial?spatial.ObserveAttention():new JObject());
            public override void Stop(bool preservePlacement){if(stopped)return;stopped=true;if(spatial)spatial.End(owner);}
        }
        internal static BehaviourCatalog.FactDefinition Fact(){
            var type=OutputType(SampleSchema());
            return new("avatar.attention",type,"Maestro attention","Passive latest head-gaze sample from the shared native layer, including legacy Look/Follow controls. point is in authored room metres. active means a gaze owner exists; applied requires a rendered gaze sample with reduced motion off. limited means the requested direction exceeds the head's yaw/pitch bounds. These angles are applied offsets, not eye-contact, occlusion or perceptual quality evidence. An inactive owner reports neutral fields. Reading never starts attention or loads a target.",Object(new JObject{["target"]=AnimationTargets.AvatarSchema()}),new JObject{["target"]="maestro"},(c,a)=>{
                var spatial=c.Editor?c.Editor.Find((string)a["target"])?.GetComponent<AvatarSpatialMotion>():null;
                return spatial&&spatial.isActiveAndEnabled?ProgramValue.Literal(spatial.ObserveAttention(),type):null;
            },features:new[]{Feature});
        }
    }
}
