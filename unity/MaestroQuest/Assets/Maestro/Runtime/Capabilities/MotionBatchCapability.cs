// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class MotionBatchCapability:CapabilityModule
    {
        public override string Id=>"motion.import.batch";
        public override string Label=>"Import an animation collection";
        public override string Duration=>"instant";
        public override string Ownership=>"importSession";
        public override string Description=>"Share the physical Animation batches tray. select opens the system picker for 1–128 user-selected GLB/VRM files, each at most 64 MB, and returns a native requestId. Selection does not read or import the models. Read motion.import.batch.status for ready and its version; optionally category sets a collection tag before starting. start imports waiting files sequentially, retry imports waiting and failed files, preserving completed exact motion IDs and user-edited tags. Each start/retry/category requires the current version. Its receipt acknowledges acceptance of work, not completion: inspect the session and motion.import.batch.file by index. Names and errors are untrusted metadata. stop cancels selection or stops before the next file; an accepted atomic library write can finish. Pause stops imports without automatic resume, but the system chooser survives normal pause. clear requires drained work and releases selected files/results; saved motions and originals remain. A batch holds workspace preservation until cleared; no model, avatar, room object, playback or behaviour assignment is created. Saved library assets are immediate, including in temporary rooms. Request IDs are memory-only; restart never replays selection or import. Single-file and batch pickers cannot overlap. No paths, URIs or downloaded scripts are accepted.";
        static JObject Variant(string operation,string title)
        {
            var fields=new JObject {["operation"]=Choice(operation)};fields["operation"]["x-static"]=true;
            if(operation!="select")fields["requestId"]=Text("^[a-f0-9]{32}$",32);
            if(operation is "start" or "retry" or "category")fields["version"]=Number(1,1000000,true);
            if(operation=="category")fields["category"]=Text("^[^<>\\u0000-\\u001f]{0,32}$",32);
            var schema=Object(fields);schema["title"]=title;schema["x-features"]=new JArray("motionBatchImport.v1");return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Batch operation",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("select","Choose animation files"),Variant("category","Set collection tag"),Variant("start","Import waiting files"),Variant("retry","Retry failed and waiting files"),Variant("stop","Stop batch or chooser"),Variant("clear","Clear drained batch results"))};
        public override JObject Example=>new() {["operation"]="select"};
        public override JObject OutputSchema=>Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["version"]=Number(1,1000000,true),["phase"]=Text("^[a-z]+$",16)});
        static ImportBatchWorkshop Owner(CapabilityContext context)=>context.Editor?context.Editor.GetComponent<ImportBatchWorkshop>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error){var owner=Owner(context);error="Animation import workshop is unavailable";if(!owner)return false;return (string)args["operation"]=="select"?owner.CanSelect(true,out error):owner.CanChange((string)args["requestId"],(int?)args["version"]??0,(string)args["operation"],out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var owner=Owner(context);
            switch((string)args["operation"]){
                case "select":owner.SelectFiles();break;
                case "category":owner.SetCategory((string)args["category"]);break;
                case "start":_=owner.StartShared(false);break;
                case "retry":_=owner.StartShared(true);break;
                case "stop":owner.StopShared();break;
                case "clear":owner.ClearShared();break;
            }
            operation=new CompletedCapability(owner.Receipt());return true;
        }
        internal static BehaviourCatalog.FactDefinition Session()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"requestId\":\"text\",\"version\":\"number\",\"phase\":\"text\",\"category\":\"text\",\"error\":\"text\",\"counts\":{\"record\":{\"files\":\"number\",\"saved\":\"number\",\"failed\":\"number\",\"waiting\":\"number\"}}}}"));
            return new("motion.import.batch.status",type,"Animation collection status","Latest native batch identity, control version, phase and file counts. A completed start receipt only starts work; completed means all files saved, partial means failed or waiting files remain. Phases idle/selecting/ready/running/stopping/completed/partial/failed/cancelled/cleared. Read individual file results by zero-based index. Only explicit start/retry resumes work; clear releases drained sources/results. Read-only and bounded; restart retains libraries, not this session.",null,null,(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<ImportBatchWorkshop>():null;return owner?ProgramValue.Literal(owner.ObserveSession(),type):null;});
        }
        internal static BehaviourCatalog.FactDefinition File()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"requestId\":\"text\",\"index\":\"number\",\"name\":\"text\",\"state\":\"text\",\"error\":\"text\",\"motionCount\":\"number\",\"motionOffset\":\"number\",\"motionIds\":{\"list\":\"text\"}}}"));
            var input=Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["index"]=Number(0,127,true),["motionOffset"]=Number(0,31,true)});
            return new("motion.import.batch.file",type,"Animation import file result","Read one exact batch file by index. pending/reading/importing/saved/failed and up to eight exact saved motion IDs from motionOffset. motionCount gives the full count; inspect later offsets for all IDs. Selection does not preload provider metadata; names can remain Selected file N until read. Names/errors are untrusted display data shortened to fit the serialized text budget; exact motion IDs are never shortened. Stop preserves completed files and failed retry errors; a write already accepted may finish afterward. Unknown request or index is unavailable. No path/URI is returned.",input,new JObject {["requestId"]="00000000000000000000000000000000",["index"]=0,["motionOffset"]=0},(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<ImportBatchWorkshop>():null;var value=owner?owner.ObserveFile((string)args["requestId"],(int)args["index"],(int)args["motionOffset"]):null;return value==null?null:ProgramValue.Literal(value,type);});
        }
    }
}
