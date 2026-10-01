// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceActivateCapability:CapabilityModule
    {
        public override string Id=>"workspace.archive.activate";
        public override string Label=>"Activate reviewed workspace archive";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Start activating the exact prepared preview after the user requests the switch. Read workspace.archive.selection for selectionRequestId, generationId and manifestHash, and workspace.current for expectedRevision. Preserve accepted current content in a separate verified generation before any switch. Editing and activity are held until preservation/commit finish. Returns a tracked activation requestId, not completed activation. Read workspace.archive.activation with that ID. The book and agent transport survive; the room agent session changes. Imported activity stays held for review. Pause or cancellation attempts to stop before commit, but cannot undo a committed selection; always inspect the outcome. Never silently retry an interrupted pair after editing resumes. Startup reconciles the exact pair without replay. Only the latest activation status is retained.";
        public override JObject InputSchema {get{var value=Object(new JObject {["selectionRequestId"]=Text("^[a-f0-9]{32}$",32),["generationId"]=Text("^[a-f0-9]{32}$",32),["manifestHash"]=Text("^[a-f0-9]{64}$",64),["expectedRevision"]=Text("^(initial|[a-f0-9]{32})$",32)});value["x-features"]=new JArray("workspaceArchiveActivation.v1");return value;}}
        public override JObject Example=>new JObject {["selectionRequestId"]=new string('0',32),["generationId"]=new string('0',32),["manifestHash"]=new string('0',64),["expectedRevision"]="initial"};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace activation is unavailable";return context.Workspace?.Activation?.CanStart(args,out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            try{operation=new CompletedCapability(new JObject {["requestId"]=context.Workspace.Activation.Start(args)});return true;}
            catch(System.Exception){error="Workspace activation could not start. Inspect workspace.current and its tracked status before retrying.";return false;}
        }
    }
    internal sealed class WorkspaceCancelActivationCapability:CapabilityModule
    {
        public override string Id=>"workspace.archive.activation.cancel";
        public override string Label=>"Cancel workspace activation";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Request cancellation of the exact tracked activation. Preservation/cleanup can continue under the edit hold until workers finish. Cancellation cannot retract a committed selection. Read workspace.archive.activation to learn the outcome; the cancellation acknowledgement is not proof that no switch occurred. An old request never cancels a newer one.";
        public override JObject InputSchema {get{var value=WorkspaceSelectionFacts.RequestSchema();value["x-features"]=new JArray("workspaceArchiveActivation.v1");return value;}}
        public override JObject Example=>new JObject {["requestId"]=new string('0',32)};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace activation is unavailable";return context.Workspace?.Activation?.CanCancel((string)args["requestId"],out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;context.Workspace.Activation.Cancel((string)args["requestId"]);operation=new CompletedCapability(args);return true;}
    }
    internal static class WorkspaceActivationFacts
    {
        internal static BehaviourCatalog.FactDefinition Current()=>new("workspace.current",ProgramDataType.Read(new JObject {["record"]=new JObject {["revision"]="text",["generationId"]="text",["available"]="boolean",["reviewRequired"]="boolean",["changing"]="boolean",["activationRequestId"]="text",["error"]="text"}}),"Current workspace",
            "Read the live selection revision, active generation, content availability, review hold and latest tracked activation ID. Empty identity means no selection could be read. This does not release review or recover anything. Use the revision with an explicitly reviewed archive activation.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.Activation==null?null:ProgramValue.Literal(context.Workspace.Activation.Current()),domain:"workspace");
        internal static BehaviourCatalog.FactDefinition Activation()
        {
            var fields=new JObject();foreach(var key in new[]{"requestId","selectionRequestId","originRevision","committedRevision","phase","status"})fields[key]="text";
            foreach(var key in new[]{"preview","retained"})fields[key]=new JObject {["record"]=new JObject {["generationId"]="text",["manifestHash"]="text"}};
            return new BehaviourCatalog.FactDefinition("workspace.archive.activation",ProgramDataType.Read(new JObject {["record"]=fields}),"Workspace activation status",
                "Read the exact tracked activation. Preserving/activating precede the switch; committed means the durable selection changed. Review means imported content opened and remains held for review. Unavailable, failed, cancelled or interrupted require inspecting workspace.current; never invent success or replay the old request. Retained identity describes the previous accepted snapshot, not an instruction to discard it. Only the latest job is retained. Startup reconciles its exact committed pair and never repeats a commit.",WorkspaceSelectionFacts.RequestSchema(),new JObject {["requestId"]=new string('0',32)},(context,args)=>{var value=context.Workspace?.Activation?.Read((string)args["requestId"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
        }
    }
}
