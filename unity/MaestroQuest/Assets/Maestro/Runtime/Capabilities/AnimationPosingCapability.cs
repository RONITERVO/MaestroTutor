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
        // Existing sessions own their preview/take. Finish and discard must not
        // acquire a replacement instance, especially after loss or a failed save.
        internal override IEnumerable<string> NativeEntities(JObject arguments) {
            if((string)arguments["operation"]=="start")yield return (string)arguments["target"];
        }
        public override string Id=>"animation.pose";
        public override string Label=>"Pose Maestro together";
        public override string Duration=>"completion";
        public override string Ownership=>"authoringSession";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.session.current","pose.version.current","joint.unheld","pose.control.available"};
        public override string Description=>"Share Maestro's live pose with the physical joint handles. Read animation.posing first. start completes immediately and opens a persistent session; rotate immediately changes 1–8 supported joints at its exact sessionId/version using normalized canonical local quaternions and the same physical limits. Read animation.pose.joint for the actual clamped value. save freezes the current version, queues one Undo and keeps posing; finish freezes the latest version and ends physical posing. Completion confirms native application and saving of the captured version, not later live edits. Ordered saves share the recording/object edit queue, with at most 32 pending pose versions. Repeated save/finish of a queued version observes that same save. Stopping a receipt stops observation only. Physical handle releases queue saves; physical Stop, focus/pause and selection changes freeze the latest pose and finish. Later joint edits remain live while earlier saves publish; an older result cannot overwrite that preview. Inspect again after every mutation. discard cancels unaccepted queued saves and abandons the live/retained preview; previously accepted poses remain saved. While an accepted save publishes, wait for its outcome before discarding. Preparation failure stops posing and retains the newest live/requested pose for exact retry; failed publication leaves an applied unconfirmed edit and blocks saving, so it must not be replayed. Retried saves require the same room/object revision and cannot steal another actor. Suspended interaction defers acceptance; accepted publication continues. App termination loses unsaved memory. During recording only rotate is available; recording finishes both sessions. Temporary saves stay in the fork until Keep. Neither saving nor discarding starts playback.";
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
            operation=null;if(!CanRun(context,args,out error))return false;
            string session=(string)args["sessionId"],kind=(string)args["operation"];
            if(kind is "save" or "finish"){
                string phase=kind=="finish"||!context.Workshop.IsPosing?"saved":"posing";
                if(!context.Workshop.BeginPoseSave(session,(int)args["version"],kind=="finish",out var attempt,out error))return false;
                operation=new PoseCompletion(attempt,phase);return true;
            }
            JObject result;
            bool accepted=kind switch {
                "start"=>context.Workshop.StartPose(session,(int)args["revision"],out result,out error),
                "rotate"=>context.Workshop.RotatePose(session,(int)args["version"],AnimationAuthoringCapability.Joints(args["joints"]),out result,out error),
                _=>context.Workshop.DiscardPose(session,(int)args["version"],out result,out error)
            };
            if(!accepted)return false;operation=new CompletedCapability(result);return true;
        }
        sealed class PoseCompletion:CapabilityOperation
        {
            readonly AnimationWorkshop.PoseSaveAttempt attempt;readonly string phase;bool stopped;
            internal PoseCompletion(AnimationWorkshop.PoseSaveAttempt attempt,string phase){this.attempt=attempt;this.phase=phase;}
            public override float Seconds=>0;
            public override Rules.RuleActionState State(out string error){
                if(stopped){error="Pose save observation stopped";return Rules.RuleActionState.Failed;}
                return !attempt.Poll(out var ready,out error)?Rules.RuleActionState.Failed:ready?Rules.RuleActionState.Ready:Rules.RuleActionState.Preparing;
            }
            public override bool Complete(out string error){
                if(stopped){error="Pose save observation stopped";return false;}
                if(!attempt.Poll(out var ready,out error))return false;
                if(!ready){error="The pose is still saving";return false;}return true;
            }
            public override JObject Result { get {var value=attempt.Result;if(value!=null)value["phase"]=phase;return value??new JObject();} }
            public override void Stop(bool preservePlacement)=>stopped=true;
            public override string InterruptionStatus=>attempt.InterruptionStatus;
        }
        public static BehaviourCatalog.FactDefinition Fact()=>new("animation.posing",ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"phase\":\"text\",\"version\":\"number\",\"revision\":\"number\",\"holding\":\"boolean\",\"editing\":\"boolean\",\"temporary\":\"boolean\",\"joints\":{\"list\":\"text\"},\"error\":\"text\"}}")),
            "Current live pose session","Shared physical/agent pose session: idle issues the next start identity (version 0); posing identifies the current preview and supported displayed joint channels; saving identifies queued or publishing versions; editing says whether live joint editing continues during that save; unsaved identifies a retained failed pose and its original object revision. Read this before each mutation; version is an opaque change counter and can skip. Joint holds prevent shared mutations/readback until released. Idle has no live joint values. Retained data is memory-only, cannot overwrite a changed room/object, and is lost on process termination.",null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<AnimationWorkshop>() is AnimationWorkshop workshop&&workshop?ProgramValue.Literal(workshop.ObservePosing(),BehaviourCatalog.Fact("animation.posing").Type):null);
        public static BehaviourCatalog.FactDefinition Joint()=>new("animation.pose.joint",ProgramDataType.Read(JObject.Parse("{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}}")),
            "Live pose joint rotation","Read one actual canonical local rotation from the exact live or retained pose session. Joint names come from animation.posing. Unavailable for idle/stale sessions or while a physical joint is held; never substitutes an automatic animation or saved pose. Physical handles and shared edits use the same canonical rig and limits. Reading makes no edits.",Object(new JObject(Identity().Properties()) {["joint"]=Choice(Enum.GetNames(typeof(PoseJoint)))}),new JObject {["sessionId"]=new string('0',32),["version"]=1,["joint"]="Head"},(context,args)=>{
                var workshop=context.Editor?context.Editor.GetComponent<AnimationWorkshop>():null;if(!workshop)return null;
                var value=workshop.ReadPoseJoint((string)args["sessionId"],(int)args["version"],Enum.Parse<PoseJoint>((string)args["joint"]));return value==null?null:ProgramValue.Literal(value);
            });
    }
}
