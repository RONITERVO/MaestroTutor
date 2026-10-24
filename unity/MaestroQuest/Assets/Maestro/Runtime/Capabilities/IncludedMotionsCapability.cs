// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class IncludedMotionsCapability:CapabilityModule
    {
        public override string Id=>"motion.pack.install";
        public override string Label=>"Add included animations";
        public override string Duration=>"completion";
        public override string Ownership=>"motionLibraryWrite";
        internal override float CompletionTimeoutSeconds=>300;
        public override string Description=>"Copy the exact offline animation package into the existing private library. Read motion.pack.included for package.manifestHash first. add only adds missing content; matching saved motions keep their existing IDs, names, tags, favourites and archived/removed choices. restore requires an exact existing motionId whose payload is in this package; it restores only that download and keeps its metadata and archive state. New content receives the package's stable IDs; a changed clip never replaces a saved one. First use of a new library adds the package automatically; app updates never silently install into existing libraries. Existing quotas apply without eviction. Payloads are verified one at a time, then one complete catalogue is published. Stop checks between copies; an accepted final save may finish, so inspect the library before retrying. Valid copied bytes can remain after interrupted work, with no new catalogue entries. This writes private assets immediately even in a temporary room; it never assigns a gait, changes the avatar, sets looping, resumes a program or starts playback. Programmed playback uses the existing exact motion IDs and rig checks.";
        static JObject Variant(bool restore){var fields=new JObject {["operation"]=Choice(restore?"restore":"add"),["manifestHash"]=Text("^[a-f0-9]{64}$",64)};fields["operation"]["x-static"]=true;if(restore)fields["motionId"]=Text("^[a-f0-9]{32}$",32);var value=CurrentInputs(Object(fields),"motion.pack.included","manifestHash");value["x-current"]["fields"]["manifestHash"]=new JArray("package","manifestHash");value["title"]=restore?"Restore one included download":"Add missing included motions";value["x-features"]=new JArray("includedMotions.v1");return value;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant(false),Variant(true))};
        public override JObject OutputSchema=>Object(new JObject {["manifestHash"]=Text("^[a-f0-9]{64}$",64),["added"]=Number(0,1024,true),["preserved"]=Number(0,1024,true),["restored"]=Number(0,1,true)});
        public override JObject Example=>new(){["operation"]="add",["manifestHash"]=new string('0',64)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var editor=context.Editor;var library=editor?editor.Motions:null;error="The included animation package is unavailable";
            if(library?.Included==null)return false;
            if(editor.RuntimeGate.Held||editor.WriteGate.Frozen||!editor.CanSaveRoom||library.ReadOnly){error="Resume a writable room before adding included animations";return false;}
            if(library.InstallingIncluded){error="Wait for the current included animation copy to finish";return false;}
            if(library.Included.Hash!=(string)args["manifestHash"]){error="The included package changed. Inspect its current identity";return false;}
            if((string)args["operation"]=="restore"){
                var entry=library.Inspect((string)args["motionId"]);
                if(entry==null||!library.Included.Catalogue.entries.Any(x=>x.hash==entry.hash)){error="This exact saved motion is not in the current included package";return false;}
                if(library.Pinned(entry.id)){error="Stop this motion and wait for its loading to finish before restoring its download";return false;}
            }
            error=null;return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            operation=new Install(context.Editor.Motions,(string)args["manifestHash"],(string)args["operation"]=="restore"?(string)args["motionId"]:null);return true;
        }
        sealed class Install:CapabilityOperation
        {
            readonly CancellationTokenSource stop=new();readonly Task<IncludedMotionResult> task;bool closed;
            internal Install(MotionLibrary library,string hash,string id){task=library.InstallIncludedAsync(hash,id,stop.Token);_=Close();}
            async Task Close(){try{await task;}catch{}finally{closed=true;stop.Dispose();}}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=null;if(!task.IsCompleted)return RuleActionState.Preparing;if(task.IsCanceled||task.IsFaulted){var cause=task.Exception?.GetBaseException();error=cause is ModelImportException?cause.Message:"Animation copy did not finish. Inspect the library before retrying";return RuleActionState.Failed;}return RuleActionState.Ready;}
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?new JObject {["manifestHash"]=task.Result.ManifestHash,["added"]=task.Result.Added,["preserved"]=task.Result.Preserved,["restored"]=task.Result.Restored}:new JObject();
            public override void Stop(bool preservePlacement){if(!closed)stop.Cancel();}
            public override string InterruptionStatus=>"Animation copy stop requested. A dispatched catalogue save may finish; inspect the library. No playback was started.";
        }
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"available\":\"boolean\",\"busy\":\"boolean\",\"status\":\"text\",\"package\":{\"record\":{\"manifestHash\":\"text\",\"packId\":\"text\",\"name\":\"text\",\"revision\":\"number\",\"avatarHash\":\"text\",\"rigHash\":\"text\"}},\"counts\":{\"record\":{\"motions\":\"number\",\"kibibytes\":\"number\",\"catalogued\":\"number\",\"removed\":\"number\"}}}}"));
            return new("motion.pack.included",type,"Included animation package","Read-only offline package identity and library metadata counts. catalogued includes archived/removed matches by exact payload hash, even when a user's earlier import has a different ID. removed counts explicit removed-download records; neither count certifies bytes. Use the existing motion search and detail facts for actual saved IDs and playback availability. Adding content preserves matching choices. Names/status are bounded; first-use copying never plays or assigns motions.",null,null,(context,args)=>{
                var library=context.Editor?context.Editor.Motions:null;var pack=library?.Included;
                var known=library==null?Array.Empty<MotionEntry>():library.List(includeShort:true).Concat(library.List(includeShort:true,archivedOnly:true)).ToArray();var hashes=pack?.Catalogue.entries.Select(x=>x.hash).ToHashSet();
                return ProgramValue.Literal(new JObject {["available"]=pack!=null,["busy"]=library?.InstallingIncluded??false,["status"]=ImportObservation.Text(library?.IncludedStatus??""),
                    ["package"]=new JObject {["manifestHash"]=pack?.Hash??"",["packId"]=pack?.PackId??"",["name"]=ImportObservation.Text(pack?.Name??""),["revision"]=pack?.Revision??0,["avatarHash"]=pack?.AvatarHash??"",["rigHash"]=pack?.RigHash??""},
                    ["counts"]=new JObject {["motions"]=pack?.Count??0,["kibibytes"]=Math.Ceiling((pack?.Bytes??0)/1024d),["catalogued"]=known.Count(x=>hashes?.Contains(x.hash)==true),["removed"]=known.Count(x=>x.removed&&hashes?.Contains(x.hash)==true)}},type);
            });
        }
    }
}
