// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ModelLibraryCapability:CapabilityModule
    {
        public override string Id=>"model.library.inspect";
        public override string Label=>"Browse imported models";
        public override string Duration=>"completion";
        public override string Description=>"Read a page of metadata from this workspace's private imported-model library (32 models/256 MB maximum). Results contain exact content hashes, safe display names and byte sizes, never external paths. Entries are sorted by hash and offsets are session snapshots, not stable indexes: inspect again after an import. Metadata is not proof of valid content, humanoid compatibility or permitted use; avatar.model.select verifies the chosen bytes and rig before saving. Empty modelHash on avatar.model.select chooses the current included Maestro; inspect avatar.included for its exact identity. The included avatar is copied into this same bounded library on first use and is portable in backups. This does not open a file picker, download or import a file.";
        public override JObject InputSchema {get {var schema=Object(new JObject {["offset"]=Number(0,32,true)});schema["x-features"]=new JArray("avatarModels.v1");return schema;}}
        public override JObject OutputSchema=>Object(new JObject {["total"]=Number(0,32,true),["offset"]=Number(0,32,true),["entries"]=List(Object(new JObject {["modelHash"]=Text("^[a-f0-9]{64}$",64),["name"]=Text("^[^\\u0000-\\u001f]{0,100}$",100),["bytes"]=Number(28,ModelInspection.MaximumBytes,true)}),0,8)});
        public override JObject Example=>new() {["offset"]=0};
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error=context.Editor?null:"The model library is unavailable";return context.Editor;}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(context,args,out error))return false;operation=new ListOperation(context.Editor.Models.ListAsync(),(int)args["offset"]);return true;}
        sealed class ListOperation:CapabilityOperation
        {
            readonly Task<ModelLibrary.Entry[]> task;readonly int offset;JObject result;
            internal ListOperation(Task<ModelLibrary.Entry[]> task,int offset){this.task=task;this.offset=offset;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){
                error=null;if(!task.IsCompleted)return RuleActionState.Preparing;
                if(task.IsFaulted||task.IsCanceled){error="The model library could not be inspected; check its local files and retry.";return RuleActionState.Failed;}
                result??=new JObject {["total"]=task.Result.Length,["offset"]=offset,["entries"]=new JArray(task.Result.Skip(offset).Take(8).Select(e=>new JObject {["modelHash"]=e.Hash,["name"]=e.Name,["bytes"]=e.Bytes}))};return RuleActionState.Ready;
            }
            public override JObject Result=>result??new JObject();
        }
    }
    internal sealed class AvatarModelCapability:NativeTargetCapability
    {
        public override string Id=>"avatar.model.select";
        public override string Label=>"Choose Maestro avatar";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","object.revision.current","model.humanoid","storage.writable"};
        public override string Description=>"Choose the current included Maestro with empty modelHash (resolved once to the exact hash from avatar.included), or an exact imported hash from model.library.inspect. The resolved hash is saved and returned; app artwork updates never replace saved selections. Inspect avatar.model and supply its revision. Shares the physical Use Maestro/Default path: loads and validates a hidden humanoid first, then saves and applies it as one Undo; temporary rooms remain unsaved until Keep. Preparation failure or cancellation before publication retains the previous saved and displayed model. Saving runs in the background and briefly reserves authored edits; unrelated runtime movement continues. Unconfirmed publication may leave disk and live identities different: saving is blocked for inspection or recovery, never automatically retried. Saved poses and recorded motions are retained. Embedded walk index resets on model change (the bundled default uses its declared walk clip); exact library motion/activity choices are retained and may be incompatible, never silently replaced. Other actions must release Maestro first. Stop before publication acceptance prevents later commit; once accepted, publication and display drain even after Stop or pause. Use Undo or explicit selection to revert confirmed completion. No file picker or downloaded code is used. Selection does not request a clip or recording; configured automatic tutor activity may resume when ready. Restart loads only the saved selection. Completion reports a ready selected model with its immutable accepted revision, even if a later observer edit changes the current revision; avatar.model separates saved and displayed identities when an existing saved model later becomes unavailable.";
        public override JObject InputSchema {get {var schema=Object(new JObject {["target"]=Resource(Choice("maestro")),["modelHash"]=Text("^([a-f0-9]{64})?$",64),["revision"]=Revision()});schema["x-features"]=new JArray("avatarModels.v1");return schema;}}
        public override JObject OutputSchema=>Object(new JObject {["target"]=Choice("maestro"),["modelHash"]=Text("^([a-f0-9]{64})?$",64),["revision"]=Revision(),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["target"]="maestro",["modelHash"]="",["revision"]=1};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out _,out error)&&context.Editor.CanSelectMaestroModel((string)args["modelHash"],(int)args["revision"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;return CanRun(context,args,out error)&&context.Editor.StartMaestroModel((string)args["modelHash"],(int)args["revision"],out operation,out error);
        }
        internal static BehaviourCatalog.FactDefinition IncludedFact()=>new("avatar.included",ProgramDataType.Read(new JObject {["record"]=new JObject {["modelHash"]="text",["name"]="text",["bundled"]="boolean",["attribution"]="text",["walkClipIndex"]="number"}}),
            "Included Maestro avatar","Metadata for this app build's offline default. Selecting empty modelHash resolves to this exact identity and stores it. Existing room selections, backups and animation references keep their own exact hashes across app updates. bundled=false means the original sketch fallback (empty hash). Metadata is not evidence of a loaded model: inspect avatar.model after selection. walkClipIndex identifies the declared embedded walk (-1 means the canonical walk); new rooms and a changed selection to this default use it when walking is explicitly started. Embedded clips do not start on selection.",null,null,(context,args)=>{
                if(!context.Editor)return null;var included=context.Editor.IncludedAvatar;
                return ProgramValue.Literal(new JObject {["modelHash"]=included?.Hash??"",["name"]=included?.Name??"Sketch Maestro",["bundled"]=included!=null,["attribution"]=included?.Attribution??"Maestro project artwork",["walkClipIndex"]=included?.WalkClipIndex??-1});
            });
        internal static BehaviourCatalog.FactDefinition Fact()=>new("avatar.model",ProgramDataType.Read(new JObject {["record"]=new JObject {["revision"]="number",["selectedHash"]="text",["displayedHash"]="text",["busy"]="boolean",["phase"]="text",["status"]="text",["temporary"]="boolean",["canPose"]="boolean"}}),
            "Current Maestro model","Live selected versus displayed model identity; empty hash denotes the original sketch fallback, while a packaged included avatar has its exact model hash. Read avatar.included to identify the current bundled default. loading includes queueing, preparation and publication while retaining the previous display. ready means live selected and displayed identities match, not independent disk verification; unavailable means those identities differ or publication was not confirmed. Rejected preparation leaves the previous saved identity. Unconfirmed publication can leave disk different from both live identities and blocks saves; bounded status reports this and recovery is required. Read the revision before selecting. Poses use canonical joints across supported humanoids.",null,null,(context,args)=>{var value=context.Editor?context.Editor.ObserveMaestroModel():null;return value==null?null:ProgramValue.Literal(value);});
    }
}
