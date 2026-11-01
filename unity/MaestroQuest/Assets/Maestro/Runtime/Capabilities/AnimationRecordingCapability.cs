// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class AnimationRecordingCapability:CapabilityModule
    {
        // Existing sessions own their preview/take. Finish and discard must not
        // acquire a replacement instance, especially after loss or a failed save.
        internal override IEnumerable<string> NativeEntities(JObject arguments) {
            if((string)arguments["operation"]=="start")yield return (string)arguments["target"];
        }
        public override string Id=>"animation.record";
        public override string Label=>"Record, save or discard a take";
        public override string Duration=>"completion";
        public override string Ownership=>"authoringSession";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.session.current","recording.control.available"};
        public override string Description=>"Control the same recorder as the physical Record/Discard take controls. Read animation.recording first and pass its exact sessionId. start also takes target and its current object revision; it refuses another live actor or retained unsaved take. It begins a persistent authoring session, samples at most 10 times/second for 30 seconds/301 frames, permits gripping the recorded object, and completes the start action immediately. The session owns wholeTarget independently until finish, discard or lifecycle Stop; stopping the completed start receipt does not end the take. finish freezes then queues background preparation/publication of that exact take as one Undo (temporary in a room fork). Completion confirms native application and saving. Physical Finish, lifecycle Stop and agent Finish observe the same session-owned save; stopping an observer does not cancel it. Before native acceptance, explicit discard cancels the save and keeps the previous animation; after acceptance, wait for its outcome. Pre-acceptance failures retain frozen frames for retry or discard. Failed publication retains the buffer, leaves the applied edit unconfirmed and blocks saving; do not replay it. Discarding that failed buffer does not undo an applied animation. Retried saves refuse changed target revisions or room sessions. discard destroys only the matching live/retained buffer and does not undo a previously applied edit. Start a new take only using the next observed sessionId. Pause, focus loss, selection/avatar change, physical Stop and time/frame limits freeze and attempt to finish automatically; suspended native interaction defers acceptance until resumed, and unsaved failures remain visible. App termination/reopening loses unsaved in-memory frames and issues a different identity. Neither finish nor discard starts playback. Only one recording session exists per room.";
        static JObject Variant(string operation,string title,JObject fields)
        {
            var props=new JObject {["operation"]=Choice(operation),["sessionId"]=Text("^[a-f0-9]{32}$",32)};props["operation"]["x-static"]=true;
            foreach(var p in fields.Properties())props[p.Name]=p.Value.DeepClone();var schema=Object(props);schema["title"]=title;schema["x-features"]=new JArray("animationRecording.v1");return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Recording operation",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Variant("start","Start a take",new JObject {["target"]=AnimationAuthoringCapability.Target(),["revision"]=Revision()}),
            Variant("finish","Save this take",new JObject {["target"]=AnimationAuthoringCapability.Target()}),
            Variant("discard","Discard this take",new JObject()))};
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["phase"]=Choice("recording","saved","discarded"),["frames"]=Number(1,301,true),["duration"]=Number(0,30),["revision"]=Revision(true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="start",["sessionId"]=new string('0',32),["target"]="maestro",["revision"]=1};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="Animation workshop is unavailable";var workshop=context.Workshop;if(!workshop)return false;
            return (string)args["operation"]=="start"?workshop.CanStartRecording((string)args["sessionId"],(string)args["target"],(int)args["revision"],out error):workshop.CanResolveRecording((string)args["sessionId"],(string)args["target"],(string)args["operation"]=="discard",out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            if((string)args["operation"]=="finish"){
                if(!context.Workshop.BeginFinishRecording((string)args["sessionId"],(string)args["target"],out var attempt,out error))return false;
                operation=new RecordingCompletion(attempt);return true;
            }
            JObject result;
            bool accepted=(string)args["operation"] switch {
                "start"=>context.Workshop.StartRecording((string)args["sessionId"],(string)args["target"],(int)args["revision"],out result,out error),
                _=>context.Workshop.DiscardRecording((string)args["sessionId"],out result,out error)
            };
            if(!accepted)return false;operation=new CompletedCapability(result);return true;
        }
        sealed class RecordingCompletion:CapabilityOperation
        {
            readonly AnimationWorkshop.RecordingSaveAttempt attempt;
            bool stopped;
            internal RecordingCompletion(AnimationWorkshop.RecordingSaveAttempt attempt)=>this.attempt=attempt;
            public override float Seconds=>0;
            public override Rules.RuleActionState State(out string error){
                if(stopped){error="Recording save observation stopped";return Rules.RuleActionState.Failed;}
                return !attempt.Poll(out var ready,out error)?Rules.RuleActionState.Failed:ready?Rules.RuleActionState.Ready:Rules.RuleActionState.Preparing;
            }
            public override bool Complete(out string error){
                if(stopped){error="Recording save observation stopped";return false;}
                if(!attempt.Poll(out var ready,out error))return false;
                if(!ready){error="The recording is still saving";return false;}return true;
            }
            public override JObject Result=>attempt.Result??new JObject();
            public override void Stop(bool preservePlacement)=>stopped=true;
            public override string InterruptionStatus=>attempt.InterruptionStatus;
        }
        public static BehaviourCatalog.FactDefinition Fact()=>new("animation.recording",ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"target\":\"text\",\"phase\":\"text\",\"frames\":\"number\",\"duration\":\"number\",\"revision\":\"number\",\"temporary\":\"boolean\",\"error\":\"text\"}}")),
            "Current recording session","Native recorder identity and phase: idle issues the next start ID; recording samples a live take; saving is a frozen session-owned background save; unsaved retains a frozen failed take in memory. Duration is sampled seconds, not wall time. Idle has empty target and zero counts/revision. Read target's authored revision before starting. Finish/discard take the current exact ID; completed receipt cancellation is separate. Error is bounded status, not a recovery token. A new app/room instance never restores an active recording.",null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<AnimationWorkshop>() is AnimationWorkshop workshop&&workshop?ProgramValue.Literal(workshop.ObserveRecording()):null);
    }
}
