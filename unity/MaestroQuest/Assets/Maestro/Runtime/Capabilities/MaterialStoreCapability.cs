// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class MaterialStoreCapability:CapabilityModule {
        internal const string Feature="materialStores.v1";
        public override string Id=>"object.material.edit";
        public override string Label=>"Configure a measured material store";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure or remove one measured material store on a created object. Up to 16 stores per room. Capacity and contents are logical local litres, independent of visual size and rigid-body mass. Configuration is an explicit authoring edit and can add or discard material. It does not change the object's geometry, colour, collider, mass or motion, and supplies no fluid/granular solver. A packed ball uses the same component. Copy/prototypes explicitly duplicate contents; Undo, temporary rooms and archives retain them. No bare-hand scoop, automatic pour, compaction density, melting or mixing is inferred. Use the current object.material revision and exclusive ownership.";
        internal static JObject DefinitionSchema(){var s=Object(new JObject{["capacityLitres"]=Number(.001,8000),["amountLitres"]=Number(0,8000),["material"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["color"]=Object(new JObject{["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)})});s["format"]="materialStore";return s;}
        internal static JObject SavedSchema(){var s=DefinitionSchema();((JObject)s["properties"])["version"]=Number(1,1,true);((JArray)s["required"]).Add("version");s["x-features"]=new JArray(Feature);return s;}
        internal static JObject Definition(RoomMaterialStore s){var j=JObject.Parse(JsonUtility.ToJson(s));j.Remove("version");return j;}
        static JObject Variant(string operation){var p=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(operation=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.material","revision",new JObject{["target"]="target"});s["title"]=operation=="configure"?"Configure / fill":"Remove material store";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new RoomMaterialStore())};
        static RoomMaterialStore Read(JObject a)=>(string)a["operation"]=="remove"?null:JsonUtility.FromJson<RoomMaterialStore>(a["definition"].ToString());
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareMaterialStore((string)a["target"],(int)a["revision"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditMaterialStore((string)a["target"],(int)a["revision"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.material",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=DefinitionSchema()})),"Measured carried material","Saved logical local litres, capacity, material identity and colour. Quantity stays unchanged on resize and does not set mass or infer geometry. Missing stores return inert editable defaults with configured=false. Reading never creates material or starts simulation.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var s=d.materialStores?.FirstOrDefault();return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=s!=null,["definition"]=Definition(s??new RoomMaterialStore())});},features:new[]{Feature});
    }
    internal sealed class PackMaterialCapability:CapabilityModule {
        internal const string Feature="materialPacking.v1";
        public override string Id=>"object.material.pack";
        public override string Label=>"Pack surface material into a ball";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"source.created","source.unheld","source.revision.current","storage.writable","room.capacity"};
        public override string Description=>"Remove up to amountLitres from a local disk on a fixed height surface and create one editable, grabbable solid ball at the requested room position. Smooth footprint weights bound available volume; a grid-missing or less-than-0.001-litre footprint refuses. Stored amount is the measured actual loss, never more than requested. The new sphere recipe and sphere collider have matching diameter derived from that local volume; material label and colour are copied into its measured store. Rigid mass is explicitly supplied, not inferred density. One atomic save/Undo updates the patch and creates/removes the ball; failed saves or capacity limits leave neither partial removal nor partial object. Returns exact objectId and amountLitres. Ordinary grip, hold, launch, collision, animation and copy apply. Consumes one program creation allowance. Both geometry and quantity remain editable: resizing or deforming later does not rewrite the store. Does not start physics, move a hand/shovel, check reach, auto-catch or simulate grains. Stop cannot retract a completed edit; receipt replay cannot create a second ball.";
        public override JObject InputSchema{get{var endpoint=CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["centre"]=Object(new JObject{["x"]=Number(-2,2),["z"]=Number(-2,2)}),["radius"]=Number(.005,2)}),"object.field","revision",new JObject{["target"]="target"});var s=Object(new JObject{["source"]=endpoint,["amountLitres"]=Number(.001,20),["name"]=Text("^.{1,80}$",80),["position"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),["mass"]=Number(.05,20)});s["x-features"]=new JArray(MaterialStoreCapability.Feature,HeightFieldCapability.Feature,Feature,"actionResults.v1");return s;}}
        public override JObject OutputSchema=>Object(new JObject{["objectId"]=Resource(Text("^[a-f0-9]{32}$",32)),["amountLitres"]=Number(.001,20)});
        public override JObject Example=>new(){["source"]=new JObject{["target"]=new string('0',32),["revision"]=1,["centre"]=new JObject{["x"]=0,["z"]=0},["radius"]=.2},["amountLitres"]=.5,["name"]="Snowball",["position"]=new JObject{["x"]=.3,["y"]=1,["z"]=.7},["mass"]=.15};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim((string)a["source"]["target"],"wholeTarget")};
        internal override int MaximumCreatedObjects(JObject a)=>1;
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareMaterialPack(a,out _,out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.PackMaterial(a,out var id,out var amount,out error))return false;operation=new CompletedCapability(new JObject{["objectId"]=id,["amountLitres"]=amount});return true;}
    }
}
