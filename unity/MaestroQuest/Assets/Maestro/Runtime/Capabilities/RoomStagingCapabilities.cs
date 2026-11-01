// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RoomStagingCapability:CapabilityModule
    {
        internal const string Feature="roomStagingMaintenance.v1";
        readonly bool discard;
        internal RoomStagingCapability(bool discard=false){this.discard=discard;}
        public override string Id=>discard?"room.storage.preparations.discard":"room.storage.preparations.inspect";
        public override string Label=>discard?"Discard unfinished save preparation":"Inspect unfinished save preparations";
        public override string Domain=>"room";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>120;
        public override string Description=>discard?
            "Permanently discard one exact eligible unfinished room-save preparation from room.storage.preparations.entry. Requires its inspectionId, entryId, fingerprint and confirmation equal to discard followed by a space and that entryId. Obtain explicit user intent to discard this unfinished preparation; inspection or a request for more space is not permission. No Undo. Protects active preparations and all accepted room/backup/recovery files. Holds new edits briefly while rechecking and removing only the reviewed bytes. A stopped or failed attempt may have renamed or removed its private stage; inspect again and never automatically retry or replay. Every attempt consumes the review.":
            "Read-only inspection of at most 32 private unfinished save preparations in the current workspace. New preparations are limited to four files of at most 4 MiB each, including abandoned files. Read room.storage.preparations.entry with the returned inspectionId and index. Active, linked, oversized or unreadable files are ineligible. More means this is a partial inventory: inspect again after explicit cleanup if needed. These bytes are not accepted rooms and are never adopted as saves. No private paths or file contents are exposed. Inspection does not authorize deletion.";
        public override JObject InputSchema {get{
            var fields=discard?new JObject{["inspectionId"]=Text("^[a-f0-9]{32}$",32),["entryId"]=Text("^[a-f0-9]{32}$",32),
                ["fingerprint"]=Text("^[a-f0-9]{64}$",64),["confirmation"]=Text("^discard [a-f0-9]{32}$",40)}:new JObject();
            var schema=Object(fields);schema["x-features"]=new JArray(Feature);
            if(discard)schema["x-confirmation"]="Permanently discard this reviewed unfinished save preparation? It is not an accepted room save. There is no Undo; accepted room and backup files remain protected.";
            return schema;
        }}
        public override JObject OutputSchema=>discard?Object(new JObject{["entryId"]=Text("^[a-f0-9]{32}$",32),["fingerprint"]=Text("^[a-f0-9]{64}$",64),["removed"]=new JObject{["type"]="boolean"}}):
            Object(new JObject{["inspectionId"]=Text("^[a-f0-9]{32}$",32),["count"]=Number(0,PreparedStageInventory.MaximumEntries,true),["more"]=new JObject{["type"]="boolean"}});
        public override JObject Example=>discard?new JObject{["inspectionId"]=new string('0',32),["entryId"]=new string('0',32),["fingerprint"]=new string('0',64),["confirmation"]="discard "+new string('0',32)}:new JObject();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {error="The current room is unavailable.";return context.Editor&&context.Editor.StagingMaintenance.CanStart(discard?"discard":"inspect",args,out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            var service=context.Editor.StagingMaintenance;
            if(!service.Start(discard?"discard":"inspect",runId,args,out var job,out error))return false;
            operation=new Maintenance(service,job);return true;
        }
        sealed class Maintenance:CapabilityOperation
        {
            readonly RoomStagingMaintenance service;readonly RoomStagingMaintenance.Job job;bool completed;
            internal Maintenance(RoomStagingMaintenance service,RoomStagingMaintenance.Job job){this.service=service;this.job=job;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){service.Poll();error=job.Error;return !job.Finished?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
            public override JObject Result=>job.Result??new JObject();
            // The scheduler calls Stop to release completed operations too. Keep a
            // delivered inspection available for its next explicitly confirmed action.
            public override bool Complete(out string error){completed=State(out error)==RuleActionState.Ready;return completed;}
            public override void Stop(bool preservePlacement){if(!completed)service.Cancel(job);}
            public override string InterruptionStatus=>"Stopped waiting for preparation maintenance. Its file operation may have completed; inspect current preparations before continuing. Never automatically replay disposal.";
        }
    }
    internal static class RoomStagingFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()=>new("room.storage.preparations",
            ProgramDataType.Read(new JObject{["record"]=new JObject{["runId"]="text",["phase"]="text",["inspectionId"]="text",["count"]="number",["more"]="boolean",["error"]="text"}}),
            "Saved preparation maintenance status",
            "Latest session-local inspection or disposal result. Match runId to the requested execution. Completion of inspection is not deletion permission. Restart clears the inventory; interrupted operations are never replayed. This fact only reads cached status and never scans storage.",
            Object(new JObject()),new JObject(),(context,args)=>context.Editor?ProgramValue.Literal(context.Editor.StagingMaintenance.Status()):null,features:new[]{RoomStagingCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Entry()=>new("room.storage.preparations.entry",
            ProgramDataType.Read(new JObject{["record"]=new JObject{["entryId"]="text",["fingerprint"]="text",["bytes"]="number",["eligible"]="boolean",["reason"]="text"}}),
            "Reviewed unfinished preparation",
            "Read one cached entry by exact inspectionId and zero-based index below count. Eligibility is a snapshot, not permission. Disposal rechecks the fingerprint and active ownership. New inspection, any disposal attempt or suspension invalidates this review. No file path or document content is exposed.",
            Object(new JObject{["inspectionId"]=Text("^[a-f0-9]{32}$",32),["index"]=Number(0,PreparedStageInventory.MaximumEntries-1,true)}),
            new JObject{["inspectionId"]=new string('0',32),["index"]=0},
            (context,args)=>{var value=context.Editor?context.Editor.StagingMaintenance.Entry((string)args["inspectionId"],(int)args["index"]):null;return value==null?null:ProgramValue.Literal(value);},features:new[]{RoomStagingCapability.Feature});
    }
}
