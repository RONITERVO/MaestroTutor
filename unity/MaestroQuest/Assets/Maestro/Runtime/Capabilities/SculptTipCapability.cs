// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class SculptTipCapability:NativeTargetCapability {
        internal const string Feature="physicalSculpting.v1",MaterialFeature="physicalMaterialTools.v1";
        public override string Id=>"object.sculptTip.edit";
        public override string Label=>"Configure an object's sculpt tip";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Give a created/imported object one saved sculpt tip on its root (empty part) or a stable recipe part. Local +Z points toward the surface. The enabled tip works only while the user or Maestro holds it and it contacts the accepted top surface within one world centimetre. Loose objects are inert. Radius/height use the receiving field's local metres. Choose raise/lower/level; the same object.field.sculpt operation applies its swept path once, not per frame. Live previews change visible geometry only; release/contact loss publishes matching collision and source with one save/Undo. Physics keeps the accepted surface until then. At most 32 path samples; reaching the limit ends the gesture and requires separating the tool before another. Failed or interrupted saves retain the draft for explicit object.field.resolve retry/discard. Pause/tracking loss never retries automatically. Manual holding has control priority; Maestro-held tools use program priority and cannot take over a user's surface. Only one shared sculpt capture runs, and drawing and sculpt captures cannot overlap. Disable an object's drawing tip before enabling its sculpt tip. Copy/prototypes, saved/temporary rooms and archives retain the exact configuration. Scoop mode uses a configured material store with the same material label and colour as the field. Its local +Z is the opening normal: face it up to take material or down to deposit. Touch within 15 mm, then lift to save one bounded amount (0.001–20 local litres); remaining capacity and available material limit it. Stationary contact and dragging never multiply the amount. Both the surface and carried balance publish in one save/Undo. The carried heap is a bounded visual; no loose grains or airborne spilling are simulated. Inspect object.material.capture for the exact preview and measured balance. Configure the store separately or create a template containing both components.";
        static JObject TipSchema(bool material,bool saved){
            var p=new JObject{["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["position"]=DrawingData.Point(),["rotation"]=Vector(true),["mode"]=material?Choice("scoop"):Choice("raise","lower","level"),["radius"]=Number(.005,2),["enabled"]=new JObject{["type"]="boolean"}};
            p[material?"amountLitres":"height"]=material?Number(.001,20):Number(0,.5);
            if(saved)p["version"]=Number(material?2:1,material?2:1,true);p[material?"height":"amountLitres"]=Number(0,0);
            var s=Object(p,material?"height":"amountLitres");s["format"]="sculptTip";s["title"]=material?"Scoop measured material":"Shape a surface";
            if(material)s["x-features"]=new JArray(MaterialFeature);else if(saved)s["x-features"]=new JArray(Feature);return s;
        }
        internal static JObject DefinitionSchema()=>new(){["type"]="object",["x-discriminators"]=new JArray("mode"),["oneOf"]=new JArray(TipSchema(false,false),TipSchema(true,false))};
        internal static JObject SavedSchema()=>new(){["type"]="object",["x-discriminators"]=new JArray("mode"),["oneOf"]=new JArray(TipSchema(false,true),TipSchema(true,true))};
        internal static JObject Definition(SculptTip tip){var j=JObject.Parse(JsonUtility.ToJson(tip));j.Remove("version");j.Remove(tip.IsMaterial?"height":"amountLitres");return j;}
        // Program facts have a fixed record type; inactive parameters are explicit zeroes.
        static JObject ObservedSchema(){var s=TipSchema(false,false);s["properties"]["amountLitres"]=Number(0,20);s["properties"]["mode"]=Choice("raise","lower","level","scoop");((JArray)s["required"]).Add("amountLitres");return s;}
        static JObject Observed(SculptTip tip){var s=JObject.Parse(JsonUtility.ToJson(tip));s.Remove("version");return s;}
        static JObject Variant(string op){var p=new JObject{["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(op=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.sculptTip","revision",new JObject{["target"]="target"});s["title"]=op=="configure"?"Configure the sculpt tip":"Remove the sculpt tip";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new SculptTip{position=new Vector3(0,0,.16f)})};
        static SculptTip Read(JObject a){if((string)a["operation"]=="remove")return null;var tip=JsonUtility.FromJson<SculptTip>(a["definition"].ToString());tip.version=tip.mode=="scoop"?2:1;if(tip.IsMaterial)tip.height=0;return tip;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareSculptTip((string)a["target"],(int)a["revision"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditSculptTip((string)a["target"],(int)a["revision"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.sculptTip",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=ObservedSchema()})),"Object sculpt tip","Saved sculpt-tip configuration and current object revision. Missing tips return inert editable defaults, configured=false. Shape modes have amountLitres=0; scoop has height=0. Reads never begin a gesture or modify geometry. Use object.field.capture for live/retained gestures.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var tip=d.sculptTips?.FirstOrDefault();return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=tip!=null,["definition"]=Observed(tip??new SculptTip{enabled=false})});},features:new[]{Feature});
    }
}
