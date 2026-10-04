// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class SculptTipCapability:CapabilityModule {
        internal const string Feature="physicalSculpting.v1";
        public override string Id=>"object.sculptTip.edit";
        public override string Label=>"Configure an object's sculpt tip";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Give a created/imported object one saved sculpt tip on its root (empty part) or a stable recipe part. Local +Z points toward the surface. The enabled tip works only while the user or Maestro holds it and it contacts the accepted top surface within one world centimetre. Loose objects are inert. Radius/height use the receiving field's local metres. Choose raise/lower/level; the same object.field.sculpt operation applies its swept path once, not per frame. Live previews change visible geometry only; release/contact loss publishes matching collision and source with one save/Undo. Physics keeps the accepted surface until then. At most 32 path samples; reaching the limit ends the gesture and requires separating the tool before another. Failed or interrupted saves retain the draft for explicit object.field.resolve retry/discard. Pause/tracking loss never retries automatically. Manual holding has control priority; Maestro-held tools use program priority and cannot take over a user's surface. Only one shared sculpt capture runs, and drawing and sculpt captures cannot overlap. Disable an object's drawing tip before enabling its sculpt tip. Copy/prototypes, saved/temporary rooms and archives retain the exact configuration. This is shape authoring, not conserved material or snow transfer.";
        internal static JObject DefinitionSchema(){var s=Object(new JObject{["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["position"]=DrawingData.Point(),["rotation"]=Vector(true),["mode"]=Choice("raise","lower","level"),["radius"]=Number(.005,2),["height"]=Number(0,.5),["enabled"]=new JObject{["type"]="boolean"}});s["format"]="sculptTip";return s;}
        internal static JObject SavedSchema(){var s=DefinitionSchema();((JObject)s["properties"])["version"]=Number(1,1,true);((JArray)s["required"]).Add("version");s["x-features"]=new JArray(Feature);return s;}
        internal static JObject Definition(SculptTip tip){var j=JObject.Parse(JsonUtility.ToJson(tip));j.Remove("version");return j;}
        static JObject Variant(string op){var p=new JObject{["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(op=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.sculptTip","revision",new JObject{["target"]="target"});s["title"]=op=="configure"?"Configure the sculpt tip":"Remove the sculpt tip";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new SculptTip{position=new Vector3(0,0,.16f)})};
        static SculptTip Read(JObject a)=>(string)a["operation"]=="remove"?null:JsonUtility.FromJson<SculptTip>(a["definition"].ToString());
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareSculptTip((string)a["target"],(int)a["revision"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditSculptTip((string)a["target"],(int)a["revision"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.sculptTip",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=DefinitionSchema()})),"Object sculpt tip","Saved sculpt-tip configuration and current object revision. Missing tips return inert editable defaults, configured=false. Reads never begin a gesture or modify geometry. Use object.field.capture for live/retained gestures.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var tip=d.sculptTips?.FirstOrDefault();return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=tip!=null,["definition"]=Definition(tip??new SculptTip{enabled=false})});},features:new[]{Feature});
    }
}
