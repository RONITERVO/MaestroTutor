// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceHistoryCapability:CapabilityModule
    {
        readonly bool reset;internal WorkspaceHistoryCapability(bool reset){this.reset=reset;}
        public override string Id=>reset?"workspace.history.reset":"workspace.history.inspect";
        public override string Label=>reset?"Preserve and reset unavailable history":"Inspect unavailable workspace history";
        public override string Domain=>"workspace";
        public override string Duration=>"completion";
        public override string Description=>reset?
            "Reset only the exact unavailable operation history from the latest completed workspace.history.inspect result. Native code checks its fingerprint again, preserves the unreadable status and accepted tracking state, then atomically removes that status from use. Saved captures, room data, selected workspace, action receipts and required content review stay intact. No old action is replayed or approved. Requires all workspace workers/holds idle. Stop or lost focus requests cancellation, but cannot retract a completed rename. Inspect workspace.history and workspace.current after uncertainty; do not blindly replay. The inspection is consumed even on failure; inspect again to retry.":
            "Inspect unavailable activation, review or recovery operation history for an explicit tracking reset. Waits for bounded native file inspection; returns a requestId and fingerprint for workspace.history.reset. Only history already reported unavailable can be reset, after all workspace operations finish. This does not change files or approve any workspace content. The inspection is held only in this app session; restart requires a new inspection. A changed file or accepted record makes reset stale.";
        public override JObject InputSchema {get{var value=Object(reset?new JObject {["inspectionId"]=Text("^[a-f0-9]{32}$",32),["fingerprint"]=Text("^[a-f0-9]{64}$",64)}:new JObject {["target"]=Choice("activation","review","recovery")});value["x-features"]=new JArray("workspaceHistory.v1");return value;}}
        public override JObject OutputSchema=>Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["target"]=Choice("activation","review","recovery"),["fingerprint"]=Text("^[a-f0-9]{64}$",64),["evidenceId"]=Text("^([a-f0-9]{32})?$",32),["summary"]=Object(new JObject {["files"]=Number(0,256,true),["sizeKiB"]=Number(0,16384,true)})});
        public override JObject Example=>reset?new JObject {["inspectionId"]=new string('0',32),["fingerprint"]=new string('0',64)}:new JObject {["target"]="recovery"};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Workspace history repair is unavailable.";var history=context.Workspace?.History;return history!=null&&(reset?history.CanReset((string)args["inspectionId"],(string)args["fingerprint"],out error):history.CanInspect((string)args["target"],out error));}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var history=context.Workspace.History;
            try{operation=new HistoryOperation(history,reset?history.Reset((string)args["inspectionId"],(string)args["fingerprint"]):history.Inspect((string)args["target"]));return true;}
            catch(System.Exception){error="History repair could not start. Inspect current workspace history before continuing.";return false;}
        }
        sealed class HistoryOperation:CapabilityOperation
        {
            readonly WorkspaceHistory history;readonly WorkspaceHistory.Job job;
            internal HistoryOperation(WorkspaceHistory history,WorkspaceHistory.Job job){this.history=history;this.job=job;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){history.Poll();error=job.Error;return job.Pending?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
            public override JObject Result=>job.Result;
            public override void Stop(bool preservePlacement)=>history.Cancel(job);
            public override string InterruptionStatus=>"Stopped waiting. Preservation may already be committed; inspect workspace.history and the current workspace.";
        }
    }
    internal static class WorkspaceHistoryFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()=>new("workspace.history",ProgramDataType.Read(new JObject {["record"]=new JObject {
            ["requestId"]="text",["target"]="text",["phase"]="text",["status"]="text",["fingerprint"]="text",["evidenceId"]="text",["summary"]=new JObject {["record"]=new JObject {["files"]="number",["sizeKiB"]="number"}},["acceptedAvailable"]="boolean"}}),
            "Workspace history repair status","Read the latest history inspection/reset in this app session. Match requestId and target; another request replaces this status. Reset means history was preserved and tracking cleared, not that workspace content was recovered or approved. Failed/cancelled never implies reset. After restart inspect current workspace and action receipts; old operations never replay. Evidence is retained privately; no filesystem path is accepted or exposed.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.History==null?null:ProgramValue.Literal(context.Workspace.History.Status()),domain:"workspace");
    }
}
