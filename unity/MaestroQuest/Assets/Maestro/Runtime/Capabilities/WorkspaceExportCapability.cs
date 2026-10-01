// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceExportCapability:CapabilityModule
    {
        public override string Id=>"workspace.archive.export";
        public override string Label=>"Export native workspace";
        public override string Description=>"Save a portable ZIP in Quest Downloads/Maestro containing accepted room objects and recipes, recorded animations, saved behaviours, controller settings, avatar activities, models, motion files and reusable modules. It captures one snapshot; later edits stay in the workspace. Keep or discard temporary play first. Chat, credentials, room scans, active takes, running programs, Undo and execution receipts are excluded. Missing references remain unchanged and are counted in the result. Export does not change the workspace. Completion means Android closed and published the file; waiting is bounded to ten minutes. Stop cannot retract publication; after interruption check Downloads/Maestro before retrying. Restore is not available yet.";
        public override string Domain=>"workspace";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>600;
        public override IReadOnlyList<string> Requirements=>new[]{"workspace.ready","libraries.idle","downloads.available"};
        public override JObject InputSchema {get {var schema=Object(new JObject());schema["x-features"]=new JArray("workspaceArchiveExport.v1");return schema;}}
        public override JObject Example=>new JObject();
        public override JObject OutputSchema=>Object(new JObject {["location"]=Text("^Downloads/Maestro/.+\\.zip$",128),["sizeKiB"]=Number(1d/1024,WorkspaceArchive.MaximumArchiveBytes/1024d),["manifestHash"]=Text("^[a-f0-9]{64}$",64),["files"]=Number(1,1400,true),["models"]=Number(0,32,true),["motions"]=Number(0,1024,true),["modules"]=Number(0,256,true),["unavailablePrograms"]=Number(0,1000000,true),["missingModels"]=Number(0,1000000,true),["missingMotions"]=Number(0,1000000,true),["missingControllerPrograms"]=Number(0,1000000,true)});
        public override bool CanRun(CapabilityContext context,JObject args,out string error){var export=context.ArchiveExport;error="Workspace export is unavailable";return export&&export.CanStart(out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            try{operation=new Exporting(context.ArchiveExport.StartExport());return true;}catch(Exception ex){error=ex.Message;return false;}
        }
        sealed class Exporting:CapabilityOperation
        {
            readonly Task<JObject> task;
            public Exporting(Task<JObject> task){this.task=task;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=task.IsCanceled?"Workspace export was interrupted; check Downloads/Maestro before retrying.":task.Exception?.GetBaseException().Message;return !task.IsCompleted?RuleActionState.Preparing:error!=null?RuleActionState.Failed:RuleActionState.Ready;}
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?(JObject)task.Result.DeepClone():new JObject();
            public override string InterruptionStatus=>"Stopped waiting. The archive may still appear in Downloads/Maestro; check there before exporting again.";
        }
    }
}
