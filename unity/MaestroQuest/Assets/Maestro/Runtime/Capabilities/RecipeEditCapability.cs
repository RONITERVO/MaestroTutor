// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RecipeEditCapability:NativeTargetCapability
    {
        public override string Id=>"object.recipe.edit";
        public override string Label=>"Edit recipe parts and animation";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.recipe","target.unheld","source.revision.current","authoring.inactive","storage.writable"};
        public override string Description=>"Apply one exact-revision patch to an editable recipe object. parts and tracks set whole entries by stable part ID; unspecified entries remain. removeParts/removeTracks delete only named existing entries, without cascading. IDs must be unique and cannot be both removed and set. Parents must exist; native code orders parents before children and rejects cycles. A removed part must have its children reparented/removed and its track removed in the same patch. Read object.recipe, object.recipe.part and object.recipe.track first. duration retimes untouched tracks proportionally; supplied track times use the new duration. loop is the saved playback preference. All edits stop this object's recipe playback and clear its saved autoplay flag; use animation.play explicitly afterward. Placement, tint, physics and recorded root motion remain. Unrelated actors continue. Each accepted patch saves one Undo, or changes only the temporary fork until Keep. Refusal or failed preparation leaves geometry and data intact. An accepted save failure keeps the live edit unconfirmed and requires recovery. Held/owned/actively authored targets are refused. No new object, physics start or animation playback. Full capacity remains 32 parts, 17 tracks and 16 keys per track; use smaller explicit patches within the shared message limits. Receipt replay is historical even after Undo. Current root placement and body velocity are preserved through acceptance and Undo/Redo. The result is the immutable accepted recipe summary."+PersistedEditOperation.Persistence;
        internal static JObject Target()=>Resource(Text("^[a-fA-F0-9]{32}$",32));
        public override JObject InputSchema {
            get {var recipe=RecipeSchema();var schema=CurrentInputs(Object(new JObject {["target"]=Target(),["revision"]=Revision(),["parts"]=List((JObject)recipe["properties"]["parts"]["items"],0,32),["removeParts"]=List(Text("^[a-zA-Z0-9_]{1,32}$",32),0,32),["tracks"]=List((JObject)recipe["properties"]["tracks"]["items"],0,17),["removeTracks"]=List(Text("^[a-zA-Z0-9_]{1,32}$",32),0,17),["duration"]=Number(.1,30),["loop"]=new JObject {["type"]="boolean"}}),"object.recipe","revision",new JObject {["target"]="target"},"duration","loop");schema["x-features"]=new JArray("recipeEdits.v1","actionResults.v1");return schema;}
        }
        public override JObject OutputSchema=>Object(new JObject {["target"]=Text("^[a-fA-F0-9]{32}$",32),["revision"]=Revision(),["parts"]=Number(1,32,true),["tracks"]=Number(0,17,true),["duration"]=Number(.1,30),["loop"]=new JObject {["type"]="boolean"},["playing"]=new JObject {["type"]="boolean"},["autoplay"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["target"]=new string('0',32),["revision"]=1,["parts"]=new JArray(),["removeParts"]=new JArray(),["tracks"]=new JArray(),["removeTracks"]=new JArray(),["duration"]=2,["loop"]=false};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out _,out error)&&context.Editor.PrepareRecipeEdit((string)args["target"],(int)args["revision"],args,out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            string target=(string)args["target"];int revision=(int)args["revision"];var captured=(JObject)args.DeepClone();
            RoomObjectData prepared=null;RoomEditor.PendingPersistedEdit accepted=null;
            return PersistedEditOperation.Start(context.Editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{
                    bool started=context.Editor.BeginRecipeEdit(target,revision,captured,out prepared,out edit,out issue);accepted=edit;return started;
                },out operation,out error,validate:(out string issue)=>context.Editor.CanEditRecipe(target,revision,out issue),
                output:()=>RecipeEditFacts.Summary(prepared,accepted.AcceptedObjectRevision(target),false));
        }
    }
}
