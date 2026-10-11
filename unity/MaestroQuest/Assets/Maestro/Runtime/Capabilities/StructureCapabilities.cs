// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal abstract class StructureCapability:CapabilityModule
    {
        internal const string Feature="structures.v1";
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        protected static JObject IdSchema(bool optional=false)=>Text(optional?"^(|[a-f0-9]{32})$":"^[a-f0-9]{32}$",32);
        internal static JObject Member()=>Resource(Text("^[a-fA-F0-9]{32}$",32));
        protected static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        protected static JObject Receipt(RoomEditor editor,string id)=>new() {["structureId"]=id,["revision"]=editor.StructureRevision(id),["temporary"]=editor.TemporaryRoom};
        protected static bool Targets(CapabilityContext context,JObject args,JObject schema,out string error){
            error=null;foreach(string id in CapabilityArguments.Resources(args,schema))if(!context.Target(new JObject {["target"]=id},out _,out error))return false;return true;
        }
    }
    internal sealed class StructureSaveCapability:StructureCapability
    {
        internal override IEnumerable<string> NativeEntities(JObject args)=>CapabilityArguments.Resources(args,InputSchema);
        public override string Id=>"structure.save";
        public override string Label=>"Save a structure";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","structure.revision.current","storage.writable"};
        public override string Description=>"Save a persistent named structure of 1–16 independent user-created pieces. Use an empty id and revision 0 to create; otherwise use the exact structure ID and latest revision. source.kind=capture records all named members' current room-local poses together; definition supplies explicit baseline slots and tolerances. Saving changes membership/baselines only and never moves objects. Members must be distinct, released, available and not being authored. A piece can belong to more than one structure; each has its own explicit baseline. Saves once with one Undo, including temporary-room semantics. Slots retain exact object IDs after deletion; reset refuses any missing member. To replace a member, explicitly save its slot with the replacement ID and intended baseline. Names never identify objects. This does not snap/connect parts, recreate deleted objects or attach structures to the room scan. Forget removes only metadata. Saved programs keep exact structure/member IDs and can read structure.state before choosing a reaction.";
        static JObject SourceFields()=>new() {["name"]=Text("^.{0,80}$",80),["positionTolerance"]=Number(.005,1),["rotationTolerance"]=Number(.5,90),["scaleTolerance"]=Number(.005,1)};
        public override JObject InputSchema {get{
            var captureFields=SourceFields();captureFields["kind"]=Choice("capture");captureFields["members"]=List(Object(new JObject {["slot"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),["target"]=Member()}),1,16);
            var capture=Object(captureFields);capture["title"]="Capture current arrangement";capture["format"]="structureSource";capture["properties"]["kind"]["x-static"]=true;
            var definitionFields=SourceFields();definitionFields["kind"]=Choice("definition");definitionFields["slots"]=List(Object(new JObject {["slot"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),["placement"]=Object(new JObject {["target"]=Member(),["position"]=LayoutCapability.PositionSchema(),["rotation"]=Vector(true),["scale"]=Number(.1,4)})}),1,16);
            var definition=Object(definitionFields);definition["title"]="Edit baseline and membership";definition["format"]="structureSource";definition["properties"]["kind"]["x-static"]=true;
            return Featured(Object(new JObject {["id"]=IdSchema(true),["revision"]=Revision(true),["source"]=new JObject {["type"]="object",["oneOf"]=new JArray(capture,definition),["x-discriminators"]=new JArray("kind")}}));
        }}
        public override JObject OutputSchema=>Object(new JObject {["structureId"]=IdSchema(),["revision"]=Revision(),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["id"]="",["revision"]=0,["source"]=new JObject {["kind"]="capture",["name"]="Castle",["positionTolerance"]=.05,["rotationTolerance"]=15,["scaleTolerance"]=.05,["members"]=new JArray(new JObject {["slot"]="brick_1",["target"]=new string('0',32)})}};
        internal static bool ValidSource(JObject source,out string error){
            error="Use distinct slot keys and object IDs with valid baseline poses";
            var entries=(JArray)source[(string)source["kind"]=="capture"?"members":"slots"];
            if(entries.Select(x=>(string)x["slot"]).Distinct().Count()!=entries.Count)return false;
            if((string)source["kind"]=="capture")return entries.Select(x=>(string)x["target"]).Distinct().Count()==entries.Count;
            var definition=JsonUtility.FromJson<RoomStructure>(source.ToString());definition.id=new string('0',32);definition.version=1;return definition.Validate(out error);
        }
        RoomStructure Definition(CapabilityContext context,JObject args){
            var source=(JObject)args["source"];var value=JsonUtility.FromJson<RoomStructure>(source.ToString());value.version=1;value.id=string.IsNullOrEmpty((string)args["id"])?Guid.NewGuid().ToString("N"):(string)args["id"];
            if((string)source["kind"]=="capture")value.slots=((JArray)source["members"]).Select(x=>new StructureSlot {slot=(string)x["slot"],placement=context.Editor.Frame.Placement((string)x["target"],context.Editor.Find((string)x["target"]).transform)}).ToArray();
            return value;
        }
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>CapabilityArguments.Resources(args,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            error="A new structure needs revision 0; an existing structure needs its latest revision";
            if(string.IsNullOrEmpty((string)args["id"])!=((int)args["revision"]==0))return false;
            return Targets(context,args,InputSchema,out error)&&context.Editor.CanSaveStructure(Definition(context,args),(int)args["revision"],out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;var definition=Definition(context,args);
            if(!context.Editor.SaveStructure(definition,(int)args["revision"],out error))return false;operation=new CompletedCapability(Receipt(context.Editor,definition.id));return true;
        }
    }
    internal sealed class StructureResetCapability:StructureCapability
    {
        internal override IEnumerable<string> NativeEntities(JObject args)=>CapabilityArguments.Resources(args,InputSchema);
        public override string Id=>"structure.reset";
        public override string Label=>"Reset a structure";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"structure.revision.current","target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override string Description=>"Reset every piece to a saved structure baseline with one save and one live-pose Undo. Supply the exact structure ID/revision and all member IDs as an explicit ownership/authorization guard; their order is unimportant. Any missing, held, authored or conflicting member refuses the entire reset. Reset zeroes member velocities but preserves current geometry, paint, collision and animation definitions; it neither starts physics nor guarantees stability. Definitions remain unchanged. Missing pieces are never recreated or substituted: explicitly replace/remove the slot with structure.save, then retry. Temporary edits stay in the fork; completed receipt replay cannot reset twice.";
        public override JObject InputSchema=>Featured(Object(new JObject {["id"]=IdSchema(),["revision"]=Revision(),["members"]=List(Member(),1,16)}));
        public override JObject OutputSchema=>Object(new JObject {["count"]=Number(1,16,true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["id"]=new string('1',32),["revision"]=1,["members"]=new JArray(new string('0',32))};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>((JArray)args["members"]).Values<string>().Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Targets(context,args,InputSchema,out error)&&context.Editor.CanResetStructure((string)args["id"],(int)args["revision"],((JArray)args["members"]).Values<string>().ToArray(),out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error)||!context.Editor.ResetStructure((string)args["id"],(int)args["revision"],((JArray)args["members"]).Values<string>().ToArray(),out error))return false;
            operation=new CompletedCapability(new JObject {["count"]=((JArray)args["members"]).Count,["temporary"]=context.Editor.TemporaryRoom});return true;
        }
    }
    internal sealed class StructureForgetCapability:StructureCapability
    {
        public override string Id=>"structure.forget";
        public override string Label=>"Forget a structure";
        public override string Description=>"Remove only the exact structure's saved membership and baseline after checking its current revision. Objects remain unchanged. One save/Undo; temporary edits stay in the fork. Programs using this structure will report it missing until an explicit definition is restored. No deleted object is recreated.";
        public override JObject InputSchema=>Featured(Object(new JObject {["id"]=IdSchema(),["revision"]=Revision()}));
        public override JObject Example=>new() {["id"]=new string('1',32),["revision"]=1};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanForgetStructure((string)args["id"],(int)args["revision"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!context.Editor.ForgetStructure((string)args["id"],(int)args["revision"],out error))return false;operation=new CompletedCapability();return true;}
    }
}
