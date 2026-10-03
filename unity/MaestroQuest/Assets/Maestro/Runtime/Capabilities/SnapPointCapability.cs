// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class SnapPointCapability:CapabilityModule {
        internal const string Feature="snapPoints.v1";
        public override string Id=>"object.snapPoint.edit";
        public override string Label=>"Edit a snap point";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure or remove one stable named alignment point on a created object's root, including an import. Points with the same exact family may be aligned by object.layout.snap. Frames coincide: their +Y axes and forward directions match, with optional turn about the destination frame +Y. These are coordinate frames, not outward face normals. Coordinates use object-local metres and scale with the object. At most 64 per object and 256 per room. Names are display text; IDs and family are exact, case-sensitive identifiers. Points survive copy, capture, Undo and workspace export. Editing a point never moves objects or changes a previously accepted physical connection. No occupancy lock, collision check, automatic nearest-point selection or attachment to animated parts is implied.";
        internal static JObject PointId()=>Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32);
        internal static JObject DefinitionSchema(){var schema=Object(new JObject{["name"]=Text("^.*\\S.*$",64),["family"]=PointId(),["frame"]=ConnectionCapability.Frame()});schema["format"]="snapPointDefinition";return schema;}
        internal static JObject SavedSchema(){var s=DefinitionSchema();((JObject)s["properties"])["id"]=PointId();((JObject)s["properties"])["version"]=Number(1,1,true);((JArray)s["required"]).Add("id");((JArray)s["required"]).Add("version");s["x-features"]=new JArray(Feature);return s;}
        static JObject Variant(string op){var p=new JObject{["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision(),["point"]=PointId()};p["operation"]["x-static"]=true;if(op=="configure")p["definition"]=DefinitionSchema();var schema=CurrentInputs(Object(p),"object.snapPoint","revision",new JObject{["target"]="target",["point"]="point"});schema["title"]=op;schema["x-features"]=new JArray(Feature);return schema;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        internal static JObject Definition(RoomSnapPoint p)=>new(){["name"]=p.name,["family"]=p.family,["frame"]=JObject.Parse(JsonUtility.ToJson(p.frame))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["point"]="Top",["definition"]=Definition(new RoomSnapPoint{name="Top",family="Brick",frame=new ConnectionFrame{position=new Vector3(0,.042f,0)}})};
        static RoomSnapPoint Read(JObject args)=>args["definition"] is JObject d?JsonUtility.FromJson<RoomSnapPoint>(d.ToString()):null;
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareSnapPoint((string)a["target"],(int)a["revision"],(string)a["point"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditSnapPoint((string)a["target"],(int)a["revision"],(string)a["point"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        static RoomObjectData Data(BehaviourCatalog.FactContext c,JObject a)=>c.Editor?c.Editor.Read((string)a["target"]):null;
        internal static BehaviourCatalog.FactDefinition ListFact()=>new("object.snapPoints",OutputType(Object(new JObject{["revision"]=Revision(),["offset"]=Number(0,63,true),["total"]=Number(0,64,true),["ids"]=List(PointId(),0,8)})),"Object snap points","Read stable point IDs eight at a time, with total and current revision. Start offset 0 and advance by eight; an offset past the end returns an empty page. Read each definition with object.snapPoint. Reading does not reserve a slot or move anything.",Object(new JObject{["target"]=DrawingData.Target(),["offset"]=Number(0,63,true)}),new JObject{["target"]=new string('0',32),["offset"]=0},(c,a)=>{var d=Data(c,a);if(d==null||d.IsBuiltIn)return null;var points=d.snapPoints??System.Array.Empty<RoomSnapPoint>();int offset=(int)a["offset"];return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["offset"]=offset,["total"]=points.Length,["ids"]=new JArray(points.Skip(offset).Take(8).Select(p=>p.id))});},features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.snapPoint",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=DefinitionSchema()})),"Saved snap point","One root-local alignment frame and its current object revision. Missing points return configured=false with inert defaults so a new point can be configured. Read object.placement separately for its object's live room-local pose. There is no snap occupancy state; ordinary placement or physics can move pieces afterwards.",Object(new JObject{["target"]=DrawingData.Target(),["point"]=PointId()}),new JObject{["target"]=new string('0',32),["point"]="Top"},(c,a)=>{var d=Data(c,a);if(d==null||d.IsBuiltIn)return null;var p=d.snapPoints?.FirstOrDefault(x=>x.id==(string)a["point"]);return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=p!=null,["definition"]=Definition(p??new RoomSnapPoint{name="New point",family="Default"})});},features:new[]{Feature});
    }
}
