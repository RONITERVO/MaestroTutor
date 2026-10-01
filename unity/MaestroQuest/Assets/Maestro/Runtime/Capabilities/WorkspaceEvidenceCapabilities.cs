// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceEvidenceCapability:CapabilityModule
    {
        readonly string mode;internal WorkspaceEvidenceCapability(string mode){this.mode=mode;}
        public override string Id=>"workspace.evidence."+mode;
        public override string Label=>mode=="inspect"?"Inspect preserved recovery evidence":mode=="export"?"Export preserved recovery evidence":"Remove exported recovery evidence";
        public override string Domain=>"workspace";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>600;
        public override string Description=>mode switch {
            "inspect"=>"Inspect the preserved files from workspace.history.reset, without changing them. Returns a session-local inspectionId, count and total size. Read workspace.evidence.entry for each index to obtain exact evidenceId and fingerprint. This currently inventories operation-history evidence, not retained workspace generations, room content or action receipts. No private path or original status contents are exposed. A new inspection replaces the previous inventory.",
            "export"=>"Publish the exact inspected recovery-evidence entry as a diagnostic ZIP in Downloads/Maestro. Preserves every file byte and empty-directory identity, including malformed tracking and accepted state, with a hashed manifest and fixed payload names. This is diagnostic evidence, not an importable playable workspace. Completion confirms publication and returns the archive hash. Keep the native execution runId: workspace.evidence.remove requires that retained completed export receipt for the exact same evidence bytes, including after restart. Stop requests cancellation before publication; after uncertainty check Downloads and workspace.evidence, never assume publication failed.",
            _=>"Remove only the exact inspected operation-history evidence that the user requested to remove after keeping its exported copy. Requires exportRunId from a retained completed workspace.evidence.export receipt with the same evidenceId and fingerprint. A changed entry requires new inspection/export; an expired or interrupted receipt requires another completed export. Publication proves a copy existed at completion, not that the user still has it. Workspace content, selection, captures, pending files and action receipts are never removed. No Undo. Cancellation before deletion preserves everything; once deletion begins it cannot roll back, and a storage failure may leave a partial entry. Inspect again after interruption. No automatic quota cleanup or replay occurs."};
        public override JObject InputSchema {get{
            var fields=new JObject();if(mode!="inspect"){fields["inspectionId"]=Text("^[a-f0-9]{32}$",32);fields["evidenceId"]=Text("^[a-f0-9]{32}$",32);fields["fingerprint"]=Text("^[a-f0-9]{64}$",64);}if(mode=="remove")fields["exportRunId"]=Text("^[a-f0-9]{32}$",32);
            var value=Object(fields);value["x-features"]=new JArray("workspaceEvidence.v1");return value;
        }}
        public override JObject OutputSchema=>mode=="inspect"?Object(new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["count"]=Number(0,4096,true),["sizeKiB"]=Number(0,131072,true)}):mode=="export"?Object(new JObject {["evidenceId"]=Text("^[a-f0-9]{32}$",32),["fingerprint"]=Text("^[a-f0-9]{64}$",64),["location"]=Text("^Downloads/Maestro/.+\\.zip$",128),["archiveHash"]=Text("^[a-f0-9]{64}$",64),["sizeKiB"]=Number(1d/1024,524288)}):Object(new JObject {["evidenceId"]=Text("^[a-f0-9]{32}$",32),["fingerprint"]=Text("^[a-f0-9]{64}$",64),["removed"]=new JObject {["type"]="boolean"}});
        public override JObject Example {get{var v=new JObject();if(mode!="inspect"){v["inspectionId"]=new string('0',32);v["evidenceId"]=new string('0',32);v["fingerprint"]=new string('0',64);}if(mode=="remove")v["exportRunId"]=new string('0',32);return v;}}
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Evidence maintenance is unavailable.";return context.Workspace?.Evidence?.CanStart(mode,args,out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;try{operation=new EvidenceOperation(context.Workspace.Evidence,context.Workspace.Evidence.Start(mode,args,runId));return true;}catch(System.Exception){error="Evidence operation could not start. Inspect its current state before continuing.";return false;}}
        sealed class EvidenceOperation:CapabilityOperation
        {
            readonly WorkspaceEvidence service;readonly WorkspaceEvidence.Job job;
            internal EvidenceOperation(WorkspaceEvidence service,WorkspaceEvidence.Job job){this.service=service;this.job=job;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){service.Poll();error=job.Error;return job.Pending?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
            public override JObject Result=>job.Result??new JObject();
            public override void Stop(bool preservePlacement)=>service.Cancel(job);
            public override string InterruptionStatus=>"Stopped waiting. Publication or removal may already have happened; inspect workspace.evidence before continuing.";
        }
    }
    internal static class WorkspaceEvidenceFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()=>new("workspace.evidence",ProgramDataType.Read(new JObject {["record"]=new JObject {["requestId"]="text",["phase"]="text",["status"]="text",["inspectionId"]="text",["evidenceId"]="text",["fingerprint"]="text",["location"]="text",["archiveHash"]="text"}}),
            "Evidence maintenance status","Latest session-local inspection, publication or removal status. requestId is the native execution runId; match that ID and evidence identity. Exported confirms a Downloads publication, not removal permission by itself: removal checks the retained completed native execution receipt. A cancelled wait can still finish publication/removal. Failed removal may leave partial evidence; inspect again. Restart clears this status and inventory, but retained native receipts remain inspectable.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.Evidence==null?null:ProgramValue.Literal(context.Workspace.Evidence.Status()),domain:"workspace");
        internal static BehaviourCatalog.FactDefinition Entry()=>new("workspace.evidence.entry",ProgramDataType.Read(new JObject {["record"]=new JObject {["kind"]="text",["evidenceId"]="text",["fingerprint"]="text",["files"]="number",["sizeKiB"]="number"}}),
            "Inspected evidence entry","Read one bounded item from the exact completed evidence inspection. kind history identifies operation-history preservation. It never identifies a live workspace or authorizes deletion. Use this exact identity/fingerprint for export, then retain its completed export execution receipt before requesting removal.",Object(new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["index"]=Number(0,4095,true)}),new JObject {["inspectionId"]=new string('0',32),["index"]=0},(context,args)=>{var value=context.Workspace?.Evidence?.Entry((string)args["inspectionId"],(int)args["index"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
    }
}
