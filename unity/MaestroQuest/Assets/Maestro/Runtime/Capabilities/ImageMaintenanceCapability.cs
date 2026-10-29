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
    internal sealed class ImageMaintenanceCapability:CapabilityModule
    {
        internal const string Feature="imageLibraryMaintenance.v1";
        readonly bool inspect;
        internal ImageMaintenanceCapability(bool inspect=false){this.inspect=inspect;}
        public override string Id=>inspect?"image.library.inspect":"image.library.edit";
        public override string Label=>inspect?"Inspect an image download":"Remove an unused image download";
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>120;
        public override string Ownership=>inspect?"none":"imageLibraryWrite";
        public override string Description=>inspect?
            "Inspect one exact image content hash, its bounded private payload/name metadata, and current, Undo/Redo, saved/recovery, module, remembered and live-program references. Also reports pending decode/texture users. Reads retained files off-thread without repairing them. inspectionRevision fingerprints exact inspected file content/presence, not deletion permission; removal always checks again. Unavailable evidence protects the download. Read image.library for imported hashes first. The passive image.library.review fact keeps only the last bounded file inspection, never a fresh safety proof.":
            "Remove only an unused image's private payload after image.library.inspect. Pass its exact imageHash and inspectionRevision. Current or unbound appearances, Undo/Redo, saved/recovery data, reusable programs/modules, remembered or live values, and loading/rendering pins protect the bytes; uncertain evidence also refuses deletion. Checks repeat under a workspace edit hold and per-asset admission fence immediately before dispatch. Name/provenance metadata, identity and source export remain; exact reimport restores the hash. No Undo. Request removal only when the user asked for it; inspection never authorizes deletion. Existing library facts become refresh-needed. Stop before dispatch prevents removal; after dispatch the operation drains and may delete after Stop. An uncertain result requires inspecting the file and historical receipt before another explicit request. Historical replay never deletes again.";
        static JObject HashSchema()=>Text("^[a-f0-9]{64}$",64);
        static JObject Bool()=>new(){["type"]="boolean"};
        public override JObject InputSchema {get{
            var schema=Object(new JObject {["imageHash"]=HashSchema()});
            if(!inspect){
                ((JObject)schema["properties"])["inspectionRevision"]=HashSchema();((JArray)schema["required"]).Add("inspectionRevision");
                CurrentInputs(schema,"image.library.review","inspectionRevision",new JObject {["imageHash"]="imageHash"});
                schema["x-confirmation"]="Remove this unused image's local download? Its identity and name metadata remain. Reimport the original image to restore its bytes. There is no Undo, and Stop cannot reverse a deletion already started.";
            }
            schema["x-features"]=new JArray(Feature);return schema;
        }}
        static JObject EntrySchema()=>Object(new JObject {["imageHash"]=HashSchema(),["inspectionRevision"]=HashSchema(),["name"]=Text("^[^\\u0000-\\u001f]{0,80}$",80),["payloadPresent"]=Bool(),["metadataPresent"]=Bool(),["contentValid"]=Bool(),["payloadBytes"]=Number(0,SurfaceImage.MaximumBytes,true)});
        static JObject UsageSchema()=>Object(new JObject {["current"]=Bool(),["history"]=Bool(),["saved"]=Bool(),["runtime"]=Bool(),["inUse"]=Bool(),["uncertain"]=Bool(),["canRemove"]=Bool(),["protection"]=Text("^.{0,512}$",512)});
        public override JObject OutputSchema=>inspect?Object(new JObject {["entry"]=EntrySchema(),["usage"]=UsageSchema()}):Object(new JObject {["removed"]=Bool(),["previousRevision"]=HashSchema(),["entry"]=EntrySchema()});
        public override JObject Example=>inspect?new JObject {["imageHash"]=new string('0',64)}:new JObject {["imageHash"]=new string('0',64),["inspectionRevision"]=new string('0',64)};
        static bool Available(RoomEditor editor,RuleWorkshop rules,bool editing,bool ownsFreeze,out string error){
            error="The image library is unavailable";if(!editor||!rules||editor.Images==null)return false;
            if(editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen&&!ownsFreeze){error="Resume the current workspace before inspecting or removing image downloads";return false;}
            if(editing&&!editor.CanSaveRoom){error="Resolve saved room recovery before removing image downloads";return false;}
            error=null;return true;
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            var editor=context.Editor;var rules=editor?editor.GetComponent<RuleWorkshop>():null;
            if(!Available(editor,rules,!inspect,false,out error))return false;
            if(!inspect&&editor.Images.Review((string)args["imageHash"])?.Revision!=(string)args["inspectionRevision"]){error="Inspect this image and load its current file revision before removing the download";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error))return false;operation=new Maintenance(context,runId,inspect,(JObject)args.DeepClone());return true;
        }
        internal static JObject EntryValue(ImageFileReview value)=>new(){["imageHash"]=value.Hash,["inspectionRevision"]=value.Revision,["name"]=value.Name,["payloadPresent"]=value.PayloadPresent,["metadataPresent"]=value.MetadataPresent,["contentValid"]=value.ContentValid,["payloadBytes"]=value.Bytes};
        static JObject Usage(RoomEditor editor,RuleWorkshop rules,string hash,string runId,SavedAssetReferenceProof retained,bool present){
            bool current=editor.UsesImage(hash),history=editor.HistoricalImage(hash),runtime=rules.RetainsAssetIdentity(hash,runId,out bool unknownRuntime),used=editor.Images.Assets.Pinned(ImportedAssetKind.Image,hash)>0;
            bool unknown=retained==null||!retained.Ready||retained.Uncertain||unknownRuntime||editor.TemporarySavePending||editor.TemporaryStorageUncertain;
            string reason=unknown?"A retained or running reference could not be checked. Keep this download until its data is available.":
                current?"A current or saved appearance still uses this image, including unbound definitions.":
                history?"Undo or Redo still needs this image. Keep its download until those history entries expire.":
                retained.Retained?"A retained save, recovery copy, module or remembered value still needs this image.":
                runtime?"A program, reusable module or live value still needs this image.":
                used?"Release this image's live users and wait for loading to finish.":
                !present?"This image download is already absent.":"";
            return new JObject {["current"]=current,["history"]=history,["saved"]=retained?.Retained??false,["runtime"]=runtime,["inUse"]=used,["uncertain"]=unknown,["canRemove"]=reason.Length==0,["protection"]=reason};
        }
        sealed class Maintenance:CapabilityOperation
        {
            readonly CancellationTokenSource stop=new();readonly Task<JObject> task;bool closed;
            internal Maintenance(CapabilityContext context,string runId,bool inspect,JObject args){task=Run(context,runId,inspect,args);_=Close();}
            async Task Close(){try{await task;}catch{}finally{closed=true;stop.Dispose();}}
            async Task<JObject> Run(CapabilityContext context,string runId,bool inspect,JObject args){
                var editor=context.Editor;var rules=editor.GetComponent<RuleWorkshop>();string hash=(string)args["imageHash"];IDisposable saves=null;
                string Current(){stop.Token.ThrowIfCancellationRequested();if(context.Editor!=editor||!editor||!rules)return "The workspace changed";return Available(editor,rules,!inspect,!inspect,out var error)?null:error;}
                async Task<SavedAssetReferenceProof> References(){
                    if(saves==null){saves=editor.HoldSaveDispatch(out var dispatched,out var error);if(saves==null)throw new InvalidOperationException(error);await dispatched;}
                    stop.Token.ThrowIfCancellationRequested();return await Task.Run(()=>SavedAssetReferences.ReadImages(editor.SaveDirectory,hash,stop.Token),stop.Token);
                }
                try{
                    if(inspect){
                        using var hold=editor.WriteGate.Write();var entry=await editor.Images.InspectFileAsync(hash,stop.Token);var retained=await References();
                        string error=Current();if(error!=null)throw new InvalidOperationException(error);
                        return new JObject {["entry"]=EntryValue(entry),["usage"]=Usage(editor,rules,hash,runId,retained,entry.PayloadPresent)};
                    }
                    async Task<string> Protect(){
                        string issue=Current();if(issue!=null)return issue;var retained=await References();issue=Current();if(issue!=null)return issue;
                        string reason=(string)Usage(editor,rules,hash,runId,retained,true)["protection"];return reason.Length==0?null:reason;
                    }
                    var removed=await editor.Images.RemoveDownloadAsync(hash,(string)args["inspectionRevision"],Protect,stop.Token);
                    return new JObject {["removed"]=true,["previousRevision"]=(string)args["inspectionRevision"],["entry"]=EntryValue(removed)};
                }finally{saves?.Dispose();}
            }
            public override float Seconds=>0;
            public override RuleActionState State(out string error){
                error=null;if(!task.IsCompleted)return RuleActionState.Preparing;
                if(task.IsFaulted||task.IsCanceled){var cause=task.Exception?.GetBaseException();error=cause is InvalidOperationException||cause is System.IO.InvalidDataException?cause.Message:"Image maintenance did not finish. Inspect the current file and retained receipt before another request";return RuleActionState.Failed;}return RuleActionState.Ready;
            }
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?(JObject)task.Result.DeepClone():new JObject();
            public override void Stop(bool preservePlacement){if(!closed)stop.Cancel();}
            public override string InterruptionStatus=>"Image maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.";
        }
        static readonly ProgramDataType reviewType=ProgramDataType.Read(JObject.Parse("{\"record\":{\"imageHash\":\"text\",\"inspectionRevision\":\"text\",\"name\":\"text\",\"payloadPresent\":\"boolean\",\"metadataPresent\":\"boolean\",\"contentValid\":\"boolean\",\"payloadBytes\":\"number\"}}"));
        internal static BehaviourCatalog.FactDefinition ReviewFact()=>new("image.library.review",reviewType,"Inspected image file","Last bounded file inspection for this exact imageHash. Run image.library.inspect first. inspectionRevision guards file identity/presence for image.library.edit; it is not proof that deletion is currently safe. Import or removal invalidates older cached inspections. Missing/evicted inspections are unavailable. This passive fact never reads files, repairs data or checks retained references.",Object(new JObject {["imageHash"]=HashSchema()}),new JObject {["imageHash"]=new string('0',64)},(c,a)=>{var value=c.Editor?c.Editor.Images?.Review((string)a["imageHash"]):null;return value==null?null:ProgramValue.Literal(EntryValue(value),reviewType);},features:new[]{Feature});
    }
}
