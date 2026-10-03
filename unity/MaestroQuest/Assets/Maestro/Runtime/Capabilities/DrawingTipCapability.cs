// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class DrawingTipCapability:CapabilityModule
    {
        internal const string Feature="drawingTips.v1";
        public override string Id=>"object.drawingTip.edit";
        public override string Label=>"Configure an object's drawing tip";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Give any created/imported object one saved drawing tip, or remove it. Empty part means its root; otherwise use a stable recipe part. Local +Z points toward the drawing patch. Ink colour and radius are independent of object paint and tray pencil preferences. Radius uses the receiving patch's local metres. The enabled tip draws only while a user or Maestro holds the tool and its tip is within one world centimetre of an enabled flat patch. Loose tools do not draw. No curved-mesh, skin or scanned-wall painting. One room capture at a time; loss of contact ends one saved stroke/Undo. After interruption or a busy/failed capture, separate the tip before starting again. Failed saves retain the draft for explicit retry/discard. Manual holding uses control priority; Maestro holding uses program priority and cannot interrupt a user. Configuration does not itself create ink or move objects. Copy, Undo, temporary rooms and portable workspaces retain the same tip data.";
        internal static JObject DefinitionSchema()=>Object(new JObject {["part"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["position"]=DrawingData.Point(),["rotation"]=Vector(true),["color"]=Object(new JObject {["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)}),["radius"]=Number(.001,.02),["enabled"]=new JObject {["type"]="boolean"}});
        static JObject Variant(string op){var p=new JObject {["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(op=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.drawingTip","revision",new JObject {["target"]="target"});s["title"]=op=="configure"?"Configure the drawing tip":"Remove the drawing tip";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new DrawingTip {position=new Vector3(0,0,.065f)})};
        internal static JObject Definition(DrawingTip tip){var j=JObject.Parse(JsonUtility.ToJson(tip));j.Remove("version");return j;}
        static DrawingTip Read(JObject a)=>(string)a["operation"]=="remove"?null:JsonUtility.FromJson<DrawingTip>(a["definition"].ToString());
        public override bool Validate(JObject a,out string error){var d=Read(a);error="Use a normalized drawing-tip rotation within ten metres of its anchor";if(d!=null&&(!MotionFrame.ValidRotation(d.rotation)||d.position.sqrMagnitude>100))return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareDrawingTip((string)a["target"],(int)a["revision"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditDrawingTip((string)a["target"],(int)a["revision"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        public static BehaviourCatalog.FactDefinition Fact()=>new("object.drawingTip",ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"configured\":\"boolean\",\"definition\":{\"record\":{\"part\":\"text\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}},\"color\":{\"record\":{\"r\":\"number\",\"g\":\"number\",\"b\":\"number\",\"a\":\"number\"}},\"radius\":\"number\",\"enabled\":\"boolean\"}}}}")),"Object drawing tip","Saved tip configuration and current object revision. configured=false returns inert default fields for editing; it does not add a component. Reads do not draw. Use object.drawing.capture for the shared active/retained capture.",Object(new JObject {["target"]=DrawingData.Target()}),new JObject {["target"]=new string('0',32)},(c,a)=>{var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var tip=d.drawingTips?.FirstOrDefault();return ProgramValue.Literal(new JObject {["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=tip!=null,["definition"]=Definition(tip??new DrawingTip {enabled=false})});},features:new[]{Feature});
    }
}
