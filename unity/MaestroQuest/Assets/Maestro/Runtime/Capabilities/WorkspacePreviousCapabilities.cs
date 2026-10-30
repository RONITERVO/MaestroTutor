// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceSelectPreviousCapability:CapabilityModule
    {
        public override string Id=>"workspace.previous.select";
        public override string Label=>"Inspect previous workspace";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Prepare a verified private preview of the exact retained previous workspace identified by workspace.previous. No Android chooser is needed. Supply its generationId, manifestHash and revision as expectedRevision. Returns a tracked requestId; read workspace.archive.selection until prepared or failed. Metadata availability is not proof that files are intact. Inspect the preview and known missing references before using workspace.archive.activate with its new generation/hash and the same expectedRevision. Activation preserves today's accepted contents before switching, with fresh action history and activity held for content review. A changed selection makes this preview stale; cancel it and inspect again. This path requires readable current content; it does not recover a corrupt or unavailable current workspace. Cancel through workspace.archive.cancel. No file, motion or program starts from selection.";
        public override JObject InputSchema {get{var schema=Object(new JObject {["expectedRevision"]=Text("^[a-f0-9]{32}$",32),["generationId"]=Text("^[a-f0-9]{32}$",32),["manifestHash"]=Text("^[a-f0-9]{64}$",64)});schema["x-features"]=new JArray("workspacePrevious.v1");return schema;}}
        public override JObject Example=>new JObject {["expectedRevision"]=new string('0',32),["generationId"]=new string('0',32),["manifestHash"]=new string('0',64)};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="The current workspace must be readable and idle before preparing previous-workspace recovery.";var host=context.Workspace;
            if(!host||!host.Current||host.Switching||host.Activation?.Busy==true||host.Review?.Busy==true||host.Retiring||host.Recovery?.BlocksOtherOperations==true||host.Selection?.Revision!=(string)args["expectedRevision"])return false;
            return WorkspaceArchiveCapture.CanStart(host.Current.Editor,host.Current.Rules,host.Current.Controls,out error)&&host.Import.CanSelectPrevious(args,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            operation=new CompletedCapability(new JObject {["requestId"]=context.Workspace.Import.SelectPrevious(args)});return true;
        }
    }
    internal static class WorkspacePreviousFacts
    {
        internal static BehaviourCatalog.FactDefinition Previous()=>new("workspace.previous",ProgramDataType.Read(new JObject {["record"]=new JObject {["revision"]="text",["generationId"]="text",["manifestHash"]="text",["available"]="boolean",["error"]="text"}}),"Previous workspace identity",
            "Read the exact retained previous-workspace metadata and the current selection revision. Available means metadata can be read, not that its assets have passed verification. Use workspace.previous.select to create and verify a separate preview, then follow workspace.archive.selection. This read never switches, resumes, clones or deletes content. Missing/corrupt metadata is unavailable; no empty or original workspace is substituted.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.Import==null?null:ProgramValue.Literal(context.Workspace.Import.ReadPrevious()),domain:"workspace");
    }
}
