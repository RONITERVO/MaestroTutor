// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ObjectLightControlCapability:NativeTargetCapability
    {
        public override string Id=>"object.light.control";
        public override string Label=>"Control a lamp";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"target.loaded","light.state.current"};
        public override string Description=>"Set transient enabled, intensity and colour on an existing object lamp, or reset all three to saved defaults. Inspect object.lights then object.light and pass its exact stateId. No room save or Undo entry: a program can react to buttons, time or other events without rewriting its document. Works on a held or moving prop and does not claim its motion channels. Commands are serialized: a competing change invalidates an older stateId. A completed command lasts after its calling program stops; explicitly reset or change it. Source definition changes/removal, Undo of that definition, temporary-room boundaries, room reload/workspace change and disabling the editor reset controls. Ordinary pose/paint edits and renderer recreation preserve them. Tracking/focus suspension hides emission but retains controls until recovery. Copies/prototypes use saved defaults only. Native active state is not proof of illumination or headset performance. This command neither creates a lamp nor changes its attachment, shadow allocation, collision, visual opacity, weather, world time or camera sharing.";
        static JObject Variant(string operation){
            var fields=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["light"]=ObjectLightCapability.Name(),["stateId"]=Text("^[a-f0-9]{32}$",32)};
            if(operation=="set"){fields["enabled"]=ObjectLightCapability.Bool();fields["intensity"]=Number(0,4);fields["color"]=Text("^#[0-9A-Fa-f]{6}$",7);}
            fields["operation"]["x-static"]=true;var schema=CurrentInputs(Object(fields),"object.light","stateId",new JObject{["target"]="target",["light"]="light"});schema["x-features"]=new JArray(ObjectLightCapability.Feature);schema["title"]=operation=="set"?"Set live lamp controls":"Restore saved lamp controls";return schema;
        }
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("set"),Variant("reset"))};
        public override JObject OutputSchema=>ViewSchema();
        public override JObject Example=>new(){["operation"]="set",["target"]=new string('0',32),["light"]="Lamp",["stateId"]=new string('0',32),["enabled"]=true,["intensity"]=.5,["color"]="#FFE0C0"};
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room unavailable";return c.Editor&&c.Editor.CanControlLight((string)a["target"],(string)a["light"],(string)a["stateId"],out error);}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error)||!c.Editor.ControlLight((string)a["target"],(string)a["light"],(string)a["stateId"],a,out error))return false;operation=new CompletedCapability(c.Editor.ObserveLight((string)a["target"],(string)a["light"]));return true;}
        static JObject ViewSchema()=>Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["light"]=ObjectLightCapability.Name(),["stateId"]=Text("^[a-f0-9]{32}$",32),["settings"]=ObjectLightCapability.SavedSchema(),["effective"]=Object(new JObject{["enabled"]=ObjectLightCapability.Bool(),["intensity"]=Number(0,4),["color"]=Text("^#[0-9A-Fa-f]{6}$",7),["overridden"]=ObjectLightCapability.Bool()}),["active"]=ObjectLightCapability.Bool(),["reason"]=Text(null,160)});
        internal static BehaviourCatalog.FactDefinition Fact(){var type=OutputType(ViewSchema());return new("object.light",type,"Object lamp details","Saved component, effective controls, exact runtime stateId and native emission status. Unavailable for missing lamps. Read-only: does not refresh, allocate, start a light or change a revision. A renderer can be inactive while its controls are retained. Does not establish rendered pixels or headset performance.",Object(new JObject{["target"]=DrawingData.Target(),["light"]=ObjectLightCapability.Name()}),new JObject{["target"]=new string('0',32),["light"]="Lamp"},(c,a)=>c.Editor&&c.Editor.ObserveLight((string)a["target"],(string)a["light"]) is JObject value?ProgramValue.Literal(value,type):null,features:new[]{ObjectLightCapability.Feature});}
    }
}
