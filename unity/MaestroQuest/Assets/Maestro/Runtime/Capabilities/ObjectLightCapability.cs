// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ObjectLightCapability:NativeTargetCapability
    {
        internal const string Feature="objectLights.v1";
        public override string Id=>"object.light.edit";
        public override string Label=>"Edit an object lamp";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Save or remove an exact point/spot lamp on a created object or existing recipe part. Use object.lights for the current revision. Position/rotation use the attachment's local frame; spot beams point along local +Z. Range scales with the attachment. Colours are sRGB hex and intensity is a multiplier. Use world lighting/day-cycle controls for dark surroundings; local lamps add to existing ambient/sun light. enabled controls emission independently of the object's paint opacity or visual layer. No real-camera pixels, book pages or control lettering are relit. Hard shadows come from opaque virtual geometry and texture cutouts; blended/faded surfaces transmit light. Scanned/unscanned real obstacles are not shadow casters. Two lamps per object, four per room, six reserved shadow-map faces total: a point light uses six, a spot uses one, including disabled definitions. This bounds rendering work, not a physical Quest performance guarantee. Use object.light.control for transient on/off, intensity and colour without saving. Saves one Undo; copy and creation prototypes preserve the lamps. Changes do not change collisions, physics or the world clock. Readback confirms the native light state, not physical pixels."+PersistedEditOperation.Persistence;
        internal static JObject Name()=>Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32);
        internal static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject SavedSchema()=>Object(new JObject{["version"]=Number(1,1,true),["id"]=Name(),["kind"]=Choice("point","spot"),["part"]=Text("^$|^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["position"]=Offset(),["rotation"]=Vector(true),["enabled"]=Bool(),["color"]=Text("^#[0-9A-Fa-f]{6}$",7),["intensity"]=Number(0,4),["range"]=Number(.1,8),["angle"]=Number(10,160),["shadows"]=Choice("none","hard")});
        static JObject Offset(){var schema=Object(new JObject{["x"]=Number(-10,10),["y"]=Number(-10,10),["z"]=Number(-10,10)});schema["description"]="Local offset, Euclidean length at most 10 metres";return schema;}
        static JObject Settings(){var schema=SavedSchema();((JObject)schema["properties"]).Remove("id");((JArray)schema["required"]).First(x=>(string)x=="id").Remove();return schema;}
        static JObject Variant(string operation){var fields=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["revision"]=Revision(),["light"]=Name()};if(operation=="save")fields["settings"]=Settings();fields["operation"]["x-static"]=true;var schema=CurrentInputs(Object(fields),"object.lights","revision",new JObject{["target"]="target"});schema["x-features"]=new JArray(Feature);schema["title"]=operation=="save"?"Save a lamp":"Remove a lamp";return schema;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("save"),Variant("remove"))};
        public override JObject OutputSchema=>Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["light"]=Name()});
        public override JObject Example {get{var light=JObject.Parse(UnityEngine.JsonUtility.ToJson(new RoomLight()));light.Remove("id");return new(){["operation"]="save",["target"]=new string('0',32),["revision"]=1,["light"]="Lamp",["settings"]=light};}}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareLightEdit((string)a["target"],(int)a["revision"],a,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            string target=(string)a["target"];int revision=(int)a["revision"];var captured=(JObject)a.DeepClone();
            string identity=(string)a["light"];RoomEditor.PendingPersistedEdit accepted=null;
            return PersistedEditOperation.Start(c.Editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool started=c.Editor.BeginLightEdit(target,revision,captured,out edit,out issue);accepted=edit;return started;},
                out operation,out error,validate:(out string issue)=>c.Editor.CanEditLight(target,revision,out issue),
                output:()=>new JObject{["target"]=target,["revision"]=accepted.AcceptedObjectRevision(target),["light"]=identity});
        }
        internal static BehaviourCatalog.FactDefinition Fact(){var type=OutputType(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["lights"]=List(Object(new JObject{["id"]=Name(),["overridden"]=Bool(),["active"]=Bool(),["reason"]=Text(null,160)}),0,2)}));return new("object.lights",type,"Object lamps","Saved lamp IDs and current native emission state. Read object.light with a lamp ID for its exact source and effective controls. Reads are passive. Active does not establish visible illumination, shadows, real-room lighting or headset performance.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>c.Editor&&c.Editor.ObserveLights((string)a["target"]) is JObject v?ProgramValue.Literal(v,type):null,features:new[]{Feature});}
    }
}
