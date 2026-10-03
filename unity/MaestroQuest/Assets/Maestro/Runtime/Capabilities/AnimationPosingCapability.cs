// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class AnimationPosingCapability:CapabilityModule
    {
        public override string Id=>"animation.pose";
        public override string Label=>"Pose Maestro together";
        public override string Duration=>"instant";
        public override string Ownership=>"authoringSession";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.session.current","pose.version.current","joint.unheld","pose.control.available"};
        public override string Description=>"Share the same live Maestro pose as the physical joint handles. Inspect animation.posing first. start requires its idle sessionId and current Maestro revision. The start receipt completes immediately while the pose session owns Maestro independently; cancelling a completed receipt does not stop posing. rotate patches 1–8 distinct supported joints at the exact sessionId/version; rotations are normalized canonical local quaternions, never imported bone or world coordinates. It uses the physical limits relative to canonical rest: head 75 degrees, spine/chest 45, others 150. Read animation.pose.joint for the actual clamped result. Handle movement, saved edits and failed-save retention change the opaque pose version; inspect again before every mutation. Edits and reads refuse a held joint. rotate only changes the live preview; save commits one Undo and keeps posing, finish saves and ends it, discard abandons only the unsaved preview (previous handle releases/saves stay saved). Physical handle releases save automatically; physical Stop, focus/pause and selection changes attempt finish. Failed saves stop posing and retain the frozen pose in memory: save/finish retry only its original room and object revision; discard removes only that retained pose. No restart/replay after launch; process termination loses unsaved memory. Agent/program start and recovery cannot interrupt another live actor. During an active recording rotate remains available, but save/finish/discard require finishing the recording first; recording completion ends both authoring sessions. Temporary saved edits remain in the fork until Keep. No playback starts.";
        static JObject Identity()=>new() {["sessionId"]=Text("^[a-f0-9]{32}$",32),["version"]=Number(1,1000000,true)};
        static JObject Variant(string operation,string title,JObject fields)
        {
            var props=new JObject {["operation"]=Choice(operation),["target"]=Resource(Choice("maestro")),["sessionId"]=Text("^[a-f0-9]{32}$",32)};
            props["operation"]["x-static"]=true;
            foreach(var property in fields.Properties())props[property.Name]=property.Value.DeepClone();var schema=Object(props);schema["title"]=title;schema["x-features"]=new JArray("animationPosing.v1");return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Pose operation",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Variant("start","Begin posing",new JObject {["revision"]=Revision()}),
            Variant("rotate","Adjust joints",new JObject {["version"]=Number(1,1000000,true),["joints"]=List(AnimationAuthoringCapability.JointSchema(),1,8)}),
            Variant("save","Save pose",new JObject {["version"]=Number(1,1000000,true)}),
            Variant("finish","Save and finish posing",new JObject {["version"]=Number(1,1000000,true)}),
            Variant("discard","Discard unsaved pose",new JObject {["version"]=Number(1,1000000,true)}))};
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("posing","saved","discarded"),["version"]=Number(1,1000000,true),["revision"]=Revision(),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="start",["target"]="maestro",["sessionId"]=new string('0',32),["revision"]=1};
        public override bool Validate(JObject args,out string error)
        {
            error="Choose distinct joints with normalized rotations";
            if((string)args["operation"]=="rotate"&&!MotionFrame.ValidJoints(AnimationAuthoringCapability.Joints(args["joints"])))return false;
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="Animation workshop is unavailable";if(!context.Workshop)return false;
            return (string)args["operation"] switch {
                "start"=>context.Workshop.CanStartPose((string)args["sessionId"],(int)args["revision"],out error),
                "rotate"=>context.Workshop.CanRotatePose((string)args["sessionId"],(int)args["version"],AnimationAuthoringCapability.Joints(args["joints"]),out error),
                _=>context.Workshop.CanUsePose((string)args["sessionId"],(int)args["version"],(string)args["operation"],out error)
            };
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;JObject result;string session=(string)args["sessionId"];
            bool accepted=(string)args["operation"] switch {
                "start"=>context.Workshop.StartPose(session,(int)args["revision"],out result,out error),
                "rotate"=>context.Workshop.RotatePose(session,(int)args["version"],AnimationAuthoringCapability.Joints(args["joints"]),out result,out error),
                _=>context.Workshop.ResolvePose(session,(int)args["version"],(string)args["operation"],out result,out error)
            };
            if(!accepted)return false;operation=new CompletedCapability(result);return true;
        }
        public static BehaviourCatalog.FactDefinition Fact()=>new("animation.posing",ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"phase\":\"text\",\"version\":\"number\",\"revision\":\"number\",\"holding\":\"boolean\",\"temporary\":\"boolean\",\"joints\":{\"list\":\"text\"},\"error\":\"text\"}}")),
            "Current live pose session","Shared physical/agent pose session: idle issues the next start identity (version 0); posing identifies the current preview and supported displayed joint channels; unsaved identifies a retained failed pose and its original object revision. Read this before each mutation; version is an opaque change counter and can skip. Joint holds prevent shared mutations/readback until released. Idle has no live joint values. Retained data is memory-only, cannot overwrite a changed room/object, and is lost on process termination.",null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<AnimationWorkshop>() is AnimationWorkshop workshop&&workshop?ProgramValue.Literal(workshop.ObservePosing(),BehaviourCatalog.Fact("animation.posing").Type):null);
        public static BehaviourCatalog.FactDefinition Joint()=>new("animation.pose.joint",ProgramDataType.Read(JObject.Parse("{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}}")),
            "Live pose joint rotation","Read one actual canonical local rotation from the exact live or retained pose session. Joint names come from animation.posing. Unavailable for idle/stale sessions or while a physical joint is held; never substitutes an automatic animation or saved pose. Physical handles and shared edits use the same canonical rig and limits. Reading makes no edits.",Object(new JObject(Identity().Properties()) {["joint"]=Choice(Enum.GetNames(typeof(PoseJoint)))}),new JObject {["sessionId"]=new string('0',32),["version"]=1,["joint"]="Head"},(context,args)=>{
                var workshop=context.Editor?context.Editor.GetComponent<AnimationWorkshop>():null;if(!workshop)return null;
                var value=workshop.ReadPoseJoint((string)args["sessionId"],(int)args["version"],Enum.Parse<PoseJoint>((string)args["joint"]));return value==null?null:ProgramValue.Literal(value);
            });
    }
}
