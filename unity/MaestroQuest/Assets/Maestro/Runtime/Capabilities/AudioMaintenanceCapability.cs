// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class AudioMaintenanceCapability:CapabilityModule
    {
        internal const string Feature="audioLibraryMaintenance.v1";
        readonly bool inspect;
        internal AudioMaintenanceCapability(bool inspect=false){this.inspect=inspect;}
        public override string Id=>inspect?"audio.library.inspect":"audio.library.edit";
        public override string Label=>inspect?"Inspect a sound download":"Remove an unused sound download";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>120;
        public override string Ownership=>inspect?"none":"audioLibraryWrite";
        public override string Description=>inspect?
            "Inspect one exact sound content hash, its bounded private payload/name metadata, and current, Undo/Redo, saved/recovery, module, remembered and live-program references. Also reports pending decode, playback and streaming users. Reads retained files off-thread without repairing them. inspectionRevision fingerprints exact inspected file content/presence, not deletion permission; removal always checks again. Unavailable evidence protects the download. Read audio.library for imported hashes first. The passive audio.library.review fact keeps only the last bounded file inspection, never a fresh safety proof.":
            "Remove only an unused sound's private payload after audio.library.inspect. Pass its exact assetHash and inspectionRevision. Current or unbound clip sources, Undo/Redo, saved/recovery data, reusable programs/modules, remembered or live values, and loading/playback pins protect the bytes; uncertain evidence also refuses deletion. Checks repeat under a workspace edit hold and per-asset admission fence immediately before dispatch. The complete display-name sidecar, identity and source export remain; reimporting the exact canonical stored WAV restores its hash. MP3/Ogg input hashes differ from normalized WAV hashes; future decoding is not promised to reproduce identical bytes. A bounded damaged payload can be removed only after the same exact revision and unused-reference proof; contentValid:false alone is not permission. No Undo. Request removal only when the user asked for it; inspection never authorizes deletion. Existing library facts become refresh-needed. Stop before dispatch prevents removal; after dispatch the operation drains and may delete after Stop. An uncertain result requires inspecting the file and historical receipt before another explicit request. Historical replay never deletes again.";
        static JObject HashSchema()=>Text("^[a-f0-9]{64}$",64);
        static JObject Bool()=>new(){["type"]="boolean"};
        public override JObject InputSchema {get{
            var schema=Object(new JObject {["assetHash"]=HashSchema()});
            if(!inspect){
                ((JObject)schema["properties"])["inspectionRevision"]=HashSchema();((JArray)schema["required"]).Add("inspectionRevision");
                CurrentInputs(schema,"audio.library.review","inspectionRevision",new JObject {["assetHash"]="assetHash"});
                schema["x-confirmation"]="Remove this unused sound's local download? Its identity and name metadata remain. Reimport the exact stored WAV to restore its bytes; MP3/Ogg decoding may produce a different hash. There is no Undo, and Stop cannot reverse a deletion already started.";
            }
            schema["x-features"]=new JArray(Feature);return schema;
        }}
        static JObject EntrySchema()=>Object(new JObject {["assetHash"]=HashSchema(),["inspectionRevision"]=HashSchema(),["name"]=Text("^[^\\u0000-\\u001f]{0,80}$",80),["payloadPresent"]=Bool(),["metadataPresent"]=Bool(),["contentValid"]=Bool(),["payloadBytes"]=Number(0,WaveAudio.MaximumBytes,true)});
        static JObject UsageSchema()=>Object(new JObject {["current"]=Bool(),["history"]=Bool(),["saved"]=Bool(),["runtime"]=Bool(),["inUse"]=Bool(),["uncertain"]=Bool(),["canRemove"]=Bool(),["protection"]=Text("^.{0,512}$",512)});
        public override JObject OutputSchema=>inspect?Object(new JObject {["entry"]=EntrySchema(),["usage"]=UsageSchema()}):Object(new JObject {["removed"]=Bool(),["previousRevision"]=HashSchema(),["entry"]=EntrySchema()});
        public override JObject Example=>inspect?new JObject {["assetHash"]=new string('0',64)}:new JObject {["assetHash"]=new string('0',64),["inspectionRevision"]=new string('0',64)};
        static bool Available(RoomEditor editor,RuleWorkshop rules,bool editing,bool ownsFreeze,out string error){
            error="The sound library is unavailable";if(!editor||!rules||editor.Sounds==null)return false;
            if(editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen&&!ownsFreeze){error="Resume the current workspace before inspecting or removing sound downloads";return false;}
            if(editing&&!editor.CanSaveRoom){error="Resolve saved room recovery before removing sound downloads";return false;}
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            var editor=context.Editor;var rules=editor?editor.GetComponent<RuleWorkshop>():null;
            if(!Available(editor,rules,!inspect,false,out error))return false;
            if(!inspect&&editor.Sounds.Review((string)args["assetHash"])?.Revision!=(string)args["inspectionRevision"]){error="Inspect this sound and load its current file revision before removing the download";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;operation=new Maintenance(context,runId,inspect,(JObject)args.DeepClone());return true;
        }
        internal static JObject EntryValue(AudioFileReview value)=>new(){["assetHash"]=value.Hash,["inspectionRevision"]=value.Revision,["name"]=value.Name,["payloadPresent"]=value.PayloadPresent,["metadataPresent"]=value.MetadataPresent,["contentValid"]=value.ContentValid,["payloadBytes"]=value.Bytes};
        static JObject Usage(RoomEditor editor,RuleWorkshop rules,string hash,string runId,SavedAssetReferenceProof retained,bool present){
            bool current=editor.UsesAudio(hash),history=editor.HistoricalAudio(hash),runtime=rules.RetainsAssetIdentity(hash,runId,out bool unknownRuntime),used=editor.Sounds.Assets.Pinned(ImportedAssetKind.Audio,hash)>0;
            bool unknown=retained==null||!retained.Ready||retained.Uncertain||unknownRuntime||editor.TemporarySavePending||editor.TemporaryStorageUncertain;
            string reason=unknown?"A retained or running reference could not be checked. Keep this download until its data is available.":
                current?"A current or saved clip source still uses this sound, including unbound definitions.":
                history?"Undo or Redo still needs this sound. Keep its download until those history entries expire.":
                retained.Retained?"A retained save, recovery copy, module or remembered value still needs this sound.":
                runtime?"A program, reusable module or live value still needs this sound.":
                used?"Release this sound's live users and wait for loading to finish.":
                !present?"This sound download is already absent.":"";
            return new JObject {["current"]=current,["history"]=history,["saved"]=retained?.Retained??false,["runtime"]=runtime,["inUse"]=used,["uncertain"]=unknown,["canRemove"]=reason.Length==0,["protection"]=reason};
        }
        sealed class Maintenance:CapabilityOperation
        {
            readonly CancellationTokenSource stop=new();readonly Task<JObject> task;bool closed;
            internal Maintenance(CapabilityContext context,string runId,bool inspect,JObject args){task=Run(context,runId,inspect,args);_=Close();}
            async Task Close(){try{await task;}catch{}finally{closed=true;stop.Dispose();}}
            async Task<JObject> Run(CapabilityContext context,string runId,bool inspect,JObject args){
                var editor=context.Editor;var rules=editor.GetComponent<RuleWorkshop>();string hash=(string)args["assetHash"];IDisposable saves=null;
                string Current(){stop.Token.ThrowIfCancellationRequested();if(context.Editor!=editor||!editor||!rules)return "The workspace changed";return Available(editor,rules,!inspect,!inspect,out var error)?null:error;}
                async Task<SavedAssetReferenceProof> References(){
                    if(saves==null){saves=editor.HoldSaveDispatch(out var dispatched,out var error);if(saves==null)throw new InvalidOperationException(error);await dispatched;}
                    stop.Token.ThrowIfCancellationRequested();return await Task.Run(()=>SavedAssetReferences.ReadAudio(editor.SaveDirectory,hash,stop.Token),stop.Token);
                }
                try{
                    if(inspect){
                        using var hold=editor.WriteGate.Write();var entry=await editor.Sounds.InspectFileAsync(hash,stop.Token);var retained=await References();
                        string error=Current();if(error!=null)throw new InvalidOperationException(error);
                        return new JObject {["entry"]=EntryValue(entry),["usage"]=Usage(editor,rules,hash,runId,retained,entry.PayloadPresent)};
                    }
                    async Task<string> Protect(){
                        string issue=Current();if(issue!=null)return issue;var retained=await References();issue=Current();if(issue!=null)return issue;
                        string reason=(string)Usage(editor,rules,hash,runId,retained,true)["protection"];return reason.Length==0?null:reason;
                    }
                    var removed=await editor.Sounds.RemoveDownloadAsync(hash,(string)args["inspectionRevision"],Protect,stop.Token);
                    return new JObject {["removed"]=true,["previousRevision"]=(string)args["inspectionRevision"],["entry"]=EntryValue(removed)};
                }finally{saves?.Dispose();}
            }
            public override float Seconds=>0;
            public override RuleActionState State(out string error){
                error=null;if(!task.IsCompleted)return RuleActionState.Preparing;
                if(task.IsFaulted||task.IsCanceled){var cause=task.Exception?.GetBaseException();error=cause is InvalidOperationException||cause is System.IO.InvalidDataException?cause.Message:"Sound maintenance did not finish. Inspect the current file and retained receipt before another request";return RuleActionState.Failed;}return RuleActionState.Ready;
            }
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?(JObject)task.Result.DeepClone():new JObject();
            public override void Stop(bool preservePlacement){if(!closed)stop.Cancel();}
            public override string InterruptionStatus=>"Sound maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.";
        }
        static readonly ProgramDataType reviewType=ProgramDataType.Read(JObject.Parse("{\"record\":{\"assetHash\":\"text\",\"inspectionRevision\":\"text\",\"name\":\"text\",\"payloadPresent\":\"boolean\",\"metadataPresent\":\"boolean\",\"contentValid\":\"boolean\",\"payloadBytes\":\"number\"}}"));
        internal static BehaviourCatalog.FactDefinition ReviewFact()=>new("audio.library.review",reviewType,"Inspected sound file","Last bounded file inspection for this exact assetHash. Run audio.library.inspect first. inspectionRevision guards file identity/presence for audio.library.edit; it is not proof that deletion is currently safe. Import or removal invalidates older cached inspections. Missing/evicted inspections are unavailable. This passive fact never reads files, repairs data or checks retained references.",Object(new JObject {["assetHash"]=HashSchema()}),new JObject {["assetHash"]=new string('0',64)},(c,a)=>{var value=c.Editor?c.Editor.Sounds?.Review((string)a["assetHash"]):null;return value==null?null:ProgramValue.Literal(EntryValue(value),reviewType);},features:new[]{Feature});
    }
}
