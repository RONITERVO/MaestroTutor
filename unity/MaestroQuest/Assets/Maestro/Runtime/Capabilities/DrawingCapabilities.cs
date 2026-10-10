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
    internal static class DrawingData
    {
        internal static JObject Target()=>Resource(Text("^[a-fA-F0-9]{32}$",32));
        internal static JObject Point()=>Object(new JObject {["x"]=Number(-10,10),["y"]=Number(-10,10),["z"]=Number(-10,10)});
        internal static Vector3[] Points(JToken value)=>((JArray)value).Select(p=>new Vector3((float)p["x"],(float)p["y"],(float)p["z"])).ToArray();
        internal static JObject Summary(RoomEditor editor,string target){var data=editor.Read(target);return new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["points"]=data.points.Length,["radius"]=data.radius};}
        internal static JObject SummarySchema()=>Object(new JObject {["target"]=Text("^[a-fA-F0-9]{32}$",32),["revision"]=Revision(),["points"]=Number(2,2048,true),["radius"]=Number(.001,.02)});
        internal static JArray ExamplePoints()=>new(new JObject {["x"]=0,["y"]=0,["z"]=0},new JObject {["x"]=.08,["y"]=.14,["z"]=0},new JObject {["x"]=.16,["y"]=0,["z"]=0});
    }
    internal sealed class CreateDrawingCapability:CapabilityModule
    {
        public override string Id=>"object.create.drawing";
        public override string Label=>"Draw a 3D stroke";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.capacity","drawing.valid","storage.writable"};
        public override string Description=>"Create one editable 3D pencil stroke through the physical pencil's saved creation path. points are ordered object-local metres, within 10 metres of its origin; x/y/z place that origin in room metres, and scale uniformly scales the stroke and radius. Supply 2–64 points per call, radius .001–.02 metres, and RGB colour. Use object.drawing.edit to grow or reshape it up to the native 2048-point stroke limit; all saved strokes together stay within 32768 points. Every intermediate shape must remain visible and valid. Return the exact new objectId only after saving with one Undo, or locally in a temporary room until Keep. Selection, pencil mode and other actors are unchanged. No automatic animation or physics start. Drawings are fixed initially; existing physics controls can change them. Collision uses the existing bounded approximate shape, not a flexible rope or a per-segment surface. Replay cannot create another stroke. No numeric action adapter is required.";
        public override JObject InputSchema=>Object(new JObject {["name"]=Text("^.{0,80}$",80),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25),["scale"]=Number(.1,4),["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1),["radius"]=Number(.001,.02),["points"]=List(DrawingData.Point(),2,64)});
        public override JObject OutputSchema=>Object(new JObject {["objectId"]=DrawingData.Target()});
        public override JObject Example=>new() {["name"]="Pencil arch",["x"]=.3,["y"]=1.3,["z"]=.65,["scale"]=1,["red"]=.2,["green"]=.6,["blue"]=.9,["radius"]=.003,["points"]=DrawingData.ExamplePoints()};
        public override bool Validate(JObject args,out string error){error="Position must be within 25 metres of the room origin";return ObjectCapabilityData.Position(args).sqrMagnitude<=625&&RoomDocument.ValidateDrawing(DrawingData.Points(args["points"]),(float)args["radius"],out error);}
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanCreateDrawing(DrawingData.Points(args["points"]),(float)args["radius"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!context.Editor.CreateDrawing((string)args["name"],ObjectCapabilityData.Position(args),(float)args["scale"],new Color((float)args["red"],(float)args["green"],(float)args["blue"]),(float)args["radius"],DrawingData.Points(args["points"]),out var id,out error))return false;
            operation=new CompletedCapability(new JObject {["objectId"]=id});return true;
        }
    }
    internal sealed class DrawingEditCapability:NativeTargetCapability
    {
        public override string Id=>"object.drawing.edit";
        public override string Label=>"Edit a pencil stroke";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.drawing","target.unheld","source.revision.current","authoring.inactive","storage.writable"};
        public override string Description=>"Edit saved stroke points or thickness at the exact revision read from object.drawing. splice removes deleteCount points at zero-based index then inserts 0–64 points there; index equal to the current count appends, and deleteCount=0 inserts. An empty insertion deletes a range. Use several explicit edits to reach all 2048 native points; each intermediate result must retain at least two points and a visible stroke, with points within 10 local metres. radius changes only local stroke thickness. Position, rotation, scale, colour, physics settings and recorded motion remain; point coordinates use the object's local axes, not room/world coordinates. The visible mesh, selection and approximate collision shape update together. Each successful call saves one Undo, or changes the temporary fork until Keep, and returns the exact new revision/count/radius. Failure preserves geometry and saved state. Reads use object.drawing.points in pages of eight. Held/owned/actively authored targets are refused. It never starts drawing input, playback or physics; unrelated actors continue. Stop after completion does not undo it; receipt replay cannot edit twice.";
        static JObject Variant(string op,string title,JObject fields){var props=new JObject {["operation"]=Choice(op),["target"]=DrawingData.Target(),["revision"]=Revision()};props["operation"]["x-static"]=true;foreach(var p in fields.Properties())props[p.Name]=p.Value.DeepClone();var schema=CurrentInputs(Object(props),"object.drawing","revision",new JObject {["target"]="target"});schema["title"]=title;schema["x-features"]=new JArray("drawingEdits.v1","actionResults.v1");return schema;}
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Drawing edit",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("splice","Insert, replace or remove points",new JObject {["index"]=Number(0,2048,true),["deleteCount"]=Number(0,2048,true),["points"]=List(DrawingData.Point(),0,64)}),Variant("radius","Set stroke thickness",new JObject {["radius"]=Number(.001,.02)}))};
        public override JObject OutputSchema=>DrawingData.SummarySchema();
        public override JObject Example=>new() {["operation"]="radius",["target"]=new string('0',32),["revision"]=1,["radius"]=.005};
        public override bool Validate(JObject args,out string error){error="A splice must change a range, with points within 10 local metres";if((string)args["operation"]=="splice"&&((int)args["deleteCount"]==0&&((JArray)args["points"]).Count==0||DrawingData.Points(args["points"]).Any(p=>p.sqrMagnitude>100)))return false;error=null;return true;}
        static float? Radius(JObject args)=>(string)args["operation"]=="radius"?(float)args["radius"]:null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out _,out error)&&context.Editor.PrepareDrawingEdit((string)args["target"],(int)args["revision"],(int?)args["index"]??0,(int?)args["deleteCount"]??0,args["points"]==null?null:DrawingData.Points(args["points"]),Radius(args),out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(context,args,out error)||!context.Editor.EditDrawing((string)args["target"],(int)args["revision"],(int?)args["index"]??0,(int?)args["deleteCount"]??0,args["points"]==null?null:DrawingData.Points(args["points"]),Radius(args),out error))return false;operation=new CompletedCapability(DrawingData.Summary(context.Editor,(string)args["target"]));return true;}
        public static BehaviourCatalog.FactDefinition SummaryFact()=>new("object.drawing",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"points\":\"number\",\"radius\":\"number\"}}")),"Saved pencil stroke","Current saved stroke revision, count and object-local radius; reads the temporary fork when active. Does not sample the live pencil or alter geometry. Missing/non-drawing targets are unavailable. Use the exact revision for edits and paged point reads. Scanned ink layers use object.surfaces instead.",Object(new JObject {["target"]=DrawingData.Target()}),new JObject {["target"]=new string('0',32)},(context,args)=>context.Editor&&context.Editor.Read((string)args["target"])?.kind==RoomObjectKind.Drawing&&!ScanDrawingAnchor.Has(context.Editor.Read((string)args["target"]))?ProgramValue.Literal(DrawingData.Summary(context.Editor,(string)args["target"])):null);
        public static BehaviourCatalog.FactDefinition PointsFact()=>new("object.drawing.points",ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"offset\":\"number\",\"total\":\"number\",\"points\":{\"list\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}}}}}")),"Saved stroke point page","Read at most eight exact object-local points at offset, with the current saved revision. Total is the complete stroke count. Offset equal to total returns an empty page; a later offset, stale revision or missing/non-drawing target is unavailable. Scanned ink layers use object.surfaces instead. No simplification, coordinate conversion or ownership is implied.",Object(new JObject {["target"]=DrawingData.Target(),["revision"]=Revision(),["offset"]=Number(0,2048,true)}),new JObject {["target"]=new string('0',32),["revision"]=1,["offset"]=0},(context,args)=>{
            string target=(string)args["target"];if(!context.Editor||context.Editor.ObjectRevision(target)!=(int)args["revision"])return null;var data=context.Editor.Read(target);int offset=(int)args["offset"];if(data?.kind!=RoomObjectKind.Drawing||ScanDrawingAnchor.Has(data)||offset>data.points.Length)return null;
            return ProgramValue.Literal(new JObject {["revision"]=(int)args["revision"],["offset"]=offset,["total"]=data.points.Length,["points"]=new JArray(data.points.Skip(offset).Take(8).Select(p=>new JObject {["x"]=p.x,["y"]=p.y,["z"]=p.z}))},BehaviourCatalog.Fact("object.drawing.points").Type);
        });
    }
}
