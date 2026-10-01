// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class HoldObjectCapability:CapabilityModule
    {
        public override string Id=>"object.hold";
        public override string Label=>"Hold and release object";
        public override string Duration=>"timed";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.creationReady","target.unheld","holder.anchorReady","authoring.inactive","release.physicsReady"};
        public override string Description=>"Temporarily carry a loaded creation at an exact recipe part, Maestro hand or object-root anchor. Owns only the prop, so holder animation can run in parallel. Both object IDs must be authorized resources. holder.revision guards root/recipe admission; avatarHand requires the current avatarHash. Read object.anchor for the anchor pose and use an explicit offset (at most one root-scale metre) and unit rotation relative to it. Gripping either object, missing/replaced anchors, room pause and path obstruction stop the action. Self-attachment and nested held-object chains are refused. seconds is the bounded lifetime; return restores the initial prop pose on completion/cancellation, while drop/throw releases at seconds*releaseAt and leaves real physics running. Throw derives velocity from actual recent anchor motion, using the existing 15 m/s and 30 rad/s caps; a stationary anchor is not an aimed throw. Drop/throw needs solid/bouncy physics and aligned running room surfaces, with clearance from the holder on release. Stop or failure never intentionally releases an unreleased prop; grip takeover preserves its current placement. Released motion cannot be undone by cancellation. This is live motion, not a saved constraint or animation edit; no auto-restart. Use object.attachment and the native receipt for the actual outcome.";
        internal static JObject AnchorSchema()
        {
            JObject Variant(string kind,string label,JObject fields){fields["kind"]=Choice(kind);fields["kind"]["x-static"]=true;var s=Object(fields);s["title"]=label;return s;}
            return new JObject{["type"]="object",["title"]="Attachment point",["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(
                Variant("recipePart","Recipe part",new JObject{["objectId"]=RecipeEditCapability.Target(),["part"]=RecipePartAnimationCapability.PartId(),["revision"]=Number(1,1000000,true)}),
                Variant("avatarHand","Maestro hand",new JObject{["objectId"]=AnimationTargets.AvatarSchema(),["hand"]=Choice("left","right"),["avatarHash"]=Text("^(|[a-f0-9]{64})$",64)}),
                Variant("object","Object root",new JObject{["objectId"]=AnimationTargets.TargetSchema(),["revision"]=Number(1,1000000,true)}))};
        }
        public override JObject InputSchema{get{var s=Object(new JObject{["target"]=RecipeEditCapability.Target(),["holder"]=AnchorSchema(),["offset"]=Vector(),["rotation"]=Vector(true),["seconds"]=Number(.1,30),["release"]=Choice("return","drop","throw"),["releaseAt"]=Number(.05,1)});s["x-features"]=new JArray("objectAttachments.v1","actionResults.v1");return s;}}
        public override JObject OutputSchema=>Object(new JObject{["target"]=Text("^[a-fA-F0-9]{32}$",32),["holder"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["phase"]=Choice("returned","dropped","thrown"),["released"]=new JObject{["type"]="boolean"}});
        public override JObject Example=>new(){["target"]=new string('0',32),["holder"]=new JObject{["kind"]="recipePart",["objectId"]=new string('1',32),["part"]="RightHand",["revision"]=1},["offset"]=new JObject{["x"]=0,["y"]=0,["z"]=.3},["rotation"]=new JObject{["x"]=0,["y"]=0,["z"]=0,["w"]=1},["seconds"]=2,["release"]="return",["releaseAt"]=1};
        internal static RoomPropAnchor Anchor(JObject value)=>new((string)value["kind"],(string)value["objectId"],(string)value["part"]??(string)value["hand"]??"",(int?)value["revision"]??0,(string)value["avatarHash"]??"");
        static PropAttachment Attachment(JObject args)=>new((string)args["target"],"",PropHand.Right,Enum.Parse<PropRelease>((string)args["release"],true),JsonUtility.FromJson<Vector3>(args["offset"].ToString()),JsonUtility.FromJson<Quaternion>(args["rotation"].ToString()),(float)args["releaseAt"]);
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            if(!context.Target(args,out var target,out error))return false;
            string holder=(string)args["holder"]["objectId"];
            if(context.Workshop&&context.Workshop.ControlsTarget(holder)){error="Finish authoring the holder before carrying a prop";return false;}
            if(target.GetComponent<RigidRoomItem>()?.AnimationOwned==true){error="Another animation owns this prop";return false;}
            return HeldRoomProp.CanAttach(context.Editor,Attachment(args),Anchor((JObject)args["holder"]),out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var prop=HeldRoomProp.Begin(context.Editor,Attachment(args),Anchor((JObject)args["holder"]),(float)args["seconds"],out error);if(!prop)return false;
            operation=new HoldOperation(prop,args);return true;
        }
        sealed class HoldOperation:CapabilityOperation
        {
            readonly HeldRoomProp prop;readonly JObject args;bool stopped;
            public HoldOperation(HeldRoomProp prop,JObject args){this.prop=prop;this.args=(JObject)args.DeepClone();}
            public override float Seconds=>(float)args["seconds"];
            public override RuleActionState State(out string error){error="The prop action was interrupted";return prop&&prop.isActiveAndEnabled&&prop.Valid(out error)?RuleActionState.Ready:RuleActionState.Failed;}
            public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;prop.Finish();return prop.Valid(out error)&&((string)args["release"]=="return"||prop.Released);}
            public override JObject Result=>new(){["target"]=args["target"],["holder"]=args["holder"]["objectId"],["phase"]=(string)args["release"] switch {"throw"=>"thrown","drop"=>"dropped",_=>"returned"},["released"]=prop&&prop.Released};
            public override string InterruptionStatus=>prop&&prop.Released?"The prop was already released; cancelling does not undo its physical motion":null;
            public override void Stop(bool preservePlacement){if(stopped)return;stopped=true;if(prop)prop.End(preservePlacement);}
        }
    }
}
