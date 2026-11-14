// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class AudioSchema
    {
        internal const string Feature="worldAudio.v1";
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        internal static JObject Id()=>Text("^[a-f0-9]{32}$",32);
        internal static JObject Target()=>Resource(Text("^(book|maestro|[a-f0-9]{32})$",32));
        internal static JObject Key()=>Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24);
        static JObject Format(JObject schema,string format){schema["format"]=format;return schema;}
        internal const string ClipFeature="audioClips.v1",StreamFeature="audioStreams.v1";
        internal static JObject Endpoint()=>Text(@"^https://[^\s?#@]+$",2048);
        internal static JObject Clip()=>Object(new JObject {["assetHash"]=Text("^[a-f0-9]{64}$",64),["seconds"]=Number(.03,WaveAudio.MaximumSeconds)});
        internal static JObject Source(){
            var tone=Format(Object(new JObject {["name"]=Text("^.{0,80}$",80),["kind"]=Choice("tone"),["tone"]=Object(new JObject {["wave"]=Choice("sine","triangle","noise"),["frequency"]=Number(20,10000),["endFrequency"]=Number(20,10000),["seconds"]=Number(.03,30),["attack"]=Number(.005,2),["release"]=Number(.005,2),["seed"]=Number(1,int.MaxValue,true)})}),"worldAudioSource");
            var clip=Object(new JObject {["name"]=Text("^.{0,80}$",80),["kind"]=Choice("clip"),["clip"]=Clip()});clip["x-features"]=new JArray(ClipFeature);tone["title"]="Generated tone";clip["title"]="Imported WAV clip";
            var stream=Format(Object(new JObject {["name"]=Text("^.{0,80}$",80),["kind"]=Choice("stream"),["stream"]=Object(new JObject {["endpoint"]=Endpoint()})}),"worldAudioStream");
            stream["x-features"]=new JArray(StreamFeature);stream["title"]="Live HTTPS MP3";
            return new JObject {["type"]="object",["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(tone,clip,stream)};
        }
        internal static RoomAudioDefinition ReadSource(JToken value){
            var d=(string)value["kind"]=="stream"?new RoomAudioDefinition {version=2,endpoint=(string)value["stream"]["endpoint"]}:(string)value["kind"]=="clip"?new RoomAudioDefinition {assetHash=(string)value["clip"]["assetHash"],seconds=(float)value["clip"]["seconds"]}:JsonUtility.FromJson<RoomAudioDefinition>(value["tone"].ToString());
            d.name=(string)value["name"];d.kind=(string)value["kind"];return d;
        }
        // Facts have a fixed record shape. Inactive variants are explicit inert defaults;
        // executable source definitions include only the selected kind.
        internal static JObject ObserveSource(RoomAudioDefinition value){
            var result=Encode(value);result.Remove("stream");
            if(value.kind!="tone")result["tone"]=Encode(new RoomAudioDefinition())["tone"];
            if(value.kind!="clip")result["clip"]=new JObject {["assetHash"]="",["seconds"]=0};
            return result;
        }
        internal static JObject Emitter()=>Format(Object(new JObject {["source"]=Id(),["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["joint"]=Choice(new[]{""}.Concat(Enum.GetNames(typeof(PoseJoint))).ToArray()),["position"]=Object(new JObject {["x"]=Number(-10,10),["y"]=Number(-10,10),["z"]=Number(-10,10)}),["role"]=Choice("effects","media","ambience"),["spatial"]=new JObject {["type"]="boolean"},["gain"]=Number(0,1),["distance"]=Object(new JObject {["minimum"]=Number(.1,10),["maximum"]=Number(.1,50)})}),"worldAudioEmitter");
        internal static JObject Encode(object data){
            var j=JObject.Parse(JsonUtility.ToJson(data));j.Remove("version");j.Remove("id");
            if(data is RoomAudioDefinition source){if(source.kind=="stream")return new JObject {["name"]=source.name,["kind"]="stream",["stream"]=new JObject {["endpoint"]=source.endpoint}};j.Remove("endpoint");if(source.kind=="clip")return new JObject {["name"]=source.name,["kind"]="clip",["clip"]=new JObject {["assetHash"]=source.assetHash,["seconds"]=source.seconds}};j.Remove("assetHash");var name=j["name"];var kind=j["kind"];j.Remove("name");j.Remove("kind");return new JObject {["name"]=name,["kind"]=kind,["tone"]=j};}
            j["distance"]=new JObject {["minimum"]=j["minDistance"],["maximum"]=j["maxDistance"]};j.Remove("minDistance");j.Remove("maxDistance");return j;
        }
        internal static JObject Receipt(RoomEditor e,string id)=>new() {["id"]=id,["revision"]=e.AudioRevision(id),["temporary"]=e.TemporaryRoom};
        internal static JObject ReceiptSchema()=>Object(new JObject {["id"]=Id(),["revision"]=Revision(true),["temporary"]=new JObject {["type"]="boolean"}});
    }
    internal sealed class AudioSourceCapability:CapabilityModule
    {
        public override string Id=>"audio.source.edit";
        public override string Label=>"Create or edit a reusable sound";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Requirements=>new[]{"source.revision.current","storage.writable"};
        public override string Description=>"Save or remove a reusable room sound definition with one Undo. For a new sound use id='' and revision=0; the receipt returns its generated stable ID. For an existing sound use its exact ID and latest audio.source.definition revision. Source kind tone is a bounded 24 kHz mono sine, triangle or seeded noise recipe with linear frequency sweep and attack/release envelopes (their sum must fit seconds). No API call, microphone or network connection is opened. Editing a source affects future plays; an active play keeps its captured source revision. Removal fails while any saved emitter references the sound. Source kind clip references an exact privately imported WAV assetHash and inspected seconds from audio.import. Missing or changed bytes fail playback explicitly; no file is fetched by this action. Source kind stream saves a direct public HTTPS MP3 endpoint without credentials, query strings or fragments. Read audio.source.stream for its details. It requires room format 40 and source version 2; saving does not connect. Only audio.start with loop=false opens its connection, without cookies, authorization or redirects. To repeat a saved sound, use audio.start with loop=true; this edit action never starts playback. Definitions survive saving, temporary rooms and workspace exports; playback never auto-starts on restore."+PersistedEditOperation.Persistence;
        static JObject Variant(string operation){var p=new JObject {["operation"]=Choice(operation),["id"]=operation=="save"?Text("^([a-f0-9]{32})?$",32):AudioSchema.Id(),["revision"]=Revision(operation=="save")};p["operation"]["x-static"]=true;if(operation=="save")p["definition"]=AudioSchema.Source();return AudioSchema.Featured(Object(p));}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("save"),Variant("remove"))};
        public override JObject OutputSchema=>AudioSchema.ReceiptSchema();
        public override JObject Example=>new(){["operation"]="save",["id"]="",["revision"]=0,["definition"]=AudioSchema.Encode(new RoomAudioDefinition {name="Robot greeting"})};
        static RoomAudioDefinition Read(JObject a,string id){if((string)a["operation"]=="remove")return null;var d=AudioSchema.ReadSource(a["definition"]);d.id=id;return d;}
        public override bool Validate(JObject a,out string error){error="A new sound needs revision zero; an existing sound needs its current revision";if(string.IsNullOrEmpty((string)a["id"])!=((int)a["revision"]==0))return false;var d=Read(a,new string('0',32));if(d==null){error=null;return true;}return d.Validate(out error);}
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="The room editor is unavailable";if(!c.Editor||!Validate(a,out error))return false;var id=string.IsNullOrEmpty((string)a["id"])?Guid.NewGuid().ToString("N"):(string)a["id"];return c.Editor.CanEditAudio(id,(int)a["revision"],Read(a,id),out error);}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(c,a,out error))return false;
            var editor=c.Editor;string id=string.IsNullOrEmpty((string)a["id"])?Guid.NewGuid().ToString("N"):(string)a["id"];
            int revision=(int)a["revision"];var definition=Read(a,id);bool temporary=editor.TemporaryRoom;RoomEditor.PendingPersistedEdit pending=null;
            return PersistedEditOperation.Start(editor,null,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ready=editor.BeginAudio(id,revision,definition,out edit,out issue);pending=edit;return ready;},
                out operation,out error,validate:(out string issue)=>editor.CanEditAudio(id,revision,definition,out issue),
                output:()=>new JObject{["id"]=id,["revision"]=pending.AcceptedAudioRevision(id),["temporary"]=temporary});
        }
    }
    internal sealed class AudioEmitterCapability:NativeTargetCapability
    {
        public override string Id=>"object.audioEmitter.edit";
        public override string Label=>"Attach a sound to an object";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure or remove a saved sound emitter on an object, including the book or Maestro. Use the exact source ID from audio.source.edit/list. Empty part and joint attach to the object root; choose either an existing recipe part or one Maestro joint. Joint offsets use canonical avatar axes and root scale through model changes. Up to four emitters per object and 32 in a room. minDistance/maxDistance control logarithmic attenuation; gain is 0–1, spatial=false is nonspatial. The semantic role is saved for future mixing policy; it does not acquire conversation/microphone privileges. Editing never starts playback. An active play ends if its emitter changes or disappears; movement/grabbing does not change its definition. Copies share the source ID and remain silent. Use the current object revision from object.audioEmitter before editing."+PersistedEditOperation.Persistence;
        static JObject Variant(string op){var p=new JObject {["operation"]=Choice(op),["target"]=AudioSchema.Target(),["revision"]=Revision(),["emitter"]=AudioSchema.Key()};p["operation"]["x-static"]=true;if(op=="configure")p["definition"]=AudioSchema.Emitter();var schema=AudioSchema.Featured(CurrentInputs(Object(p),"object.audioEmitter","revision",new JObject {["target"]="target",["emitter"]="emitter"}));return op=="configure"?ResourceChoice(schema,"Sound","audio.source.list","definition.source"):schema;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]="maestro",["revision"]=1,["emitter"]="beep",["definition"]=AudioSchema.Encode(new RoomAudioEmitter {source=new string('0',32),joint="Head"})};
        static RoomAudioEmitter Read(JObject a){if((string)a["operation"]=="remove")return null;var e=JsonUtility.FromJson<RoomAudioEmitter>(a["definition"].ToString());e.minDistance=(float)a["definition"]["distance"]["minimum"];e.maxDistance=(float)a["definition"]["distance"]["maximum"];e.version=1;e.id=(string)a["emitter"];return e;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.CanEditAudioEmitter((string)a["target"],(int)a["revision"],(string)a["emitter"],Read(a),out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(c,a,out error))return false;
            var editor=c.Editor;string target=(string)a["target"],key=(string)a["emitter"];int revision=(int)a["revision"];var definition=Read(a);
            return PersistedEditOperation.Start(editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginAudioEmitter(target,revision,key,definition,out edit,out issue),
                out operation,out error,validate:(out string issue)=>editor.CanEditAudioEmitter(target,revision,key,definition,out issue));
        }
    }
    internal sealed class AudioPlayCapability:NativeTargetCapability
    {
        public override string Id=>"audio.play";
        public override string Label=>"Play an object's sound";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>(float)WaveAudio.MaximumSeconds+30;
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"audioEmitter"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","audio.emitter.configured","audio.output.ready","audio.voice.available"};
        public override string Description=>"Play one finite sound from a saved object emitter, waiting for consumed PCM and the device output tail before completion. Can be called by manual controls, agent programs or existing event subscriptions (buttons, contacts, timers or Maestro state). Plays retain the exact source revision at start. Moving, gripping or animating the object does not interrupt the sound; emitter edits, deletion, room switch, lost audio device, app suspension or stopping its owning program do. Claims are independent of the object's movement channels. Up to eight world voices, separate from Maestro speech; capacity failure never silently steals another voice. Sources are procedural tones up to 30 seconds or imported PCM/float WAV clips up to 600 seconds. Clips over 30 seconds stream from their checked private copy through bounded shared buffers; they do not decode the entire recording into memory. Imported clips are normalized to the same 24 kHz mono spatial transport; stereo is downmixed. For background music, looping or independently controlled playback use audio.start; audio.play keeps its calling action waiting until the finite sound finishes. Live sources require audio.start, never this finite waiting action. Echo cancellation and headset audible acceptance remain pending. Spatial emitters use the native HRTF/direct obstruction renderer; room reflections remain gated.";
        public override JObject InputSchema=>AudioSchema.Featured(Object(new JObject {["target"]=AudioSchema.Target(),["emitter"]=AudioSchema.Key()}));
        public override JObject OutputSchema=>Object(new JObject {["instance"]=AudioSchema.Id(),["source"]=AudioSchema.Id(),["sourceRevision"]=Revision(),["seconds"]=Number(0,WaveAudio.MaximumSeconds)});
        public override JObject Example=>new(){["target"]="maestro",["emitter"]="beep"};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim("audio:"+(string)a["target"]+":"+(string)a["emitter"],"audioEmitter")};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>CanBegin(c,a,false,out error);
        internal static bool CanBegin(CapabilityContext c,JObject a,bool liveAllowed,out string error){error="The sound emitter or source is missing";var item=c.Editor?c.Editor.Find((string)a["target"]):null;var emitter=c.Editor?c.Editor.Read((string)a["target"])?.audioEmitters?.FirstOrDefault(e=>e.id==(string)a["emitter"]):null;if(!item||!item.isActiveAndEnabled||emitter==null||c.Editor.ReadAudio(emitter.source)==null)return false;var source=c.Editor.ReadAudio(emitter.source);if(source.kind=="stream"&&(!liveAllowed||(bool?)a["loop"]==true)){error="Use audio.start with loop=false for a live source";return false;}var audio=c.Editor.GetComponent<WorldAudio>();if(audio)return audio.Available(out error);error=null;return true;}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error))return false;var audio=WorldAudio.For(c.Editor);if(!audio.Begin((string)a["target"],(string)a["emitter"],out var voice,out error))return false;operation=new Playback(audio,voice);return true;}
        sealed class Playback:CapabilityOperation
        {
            readonly WorldAudio audio;readonly WorldAudio.Instance voice;
            internal Playback(WorldAudio audio,WorldAudio.Instance voice){this.audio=audio;this.voice=voice;}
            public override float Seconds=>0;
            public override bool WaitsThroughGrab(string target)=>target==voice.Target;
            public override RuleActionState State(out string error){if(audio)audio.Tick(voice);error=voice.Error;if(!audio&&!voice.Complete)error="The sound workspace was closed";return error!=null?RuleActionState.Failed:voice.Complete?RuleActionState.Ready:RuleActionState.Preparing;}
            public override bool Complete(out string error)=>State(out error)==RuleActionState.Ready;
            public override void Stop(bool preservePlacement){if(audio)audio.Cancel(voice);}
            public override JObject Result=>new(){["instance"]=voice.Id,["source"]=voice.Source,["sourceRevision"]=voice.SourceRevision,["seconds"]=voice.Cursor};
        }
    }
}
