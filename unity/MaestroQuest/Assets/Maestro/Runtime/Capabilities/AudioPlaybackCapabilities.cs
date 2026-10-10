// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class AudioPlaybackCapabilities
    {
        internal static JObject Phase()=>Choice("preparing","playing","paused","completed","stopped","cancelled","failed");
        internal static JObject Query()=>AudioSchema.Featured(Object(new JObject {["target"]=AudioSchema.Target(),["instance"]=AudioSchema.Id()}));
        internal static JObject Example()=>new(){["target"]="maestro",["instance"]=new string('0',32)};
        internal static JObject ResultSchema()=>Object(new JObject {
            ["revision"]=Revision(),
            ["identity"]=Object(new JObject {["instance"]=AudioSchema.Id(),["target"]=AudioSchema.Target(),["emitter"]=AudioSchema.Key(),["source"]=AudioSchema.Id(),["sourceRevision"]=Revision()}),
            ["playback"]=Object(new JObject {["phase"]=Phase(),["seconds"]=Number(0,ProgramValue.MaximumNumber),["loop"]=new JObject {["type"]="boolean"},["gain"]=Number(0,1),["lifetime"]=Choice("action","room")}),
            ["error"]=Text("^.{0,120}$",120)});
        static ProgramDataType ResultType()=>ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"identity\":{\"record\":{\"instance\":\"text\",\"target\":\"text\",\"emitter\":\"text\",\"source\":\"text\",\"sourceRevision\":\"number\"}},\"playback\":{\"record\":{\"phase\":\"text\",\"seconds\":\"number\",\"loop\":\"boolean\",\"gain\":\"number\",\"lifetime\":\"text\"}},\"error\":\"text\"}}"));
        internal static BehaviourCatalog.FactDefinition Fact()=>new("audio.instance",ResultType(),"Sound playback instance",
            "Read one exact transient playback identity and control revision, including its captured source revision, consumed cursor, gain and lifetime. Room-owned playback may continue after its starting task completes. Terminal history retains the most recent 32 instances and 16 control/lifecycle changes each; unavailable means the identity/room changed or retention expired. It never means successful playback. Reading cannot open or advance audio; gain is the current instance gain, not a saved emitter edit. No state or samples are restored after reload. A consumed cursor is not proof that a headset user heard sound.",Query(),Example(),(context,args)=>{
                var audio=context.Editor?context.Editor.GetComponent<WorldAudio>():null;
                return audio&&audio.ReadInstance((string)args["target"],(string)args["instance"],out var sample)?ProgramValue.Literal(sample.Json(),ResultType()):null;
            },features:new[]{AudioSchema.Feature});
        internal static BehaviourCatalog.FactDefinition ActiveFact()=>new("audio.instances",ProgramDataType.Read(JObject.Parse("{\"record\":{\"entries\":{\"list\":{\"record\":{\"instance\":\"text\",\"target\":\"text\",\"emitter\":\"text\",\"phase\":\"text\",\"lifetime\":\"text\"}}}}}")),"Active room sounds",
            "List all current world-audio instances, up to eight, in stable ID order. Includes preparing, playing and paused sounds; excludes separate conversation speech and completed history. Use audio.instance for exact settings/control revision, then audio.control for room-owned sounds. An empty list does not prove silence from every application source. Reading does not start, stop or adopt playback.",null,null,(context,args)=>{
                if(!context.Editor)return null;var audio=context.Editor.GetComponent<WorldAudio>();var entries=audio?audio.ActiveInstances():System.Array.Empty<AudioInstanceSample>();
                return ProgramValue.Literal(new JObject {["entries"]=new JArray(entries.Select(s=>new JObject {["instance"]=s.Instance,["target"]=s.Target,["emitter"]=s.Emitter,["phase"]=s.Phase,["lifetime"]=s.Lifetime}))},BehaviourCatalog.Fact("audio.instances").Type);
            },features:new[]{AudioSchema.Feature});
    }
    internal sealed class AudioStartCapability:CapabilityModule
    {
        public override string Id=>"audio.start";
        public override string Label=>"Start independent room sound";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>10;
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"audioEmitter"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","audio.emitter.configured","audio.output.ready","audio.voice.available"};
        public override string Description=>"Start an independently controlled room sound. Explicit lifetime='room' transfers playback to the current room only when native PCM consumption is confirmed; the result returns the exact instance and current control revision. The program then continues without waiting for the sound to end. loop=true repeats the captured source, including its envelope, until explicitly stopped or its room/emitter becomes unavailable. loop=false ends after the finite source. Stopping the starting action before handoff cancels its preparation; stopping that task after a successful handoff does not retract the room-owned sound. Use audio.control with this exact instance to pause, resume, set gain or stop. Read audio.instance or await audio.instance.changed for later outcomes; a successful start does not mean completed playback. Never use repeated starts as a loop. One active instance per emitter, eight shared world voices across active and retiring workspaces, with no silent replacement. Emitter edits/deletion, room replacement, focus loss and audio-device reset cancel playback; restore does not restart it. Procedural tones and imported PCM/float WAV clips use the same bounded native transport. Live-source adapters remain pending. Decoding sources keep shared admission until cancelled work drains; runtime.audioBudget, runtime.audioReservation and runtime.audioOwner expose these resources without controlling playback.";
        public override JObject InputSchema=>AudioSchema.Featured(Object(new JObject {["target"]=AudioSchema.Target(),["emitter"]=AudioSchema.Key(),["loop"]=new JObject {["type"]="boolean"},["lifetime"]=Choice("room")}));
        public override JObject OutputSchema=>AudioPlaybackCapabilities.ResultSchema();
        public override JObject Example=>new(){["target"]="maestro",["emitter"]="beep",["loop"]=true,["lifetime"]="room"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>new AudioPlayCapability().Claims(args);
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>new AudioPlayCapability().CanRun(context,args,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            var audio=WorldAudio.For(context.Editor);
            if(!audio.Begin((string)args["target"],(string)args["emitter"],out var voice,out error,(bool)args["loop"]))return false;
            operation=new StartOperation(audio,voice);return true;
        }
        sealed class StartOperation:CapabilityOperation
        {
            readonly WorldAudio audio;readonly WorldAudio.Instance voice;bool handedOff;
            internal StartOperation(WorldAudio audio,WorldAudio.Instance voice){this.audio=audio;this.voice=voice;}
            public override float Seconds=>0;
            public override bool WaitsThroughGrab(string target)=>voice.Target==target;
            public override RuleActionState State(out string error)
            {
                if(audio)audio.Tick(voice);error=voice.Error;if(!audio)error="The audio workspace closed";
                return error!=null?RuleActionState.Failed:voice.Cursor>0?RuleActionState.Ready:RuleActionState.Preparing;
            }
            public override bool Complete(out string error)
            {
                if(State(out error)!=RuleActionState.Ready||!audio.Handoff(voice,out error))return false;handedOff=true;return true;
            }
            public override void Stop(bool preservePlacement){if(!handedOff&&audio)audio.Cancel(voice);}
            public override JObject Result=>audio&&audio.ReadInstance(voice.Target,voice.Id,out var sample)?sample.Json():new JObject();
        }
    }
    internal sealed class AudioControlCapability:CapabilityModule
    {
        public override string Id=>"audio.control";
        public override string Label=>"Control a room sound";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"audioControl"};
        public override IReadOnlyList<string> Requirements=>new[]{"audio.instance.exists","source.revision.current"};
        public override string Description=>"Pause, resume, stop or set the gain of the exact room-owned instance returned by audio.start. Read audio.instance immediately before editing and use its revision. A stale handle cannot stop a newer sound on the same emitter. Pause retains queued sound and its place; resume uses that same native source. Device/focus loss cancels rather than resuming. Gain is transient 0–1 and does not rewrite the saved emitter. Pause/resume of an already matching state and stop of a retained terminal instance are idempotent. Other edits to a terminal instance fail. Stop affects only this sound and never clears shared room reflections. audio.play belongs to its waiting action and is stopped through that action; use audio.start when independent controls are needed. Controls do not play a replacement, restart an ended sound or open any connection.";
        static JObject Variant(string op)
        {
            var fields=new JObject {["operation"]=Choice(op),["target"]=AudioSchema.Target(),["instance"]=AudioSchema.Id(),["revision"]=Revision()};fields["operation"]["x-static"]=true;
            if(op=="gain")fields["gain"]=Number(0,1);
            return AudioSchema.Featured(CurrentInputs(Object(fields),"audio.instance","revision",new JObject {["target"]="target",["instance"]="instance"}));
        }
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("pause"),Variant("resume"),Variant("stop"),Variant("gain"))};
        public override JObject OutputSchema=>AudioPlaybackCapabilities.ResultSchema();
        public override JObject Example=>new(){["operation"]="pause",["target"]="maestro",["instance"]=new string('0',32),["revision"]=1};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>new[]{new BehaviourCatalog.Claim("sound-instance:"+(string)args["instance"],"audioControl")};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="The sound workspace is unavailable";var audio=context.Editor?context.Editor.GetComponent<WorldAudio>():null;
            return audio&&audio.CanControl((string)args["target"],(string)args["instance"],(int)args["revision"],(string)args["operation"],out _,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var audio=context.Editor.GetComponent<WorldAudio>();
            if(!audio.Control((string)args["target"],(string)args["instance"],(int)args["revision"],(string)args["operation"],(float?)args["gain"]??0,out var sample,out error))return false;
            operation=new CompletedCapability(sample.Json());return true;
        }
    }
}
