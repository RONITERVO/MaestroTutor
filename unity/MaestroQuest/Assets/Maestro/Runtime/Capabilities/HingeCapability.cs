// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class HingeCapability:CapabilityModule
    {
        internal const string Feature="physicalHinges.v1";
        public override string Id=>"object.hinge.edit";
        public override string Label=>"Configure a physical hinge";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Save/remove one physical hinge on a created object, or explicitly align it to its connected object's current pose at a selected angle. Both local frames use +X as the axis and +Y as zero angle. Moving member needs solid/bouncy physics; connected member may be fixed. Configure never moves to align or starts room physics. Misaligned, missing or animation-owned members suspend the constraint and freeze its moving body; inspect object.hinge.state. Explicit align moves only the target and records one live-pose Undo. Connected-body collisions are disabled; other world collisions remain. Passive, spring return and motor are mutually exclusive. Limits/target angles persist relative to the saved frames through reloads. Disabling/removing releases the connection. Pausing room physics/focus stops motion; explicit Start physics re-admits valid components, including configured motors, without replaying old velocity. Sixteen hinges per room, one per object, no cycles. Whole-object scaling can invalidate alignment. Stable IDs never rebind by name. This is not snapping, cloth, breakable joints or a separate toy system.";
        static JObject Frame()=>Object(new JObject{["position"]=DrawingData.Point(),["rotation"]=Vector(true)});
        internal static JObject DefinitionSchema()=>Object(new JObject{["enabled"]=new JObject{["type"]="boolean"},["ownerFrame"]=Frame(),["connectedFrame"]=Frame(),["limits"]=Object(new JObject{["enabled"]=new JObject{["type"]="boolean"},["minimum"]=Number(-170,170),["maximum"]=Number(-170,170)}),["drive"]=Object(new JObject{["mode"]=Choice("passive","spring","motor"),["target"]=Number(-170,170),["spring"]=Number(0,100),["damper"]=Number(0,20),["speed"]=Number(-360,360),["force"]=Number(0,20)})});
        static JObject Variant(string op){var p=new JObject{["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;
            if(op!="remove")p["connected"]=DrawingData.Target();if(op=="configure")p["definition"]=DefinitionSchema();if(op=="align")p["angle"]=Number(-170,170);
            var s=CurrentInputs(Object(p),"object.hinge","revision",new JObject{["target"]="target"});if(op=="configure")s["format"]="hingeConfiguration";s["title"]=op;s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("align"),Variant("remove"))};
        internal static JObject Definition(RoomHinge h){var j=JObject.Parse(JsonUtility.ToJson(h));j.Remove("version");j.Remove("connected");return j;}
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["connected"]=new string('1',32),["definition"]=Definition(new RoomHinge())};
        static RoomHinge Read(JObject a){if((string)a["operation"]!="configure")return null;var h=JsonUtility.FromJson<RoomHinge>(a["definition"].ToString());h.connected=(string)a["connected"];return h;}
        public override bool Validate(JObject a,out string error){error=null;return (string)a["operation"]!="configure"||Read(a).Validate((string)a["target"],out error);}
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>a["connected"]==null?base.Claims(a):new[]{new BehaviourCatalog.Claim((string)a["target"],"wholeTarget"),new BehaviourCatalog.Claim((string)a["connected"],"wholeTarget")};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareHinge((string)a["target"],(int)a["revision"],(string)a["operation"],(string)a["connected"],Read(a),(float?)a["angle"]??0,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation op,out string error){op=null;if(!CanRun(c,a,out error)||!c.Editor.EditHinge((string)a["target"],(int)a["revision"],(string)a["operation"],(string)a["connected"],Read(a),(float?)a["angle"]??0,out error))return false;op=new CompletedCapability();return true;}
        static JObject SettingsSchema(){var s=DefinitionSchema();((JObject)s["properties"]).Remove("ownerFrame");((JObject)s["properties"]).Remove("connectedFrame");s["required"]=new JArray("enabled","limits","drive");return s;}
        static JObject Settings(RoomHinge h){var j=Definition(h);j.Remove("ownerFrame");j.Remove("connectedFrame");return j;}
        static JObject FactSchema()=>Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["connected"]=Text(null,32),["definition"]=SettingsSchema()});
        public static BehaviourCatalog.FactDefinition Fact()=>new("object.hinge",OutputType(FactSchema()),"Saved physical hinge","Configuration and revision; configured=false returns inert defaults. connected is an exact stable ID or empty, never a name. Read each local frame with object.hinge.frame. Settings use the same schema for user/agent editing.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{
            var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var h=d.hinges?.FirstOrDefault();return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=h!=null,["connected"]=h?.connected??"",["definition"]=Settings(h??new RoomHinge{enabled=false})});},features:new[]{Feature});
        public static BehaviourCatalog.FactDefinition FrameFact()=>new("object.hinge.frame",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["frame"]=Frame()})),"Saved hinge frame","Read one local frame within the bounded program-value budget. Use the same object revision for both sides when assembling a configuration; reads never align or start physics.",Object(new JObject{["target"]=DrawingData.Target(),["side"]=Choice("owner","connected")}),new JObject{["target"]=new string('0',32),["side"]="owner"},(c,a)=>{
            var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var h=d.hinges?.FirstOrDefault();var frame=h==null?new HingeFrame():(string)a["side"]=="owner"?h.ownerFrame:h.connectedFrame;
            return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=h!=null,["frame"]=JObject.Parse(JsonUtility.ToJson(frame))});},features:new[]{Feature});
        public static BehaviourCatalog.FactDefinition State()=>new("object.hinge.state",ProgramDataType.Read(JObject.Parse("{\"record\":{\"phase\":\"text\",\"active\":\"boolean\",\"angle\":\"number\",\"error\":\"text\"}}")),"Live hinge state","Observed native state, separate from the saved definition. active means an admitted PhysX constraint. Angle is signed degrees around the saved connected frame's +X axis, wrapped to [-180,180]; it is not a revolution counter. Missing/paused/owned/misaligned states do not imply motion completed.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var item=c.Editor?c.Editor.Find((string)a["target"]):null;if(!item)return null;var v=item.GetComponent<RoomHingeView>();return ProgramValue.Literal(new JObject{["phase"]=v?v.Phase:"absent",["active"]=v&&v.Active,["angle"]=v?v.Angle:0,["error"]=v?v.Error:""});},features:new[]{Feature});
    }
}
