// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ObjectAttentionCapability:NativeResourceInputsCapability
    {
        internal const string Feature="objectAttention.v1";
        readonly AvatarAttentionCapability maestro=new();
        public override string Id=>"object.look";
        public override string Label=>"Turn a character's attention";
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"gaze","recipePart"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","attention.jointReady"};
        public override string Description=>"Aim Maestro, an imported humanoid head, or one named recipe part toward the user, an exact object's live collision-bounds centre, or an authored point. Choose head for humanoids; choose part for a recipe robot, eye, turret or other rotating joint. The construction/rest frame defines forward (+Z) and up (+Y); imported bone axes are calibrated from that frame. Yaw is bounded to 60 degrees and pitch to 30 relative to the moving parent. Only the chosen joint rotates; no root movement, collision movement, eye recognition or visibility inference is implied. One attention owner per actor can coexist with its root navigation and other recipe-part animations. A whole-target clip, another gaze action, or animation on the chosen recipe part conflicts; stop that part's autoplay before aiming it. Maestro uses its existing gaze implementation and movement conflicts. Capturing/authoring restores source poses; Stop restores the underlying joint and never changes the saved pose or restarts itself. Missing/replaced actors, joints or focus, lost tracking, editing, grabbing the actor, pause and room changes end attention. Reduced motion reports applied=false. The focus object is read, never owned or moved. Inspect object.attention for the sampled direction; completion means elapsed time, not perception or eye contact.";
        internal static string Part(JObject args)=>(string)args["joint"]?["kind"]=="part"?(string)args["joint"]["part"]:null;
        static JObject Joint(){
            var head=Object(new JObject{["kind"]=Choice("head")});head["title"]="Humanoid head";
            var part=Object(new JObject{["kind"]=Choice("part"),["part"]=RecipePartAnimationCapability.PartId()});part["title"]="Recipe part";
            return new JObject{["oneOf"]=new JArray(head,part),["x-discriminators"]=new JArray("kind"),["title"]="Joint"};
        }
        public override JObject InputSchema{get{
            var value=Object(new JObject{["target"]=AnimationTargets.TargetSchema(),["joint"]=Joint(),["focus"]=AvatarAttentionCapability.FocusSchema(),["seconds"]=Number(.1,30)});
            value["x-features"]=new JArray(Feature);return value;
        }}
        public override JObject OutputSchema=>AvatarAttentionCapability.SampleSchema();
        public override JObject Example=>new(){["target"]="maestro",["joint"]=new JObject{["kind"]="head"},["focus"]=new JObject{["kind"]="user"},["seconds"]=5};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>Part(args) is string part
            ?new[]{new BehaviourCatalog.Claim((string)args["target"],"gaze"),new BehaviourCatalog.Claim((string)args["target"],"recipePart:"+part)}
            :new[]{new BehaviourCatalog.Claim((string)args["target"],"gaze")};
        public override bool Validate(JObject args,out string error){
            error="A character cannot look at itself";
            if((string)args["focus"]["kind"]=="object"&&(string)args["focus"]["objectId"]==(string)args["target"])return false;
            error="The book has no character attention joint";if((string)args["target"]=="book")return false;
            error="Maestro uses its humanoid head";if((string)args["target"]=="maestro"&&Part(args)!=null)return false;
            error=null;return true;
        }
        static JObject MaestroArgs(JObject args)=>new(){["target"]="maestro",["focus"]=args["focus"].DeepClone(),["seconds"]=args["seconds"].DeepClone()};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            if((string)args["target"]=="maestro")return maestro.CanRun(context,MaestroArgs(args),out error);
            if(!context.Target(args,out var item,out error))return false;
            return ObjectJointAttention.Resolve(context.Editor,item,Part(args),out _,out error)&&
                new AvatarAttentionTarget(context.Editor,(JObject)args["focus"]).Read(context.Editor,context.Editor.Viewer,out _,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;
            if((string)args["target"]=="maestro")return maestro.Start(context,runId,MaestroArgs(args),out operation,out error);
            if(!ObjectJointAttention.Begin(context.Editor,context.Editor.Find((string)args["target"]),runId,Part(args),new AvatarAttentionTarget(context.Editor,(JObject)args["focus"]),out var view,out error))return false;
            operation=new AttentionOperation(view,runId,(float)args["seconds"],(string)args["focus"]["objectId"]);return true;
        }
        sealed class AttentionOperation:CapabilityOperation{
            readonly ObjectJointAttention view;readonly string owner,observed;readonly float seconds;JObject result;
            internal AttentionOperation(ObjectJointAttention view,string owner,float seconds,string observed){this.view=view;this.owner=owner;this.seconds=seconds;this.observed=observed;}
            public override float Seconds=>seconds;
            public override bool WaitsThroughGrab(string target)=>target==observed;
            public override RuleActionState State(out string error){error=view?view.Error:"Attention was removed";return view&&view.OwnedBy(owner)&&view.Ready(out error)?RuleActionState.Ready:RuleActionState.Failed;}
            public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;result=view.Observe();return true;}
            public override JObject Result=>result??(view?view.Observe():new JObject());
            public override void Stop(bool preservePlacement){if(view)view.End(owner);}
        }
        internal static BehaviourCatalog.FactDefinition Fact(){
            var type=OutputType(AvatarAttentionCapability.SampleSchema());
            return new("object.attention",type,"Character attention","Passive last sample of object.look or Maestro's existing gaze layer. Reading never loads or starts attention. active/applied describe native joint presentation, not eye contact or sight through walls. The focus point is in authored room metres. Stop leaves inactive neutral fields; the completion receipt retains its last sample.",
                Object(new JObject{["target"]=AnimationTargets.TargetSchema()}),new JObject{["target"]="maestro"},(c,a)=>{
                    var item=c.Editor?c.Editor.Find((string)a["target"]):null;
                    if(!item||!item.isActiveAndEnabled)return null;
                    if((string)a["target"]=="maestro"){
                        var spatial=item.GetComponent<AvatarSpatialMotion>();
                        return spatial&&spatial.isActiveAndEnabled?ProgramValue.Literal(spatial.ObserveAttention(),type):null;
                    }
                    var view=item.GetComponent<ObjectJointAttention>();
                    if(view)return ProgramValue.Literal(view.Observe(),type);
                    return ProgramValue.Literal(new JObject{["active"]=false,["focus"]="none",["objectId"]="",["point"]=new JObject{["x"]=0,["y"]=0,["z"]=0},["yaw"]=0,["pitch"]=0,["limited"]=false,["applied"]=false},type);
                },features:new[]{Feature});
        }
    }
}
