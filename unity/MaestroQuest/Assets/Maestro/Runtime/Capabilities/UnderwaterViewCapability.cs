// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class UnderwaterViewCapability:NativeTargetCapability {
        internal const string Feature="underwaterView.v1";
        public override string Id=>"object.container.view.configure";
        public override string Label=>"Configure underwater view";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable","target.container"};
        public override string Description=>"Set a configured water body's underwater view. Virtual world surfaces fade toward tint according to the actual metres of this finite water along the viewing ray. densityPerMetre=0 disables attenuation; larger values reduce visibility. This applies only while the rendering viewpoint is inside water. Nested bodies choose the smallest containing cavity, then stable ID; it is not refraction, general transmission through outside water, or a mixture of overlapping fluids. Surface paint/opacity, fluid identity/quantity/forces, audio, real-room collision and passthrough remain separate. Book pages and recovery controls retain readability. The settings belong to the cavity and do not travel with pouring. Newly configured and retained legacy cavities default to disabled. Read object.container.view for current revision and settings. Ordinary container edits preserve them. Geometry must be available, but pausing physics does not remove the view."+PersistedEditOperation.Persistence;
        internal static JObject SettingsSchema(){
            var s=Object(new JObject{["version"]=Number(1,1,true),["enabled"]=new JObject{["type"]="boolean"},["tint"]=Text("^#[0-9A-Fa-f]{6}$",7),["densityPerMetre"]=Number(0,8)});
            s["x-features"]=new JArray(Feature);return s;
        }
        public override JObject InputSchema=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["settings"]=SettingsSchema()}),"object.container.view","revision",new JObject{["target"]="target"},"settings");
        public override JObject Example=>new(){["target"]=new string('0',32),["revision"]=1,["settings"]=new JObject{["version"]=1,["enabled"]=true,["tint"]="#285B78",["densityPerMetre"]=.4}};
        public override bool Validate(JObject a,out string error){error="Choose valid underwater tint and a finite density from 0 to 8 per metre";if(!UnderwaterViewSettings.Wire(a["settings"]))return false;error=null;return true;}
        static bool Read(CapabilityContext c,JObject a,out RoomContainer data,out string error){
            data=c.Editor?.Read((string)a["target"])?.containers?.FirstOrDefault()?.Copy();
            error="Configure the object's liquid container first";if(data==null)return false;
            data.underwater=a["settings"].ToObject<UnderwaterViewSettings>();error=null;return true;
        }
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>Read(c,a,out var data,out error)&&c.Editor.PrepareContainer((string)a["target"],(int)a["revision"],data,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            operation=null;if(!Read(c,a,out var data,out error))return false;string target=(string)a["target"];int revision=(int)a["revision"];
            return PersistedEditOperation.Start(c.Editor,target,(out RoomEditor.PendingPersistedEdit edit,out string issue)=>c.Editor.BeginContainer(target,revision,data,out edit,out issue),out operation,out error,validate:(out string issue)=>c.Editor.CanEditContainer(target,revision,out issue));
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.container.view",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["settings"]=SettingsSchema()})),"Underwater view settings","Saved finite-water presentation and exact object revision. Reading does not load geometry, start physics or capture a camera. configured=false supplies disabled defaults; configure a container before editing its view.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{
            var data=c.Editor?.Read((string)a["target"]);if(data==null||data.IsBuiltIn)return null;var water=data.containers?.FirstOrDefault();
            return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(data.id),["configured"]=water!=null,["settings"]=JObject.FromObject(UnderwaterViewSettings.Effective(water?.underwater))});
        },features:new[]{Feature});
    }
}
