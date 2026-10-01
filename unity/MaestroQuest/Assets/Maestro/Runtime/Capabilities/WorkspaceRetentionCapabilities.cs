// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceRetentionCapability:CapabilityModule
    {
        readonly string mode;internal WorkspaceRetentionCapability(string mode){this.mode=mode;}
        public override string Id=>"workspace.retention."+mode;
        public override string Label=>mode=="inspect"?"Inspect retained workspaces":"Export retained workspace";
        public override string Domain=>"workspace";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>600;
        public override string Description=>mode=="inspect"?
            "List up to 64 retained generations, without initializing or changing selection. Read workspace.retention.entry with the returned inspectionId and each index. Metadata availability is not content verification. Roles distinguish active, previous, pointer-backup, inactive or unknown; reservation describes stored activation/recovery dependencies, not deletion permission. A new inspection replaces this session inventory. No private paths or documents are exposed.":
            "Export one inspected inactive generation as an importable portable workspace ZIP in Downloads/Maestro. Validates its actual current saved documents/models/motions/modules and creates a new manifest: originalManifestHash identifies its original import, while output manifestHash identifies exported content, which may include newer autosaves. Refuses active/live sources, changed selection, unavailable required documents and invalid assets; never falls back to old manifests, backup files or empty defaults. excludedFiles counts other files inside its data directory omitted from the portable archive; generation metadata, recovery preservation, action receipts, scans, Undo and chat are not portable content. They remain on-device. Missing-reference and unavailable-program counts describe preserved unresolved references, not repaired content. This is not a full raw backup or permission to delete a generation. Completion means native publication; after interrupted publication check Downloads and the matching receipt instead of assuming failure.";
        public override JObject InputSchema {get{
            var fields=mode=="inspect"?new JObject():new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["generationId"]=Text("^[a-f0-9]{32}$",32),["originalManifestHash"]=Text("^[a-f0-9]{64}$",64)};
            var value=Object(fields);value["x-features"]=new JArray("workspaceRetention.v1");return value;
        }}
        public override JObject OutputSchema=>mode=="inspect"?Object(new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["count"]=Number(0,64,true),["selectionReadable"]=new JObject {["type"]="boolean"}}):Object(new JObject {
            ["generationId"]=Text("^[a-f0-9]{32}$",32),["originalManifestHash"]=Text("^[a-f0-9]{64}$",64),["manifestHash"]=Text("^[a-f0-9]{64}$",64),["sourceFingerprint"]=Text("^[a-f0-9]{64}$",64),["location"]=Text("^Downloads/Maestro/.+\\.zip$",128),["archiveHash"]=Text("^[a-f0-9]{64}$",64),["sizeKiB"]=Number(1d/1024,524288),["excludedFiles"]=Number(0,4096,true),["missingModels"]=Number(0,4096,true),["missingMotions"]=Number(0,4096,true),["missingControllerPrograms"]=Number(0,4096,true),["unavailablePrograms"]=Number(0,4096,true)});
        public override JObject Example=>mode=="inspect"?new JObject():new JObject {["inspectionId"]=new string('0',32),["generationId"]=new string('0',32),["originalManifestHash"]=new string('0',64)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Retained workspace operations are unavailable.";return context.Workspace?.Retention?.CanStart(mode,args,out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;try{operation=new RetentionOperation(context.Workspace.Retention,context.Workspace.Retention.Start(mode,args,runId));return true;}catch(System.Exception){error="Retained workspace operation could not start. Inspect its current state.";return false;}}
        sealed class RetentionOperation:CapabilityOperation
        {
            readonly WorkspaceRetention service;readonly WorkspaceRetention.Job job;
            internal RetentionOperation(WorkspaceRetention service,WorkspaceRetention.Job job){this.service=service;this.job=job;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){service.Poll();error=job.Error;return job.Pending?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
            public override JObject Result=>job.Result??new JObject();
            public override void Stop(bool preservePlacement)=>service.Cancel(job);
            public override string InterruptionStatus=>"Stopped waiting. Publication may have happened; inspect workspace.retention and Downloads before continuing.";
        }
    }
    internal static class WorkspaceRetentionFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()=>new("workspace.retention",ProgramDataType.Read(new JObject {["record"]=new JObject {["requestId"]="text",["phase"]="text",["status"]="text",["inspectionId"]="text",["generationId"]="text",["manifestHash"]="text",["location"]="text",["archiveHash"]="text"}}),
            "Retained workspace status","Latest session-local inspection or publication. requestId is its native execution runId. Exported does not mean restored, reviewed, or removable. Cancelled waits may still finish publication. Restart clears this status/inventory; durable action receipts remain inspectable.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.Retention==null?null:ProgramValue.Literal(context.Workspace.Retention.Status()),domain:"workspace");
        internal static BehaviourCatalog.FactDefinition Entry()=>new("workspace.retention.entry",ProgramDataType.Read(new JObject {["record"]=new JObject {["generationId"]="text",["originalManifestHash"]="text",["metadataAvailable"]="boolean",["role"]="text",["reservation"]="text"}}),
            "Retained workspace entry","Read one metadata entry from the exact completed inspection. metadataAvailable never asserts valid saved contents. role is active, previous, pointer-backup, inactive or unknown; reservation is reserved, unreserved or unknown. Unknown means evidence could not be interpreted. Original manifest hashes can differ from the actual saved content. None of these fields grants deletion permission; an inactive export verifies content separately.",Object(new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["index"]=Number(0,63,true)}),new JObject {["inspectionId"]=new string('0',32),["index"]=0},(context,args)=>{var value=context.Workspace?.Retention?.Entry((string)args["inspectionId"],(int)args["index"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
    }
}
