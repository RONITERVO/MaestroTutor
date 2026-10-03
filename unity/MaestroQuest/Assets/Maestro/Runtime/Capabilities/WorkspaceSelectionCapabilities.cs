// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceSelectCapability:CapabilityModule
    {
        public override string Id=>"workspace.archive.select";
        public override string Label=>"Choose workspace archive";
        public override string Description=>"Open Android's file chooser for one workspace ZIP. Returns a tracked requestId immediately, not a selected or restored workspace. The user must choose the file in Android. The system chooser pauses room actions; they will not resume or replay. After returning, read workspace.archive.selection with this requestId to follow copy, validation and preview. Preparation never activates imported content. Cancel the request to discard its unused preview. Only one selection or preview at a time; file limit 512 MB. Activate only an explicitly reviewed preview with workspace.archive.activate and the current workspace revision.";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override JObject InputSchema {get{var schema=Object(new JObject());schema["x-features"]=new JArray("workspaceArchiveSelection.v1");return schema;}}
        public override JObject Example=>new JObject();
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){var owner=context.ArchiveImport;error="Workspace selection is unavailable";return owner&&owner.CanSelect(out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            operation=new CompletedCapability(new JObject {["requestId"]=context.ArchiveImport.Select()});return true;
        }
    }
    internal sealed class WorkspaceCancelSelectionCapability:CapabilityModule
    {
        public override string Id=>"workspace.archive.cancel";
        public override string Label=>"Cancel archive selection";
        public override string Description=>"Cancel the exact file-selection or previous-workspace request, or discard its verified, unused preview. It never deletes the source file or changes the active workspace. Cleanup may continue after this acknowledgement; read workspace.archive.selection for cancelled/failed and any retained preview. Cancelling does not restart paused room actions. A stale request cannot cancel a newer chooser.";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override JObject InputSchema {get{var schema=WorkspaceSelectionFacts.RequestSchema();schema["x-features"]=new JArray("workspaceArchiveSelection.v1");return schema;}}
        public override JObject Example=>new JObject {["requestId"]="00000000000000000000000000000000"};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){var owner=context.ArchiveImport;error="Workspace selection is unavailable";return owner&&owner.CanCancel((string)args["requestId"],out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            context.ArchiveImport.Cancel((string)args["requestId"]);operation=new CompletedCapability(args);return true;
        }
    }
    internal static class WorkspaceSelectionFacts
    {
        internal static JObject RequestSchema()=>Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32)});
        public static BehaviourCatalog.FactDefinition Selection()
        {
            var counters=new JObject();foreach(string field in new[]{"files","models","motions","modules","unavailablePrograms","missingModels","missingMotions","missingControllerPrograms"})counters[field]="number";
            var fields=new JObject();foreach(string field in new[]{"requestId","phase","name","error","generationId","manifestHash"})fields[field]="text";fields["summary"]=new JObject {["record"]=counters};
            return new BehaviourCatalog.FactDefinition("workspace.archive.selection",ProgramDataType.Read(new JObject {["record"]=fields}),"Archive selection status",
                "Read the exact archive or previous-workspace preview request. Previous-workspace preparation uses no file chooser. Phases: selecting, copying, preparing, prepared, activating, activated, retained, cancelling, cancelled or failed. Activated means the preview was committed; follow workspace.archive.activation to establish whether content opened. Retained means an interrupted attempt must not be reused; its files are preserved. Prepared means its manifest, documents and assets were verified into a private preview; nothing was activated. Summary counts describe that preview, including unavailable programs and missing references. Inspect errors before another choice. IDs and counts are empty/zero until a preview exists. An unknown request or paused runtime is unavailable. Read-only; never opens a chooser, imports or runs anything.",
                RequestSchema(),new JObject {["requestId"]="00000000000000000000000000000000"},(context,args)=>{
                    var owner=context.Workspace?context.Workspace.Import:context.Editor?context.Editor.GetComponent<WorkspaceImport>():null;var value=owner?owner.ReadSelection((string)args["requestId"]):null;
                    return value==null?null:ProgramValue.Literal(value);
                },domain:"workspace");
        }
    }
}
