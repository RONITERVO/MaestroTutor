// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class WaterAudioCapability:NativeTargetCapability {
        internal const string Feature="waterAudio.v1";
        public override string Id=>"object.container.audio.configure";
        public override string Label=>"Configure water sound";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable","target.container"};
        public override string Description=>"Configure opt-in sound treatment for a finite liquid cavity. Spatial Maestro speech and authored spatial PCM sounds use the same live water path between emitter and tracked listener. attenuationDbPerMetre adds loss along assigned wet metres, capped at 48 dB; mufflingPerMetre increases a bounded low-pass treatment, capped at a 120 Hz minimum cutoff. Zero disables that part of the effect. This is styled sound, not acoustic refraction or a pressure simulation. Overlaps choose the smallest cavity then stable ID; disabled inner water keeps its intervals and does not borrow outer treatment. Settings belong to the cavity and do not pour with liquid. Newly configured and legacy cavities default to disabled. Existing voice/source/mix gain, nonspatial sounds, microphone input, visibility/opacity, underwater tint, real-room collision, physics pause and passthrough remain independent. Missing geometry, listener tracking or focus bypasses treatment and is exposed by world.audio.water; it never mutes speech or resumes physics/loads objects. Parameters refresh at up to 20 Hz and expire after 0.2 seconds, with a 20 ms audio ramp. Read object.container.audio for the current revision/settings. Ordinary container edits preserve them."+PersistedEditOperation.Persistence;
        internal static JObject SettingsSchema(){
            var s=Object(new JObject{["version"]=Number(1,1,true),["enabled"]=new JObject{["type"]="boolean"},["attenuationDbPerMetre"]=Number(0,24),["mufflingPerMetre"]=Number(0,8)});
            s["x-features"]=new JArray(Feature);return s;
        }
        public override JObject InputSchema=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["settings"]=SettingsSchema()}),"object.container.audio","revision",new JObject{["target"]="target"},"settings");
        public override JObject Example=>new(){["target"]=new string('0',32),["revision"]=1,["settings"]=new JObject{["version"]=1,["enabled"]=true,["attenuationDbPerMetre"]=1.5,["mufflingPerMetre"]=1}};
        public override bool Validate(JObject a,out string error){error="Choose finite sound loss from 0 to 24 dB/metre and muffling from 0 to 8 per metre";if(!WaterAudioSettings.Wire(a["settings"]))return false;error=null;return true;}
        static bool Read(CapabilityContext c,JObject a,out RoomContainer data,out string error){
            data=c.Editor?.Read((string)a["target"])?.containers?.FirstOrDefault()?.Copy();
            error="Configure the object's liquid container first";if(data==null)return false;
            data.waterAudio=a["settings"].ToObject<WaterAudioSettings>();error=null;return true;
        }
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>Read(c,a,out var data,out error)&&c.Editor.PrepareContainer((string)a["target"],(int)a["revision"],data,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            operation=null;if(!Read(c,a,out var data,out error))return false;string target=(string)a["target"];int revision=(int)a["revision"];
            return PersistedEditOperation.Start(c.Editor,target,(out RoomEditor.PendingPersistedEdit edit,out string issue)=>c.Editor.BeginContainer(target,revision,data,out edit,out issue),out operation,out error,validate:(out string issue)=>c.Editor.CanEditContainer(target,revision,out issue));
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.container.audio",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["settings"]=SettingsSchema()})),"Water sound settings","Saved water audio treatment and exact object revision. Reading does not load geometry, start audio/physics or capture anything. configured=false supplies disabled defaults; configure a container before editing sound treatment.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{
            var data=c.Editor?.Read((string)a["target"]);if(data==null||data.IsBuiltIn)return null;var water=data.containers?.FirstOrDefault();
            return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(data.id),["configured"]=water!=null,["settings"]=JObject.FromObject(WaterAudioSettings.Effective(water?.waterAudio))});
        },features:new[]{Feature});
    }
}
