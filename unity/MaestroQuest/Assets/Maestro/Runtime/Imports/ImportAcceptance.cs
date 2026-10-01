// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Imports
{
    public sealed partial class ImportWorkshop
    {
        JObject selectionResult;
        static JObject EmptyImportResult()=>new() {["destination"]="",["objectId"]="",["revision"]=0,["temporary"]=false,["motionIds"]=new JArray()};
        internal bool CanAcceptSelection(string id,string hash,string destination,int revision,out string error,bool manual=false)
        {
            error="Inspect the current ready model preview before accepting it";
            if(disposed||Busy||!HasPreview||selectionPhase!="preview"||id!=selectionId||hash!=pending.Hash)return false;
            if(!isActiveAndEnabled||selectionPaused||!selectionFocused||editor.RuntimeGate.Held||editor.Ownership.Suspended){error="Resume the room before accepting the model";return false;}
            if(editor.WriteGate.Frozen||!editor.CanSaveRoom){error="Room saving is unavailable";return false;}
            if(editor.AnyHeld||editor.DrawingInProgress){error="Release objects and finish drawing before accepting the model";return false;}
            if(animationWorkshop&&(animationWorkshop.ControlsTarget(editor.SelectedId)||animationWorkshop.HasUnsavedPose||animationWorkshop.HasUnsavedRecording)){error="Finish authoring and resolve retained poses or takes first";return false;}
            if(destination=="object")return editor.CanCreatePrimitive(out error);
            if(destination=="maestro"){
                if(!preview.IsHumanoid){error=preview.HumanoidIssue??"Choose a valid humanoid for Maestro";return false;}
                if(!editor.CanSelectMaestroModel(hash,revision,out error))return false;
                return editor.Ownership.CanAcquire("model-import:"+id,manual?RoomActorRole.Control:RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},out error,replaceControl:manual);
            }
            if(destination=="motions"&&pending.Inspection.Clips==0){error="This preview has no embedded animations";return false;}
            if(destination is not ("motions" or "library")){error="Choose object, maestro, library or motions";return false;}
            error=null;return true;
        }
        internal bool BeginAcceptSelection(string id,string hash,string destination,int revision,CancellationToken cancellation,out Task<JObject> completion,out string error,bool manual=false)
        {
            completion=null;if(!CanAcceptSelection(id,hash,destination,revision,out error,manual))return false;
            var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,selectionCancel.Token);RoomOwnership.Lease owner=null;
            if(destination=="maestro"&&!editor.Ownership.TryAcquire("model-import:"+id,"Import Maestro",manual?RoomActorRole.Control:RoomActorRole.Program,
                new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>linked.Cancel(),out owner,out error,replaceControl:manual)){linked.Dispose();return false;}
            busy=true;selectionPhase="accepting";selectionError="";
            completion=AcceptSelectionAsync(pending,destination,revision,linked,owner);return true;
        }
        async Task<JObject> AcceptSelectionAsync(ModelAsset asset,string destination,int revision,CancellationTokenSource cancellation,RoomOwnership.Lease owner)
        {
            var token=cancellation.Token;string roomSession=editor.TemporarySessionId;Say("Saving the selected "+destination+"…");
            try{
                await Task.Yield();token.ThrowIfCancellationRequested();
                string objectId="";string[] motions=Array.Empty<string>();
                if(destination=="motions"){
                    // Once accepted by the library, this atomic write is allowed to drain.
                    // Cancellation never promises to remove an already committed library entry.
                    var entries=await editor.Motions.ImportAsync(asset.Name,asset.Bytes);motions=entries.Select(e=>e.id).Distinct().ToArray();
                    libraryMode=true;libraryMotionId=entries.FirstOrDefault(e=>!e.Short)?.id;page=0;
                } else {
                    await editor.Models.SaveAsync(asset);token.ThrowIfCancellationRequested();
                    if(!this||disposed||!isActiveAndEnabled||editor.RuntimeGate.Held||editor.TemporarySessionId!=roomSession)throw new OperationCanceledException();
                    if((destination is "object" or "maestro") && (editor.AnyHeld||editor.DrawingInProgress||animationWorkshop&&(animationWorkshop.ControlsTarget(editor.SelectedId)||animationWorkshop.HasUnsavedPose||animationWorkshop.HasUnsavedRecording)))
                        throw new ModelImportException("Room interaction changed while importing. Finish the active edit, then retry this preview.");
                    if(destination=="object"){
                        if(!editor.CreateImportedModel(asset.Hash,out objectId,out var error))throw new ModelImportException(error);
                        editor.Select(editor.Find(objectId));
                    }else if(destination=="maestro"){
                        if(!editor.BeginMaestroModel(asset.Hash,revision,token,out var loading,out var error))throw new ModelImportException(error);
                        if(!await loading)throw new ModelImportException(maestro?maestro.ModelStatus:"The avatar could not be selected");
                        objectId="maestro";editor.Select(editor.Find(objectId));
                    }
                }
                selectionResult=new JObject {["destination"]=destination,["objectId"]=objectId,["revision"]=objectId==""?0:editor.ObjectRevision(objectId),["temporary"]=editor.TemporaryRoom,["motionIds"]=new JArray(motions)};
                selectionPhase="completed";ClearPreview();if(destination=="motions")ShowLibraryDetails();Say("Import completed — ready to use");
                var result=(JObject)selectionResult.DeepClone();result["requestId"]=selectionId;result["modelHash"]=asset.Hash;return result;
            }catch(OperationCanceledException){selectionPhase=HasPreview?"preview":"cancelled";selectionError="Import stopped. A verified library copy may remain; no interrupted placement will restart.";Say(selectionError);return null;}
            catch(Exception ex){selectionPhase=HasPreview?"preview":"failed";selectionError=Bounded(ex is ModelImportException?ex.Message:"Import could not finish. Inspect the preview before retrying; copied library assets may remain.");Say(selectionError);return null;}
            finally{
                owner?.Dispose();cancellation.Dispose();busy=false;
                if(disposed||!this||!isActiveAndEnabled){selectionPhase=selectionResult==null?"cancelled":"completed";ClearPreview();EndSelectionOwner();}
            }
        }
        async Task<bool> AcceptPreviewManually(string destination)
        {
            if(!HasPreview){Say("Choose a ready model preview first");return false;}
            if(!BeginAcceptSelection(selectionId,pending.Hash,destination,editor.ObjectRevision("maestro"),CancellationToken.None,out var completion,out var error,manual:true)){Say(error);return false;}
            return await completion!=null;
        }
    }
}
