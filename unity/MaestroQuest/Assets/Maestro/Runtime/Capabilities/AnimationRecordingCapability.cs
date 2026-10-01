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
        public override string Id=>"animation.record";
        public override string Label=>"Record, save or discard a take";
        public override string Duration=>"instant";
        public override string Ownership=>"authoringSession";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.session.current","recording.control.available"};
        public override string Description=>"Control the same recorder as the physical Record/Discard take controls. Read animation.recording first and pass its exact sessionId. start also takes target and its current object revision; it refuses another live actor or retained unsaved take. It begins a persistent authoring session, samples at most 10 times/second for 30 seconds/301 frames, permits gripping the recorded object, and completes the start action immediately. The session owns wholeTarget independently until finish, discard or lifecycle Stop; stopping the completed start receipt does not end the take. finish freezes then saves that exact take as one Undo (temporary in a room fork); failed saves stop sampling and retain frames in memory for retry or discard. Retried saves refuse changed target revisions or room sessions. discard destroys only the matching live/retained take, without changing the previous saved animation. Start a new take only using the next observed sessionId. Pause, focus loss, selection/avatar change, physical Stop and time/frame limits attempt to finish automatically; unsaved failures remain visible. App termination/reopening loses unsaved in-memory frames and issues a different identity. Neither finish nor discard starts playback. Only one recording session exists per room.";
        static JObject Variant(string operation,string title,JObject fields)
        {
            var props=new JObject {["operation"]=Choice(operation),["sessionId"]=Text("^[a-f0-9]{32}$",32)};props["operation"]["x-static"]=true;
            foreach(var p in fields.Properties())props[p.Name]=p.Value.DeepClone();var schema=Object(props);schema["title"]=title;schema["x-features"]=new JArray("animationRecording.v1");return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Recording operation",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            Variant("start","Start a take",new JObject {["target"]=AnimationAuthoringCapability.Target(),["revision"]=Number(1,1000000,true)}),
            Variant("finish","Save this take",new JObject {["target"]=AnimationAuthoringCapability.Target()}),
            Variant("discard","Discard this take",new JObject()))};
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["phase"]=Choice("recording","saved","discarded"),["frames"]=Number(1,301,true),["duration"]=Number(0,30),["revision"]=Number(0,1000000,true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="start",["sessionId"]=new string('0',32),["target"]="maestro",["revision"]=1};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="Animation workshop is unavailable";var workshop=context.Workshop;if(!workshop)return false;
            return (string)args["operation"]=="start"?workshop.CanStartRecording((string)args["sessionId"],(string)args["target"],(int)args["revision"],out error):workshop.CanResolveRecording((string)args["sessionId"],(string)args["target"],(string)args["operation"]=="discard",out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;JObject result;
            bool accepted=(string)args["operation"] switch {
                "start"=>context.Workshop.StartRecording((string)args["sessionId"],(string)args["target"],(int)args["revision"],out result,out error),
                "finish"=>context.Workshop.FinishRecording((string)args["sessionId"],(string)args["target"],out result,out error),
                _=>context.Workshop.DiscardRecording((string)args["sessionId"],out result,out error)
            };
            if(!accepted)return false;operation=new CompletedCapability(result);return true;
        }
        public static BehaviourCatalog.FactDefinition Fact()=>new("animation.recording",ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"target\":\"text\",\"phase\":\"text\",\"frames\":\"number\",\"duration\":\"number\",\"revision\":\"number\",\"temporary\":\"boolean\",\"error\":\"text\"}}")),
            "Current recording session","Native recorder identity and phase: idle issues the next start ID; recording samples a live take; unsaved retains a frozen failed take in memory. Duration is sampled seconds, not wall time. Idle has empty target and zero counts/revision. Read target's authored revision before starting. Finish/discard take the current exact ID; completed receipt cancellation is separate. Error is bounded status, not a recovery token. A new app/room instance never restores an active recording.",null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<AnimationWorkshop>() is AnimationWorkshop workshop&&workshop?ProgramValue.Literal(workshop.ObserveRecording()):null);
    }
}
