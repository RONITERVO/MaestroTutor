// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspacePrepareReviewCapability:CapabilityModule
    {
        public override string Id=>"workspace.review.prepare";
        public override string Label=>"Inspect workspace for review";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Prepare an exact review of the current imported workspace, using expectedRevision from workspace.current. Briefly hold editing while native code captures accepted documents, libraries and assets. Return a tracked requestId immediately; read workspace.review for prepared status, manifestHash, workspace identity and missing-reference counts. No ZIP download is written and no activity is enabled. Inspect the actual contents and missing references before requesting completion. Editing is available again after inspection, but later edits require a new review. Only the latest review request is retained.";
        public override JObject InputSchema {get{var value=Object(new JObject {["expectedRevision"]=Text("^(initial|[a-f0-9]{32})$",32)});value["x-features"]=new JArray("workspaceReview.v1");return value;}}
        public override JObject Example=>new JObject {["expectedRevision"]=new string('0',32)};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace review is unavailable";return context.Workspace?.Review?.CanPrepare((string)args["expectedRevision"],out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;try{operation=new CompletedCapability(new JObject {["requestId"]=context.Workspace.Review.Prepare((string)args["expectedRevision"])});return true;}catch(System.Exception){error="Review inspection could not start. Inspect workspace.current and its tracked review status.";return false;}}
    }
    internal sealed class WorkspaceCompleteReviewCapability:CapabilityModule
    {
        public override string Id=>"workspace.review.complete";
        public override string Label=>"Complete inspected workspace review";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Complete only the exact review the user inspected and approved. Supply requestId, manifestHash and expectedRevision from the prepared workspace.review value. Native code freezes editing, saves accepted documents and independently captures their current content hash; a mismatch stays under review and requires a new inspection. Returns the tracked review requestId, not proof of completion. Read workspace.review until completed or an explicit failure. Completion records approval before releasing the review hold. Stopped programs, physics and held controller input do not restart. Other native holds can still prevent activity. A cancellation or lost response cannot undo committed approval; inspect its outcome rather than retry blindly.";
        public override JObject InputSchema {get{var value=Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["manifestHash"]=Text("^[a-f0-9]{64}$",64),["expectedRevision"]=Text("^[a-f0-9]{32}$",32)});value["x-features"]=new JArray("workspaceReview.v1");return value;}}
        public override JObject Example=>new JObject {["requestId"]=new string('0',32),["manifestHash"]=new string('0',64),["expectedRevision"]=new string('0',32)};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace review is unavailable";return context.Workspace?.Review?.CanComplete(args,out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;try{operation=new CompletedCapability(new JObject {["requestId"]=context.Workspace.Review.Complete(args)});return true;}catch(System.Exception){error="Review completion could not start. Activity remains held; inspect its tracked status.";return false;}}
    }
    internal sealed class WorkspaceCancelReviewCapability:CapabilityModule
    {
        public override string Id=>"workspace.review.cancel";
        public override string Label=>"Cancel workspace review";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Cancel the exact review inspection or request cancellation before approval commits. Editing stays held until its worker finishes; room activity stays under review. Cancellation cannot retract a committed approval. Read workspace.review for the actual outcome. An old ID never cancels a newer review.";
        public override JObject InputSchema {get{var value=WorkspaceSelectionFacts.RequestSchema();value["x-features"]=new JArray("workspaceReview.v1");return value;}}
        public override JObject Example=>new JObject {["requestId"]=new string('0',32)};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace review is unavailable";return context.Workspace?.Review?.CanCancel((string)args["requestId"],out error)==true;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {operation=null;if(!CanRun(context,args,out error))return false;context.Workspace.Review.Cancel((string)args["requestId"]);operation=new CompletedCapability(args);return true;}
    }
    internal static class WorkspaceReviewFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()
        {
            var counters=new JObject();foreach(string field in WorkspaceReview.Counts)counters[field]="number";
            var fields=new JObject {["requestId"]="text",["phase"]="text",["status"]="text",["workspace"]=new JObject {["record"]=new JObject {["generationId"]="text",["revision"]="text"}},["manifestHash"]="text",["committedRevision"]="text",["summary"]=new JObject {["record"]=counters},["activityHeld"]="boolean"};
            return new BehaviourCatalog.FactDefinition("workspace.review",ProgramDataType.Read(new JObject {["record"]=fields}),"Workspace review status",
                "Read the latest exact review request. Preparing captures accepted content; prepared means its manifestHash and summary are ready to inspect, not that activity is approved. Completing rechecks and saves that content. Completed means approval was durably committed; activityHeld reports any remaining native hold. Stale means accepted content changed and needs new inspection. Failed/cancelled/interrupted/unavailable never imply approval; read workspace.current before further action. Completion is not an animation or program start. The first receipt only acknowledges this tracked operation.",WorkspaceSelectionFacts.RequestSchema(),new JObject {["requestId"]=new string('0',32)},(context,args)=>{var value=context.Workspace?.Review?.Read((string)args["requestId"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
        }
    }
}
