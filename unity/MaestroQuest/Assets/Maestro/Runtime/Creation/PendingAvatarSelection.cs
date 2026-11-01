// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // The queue and publication machinery are shared. This adapter retains
        // avatar-specific save-before-display semantics and hidden candidate lifetime.
        internal sealed class PendingModelSelection:IPersistedRoomEdit
        {
            readonly RoomEditor editor;
            readonly RoomJournal journal;
            readonly RoomStorage storage;
            readonly MaestroAvatar avatar;
            readonly string hash,session;
            readonly int revision;
            readonly bool temporary;
            readonly Action<string> checkpoint;
            readonly CancellationTokenSource cancellation=new();
            IDisposable write,dispatch;
            RoomJournal.PreparedEdit edit;
            RoomEditPreparation native;
            RoomObjectData selected;
            Task<RoomStorage.PreparedSave> preparing;
            RoomStorage.PreparedSave prepared;
            RoomStorage.PreparedPublication publication;
            Task<string> saving;
            JObject result;
            bool accepted,applied,ended,success;
            volatile bool released;
            string failure;
            internal PendingModelSelection(RoomEditor editor,string hash,int revision,IDisposable write,Action<string> checkpoint)
            {
                this.editor=editor;journal=editor.journal;storage=editor.storage;avatar=editor.Find("maestro").GetComponent<MaestroAvatar>();
                this.hash=hash;this.revision=revision;this.write=write;this.checkpoint=checkpoint;
                session=editor.TemporarySessionId;temporary=editor.TemporaryRoom;
            }
            internal void Start()=>_=Run();
            public bool Accepted=>accepted;
            public bool Drained=>released;
            internal JObject Result=>(JObject)result?.DeepClone();
            public string InterruptionStatus=>!accepted?null:success?
                "Avatar selection was saved and displayed. Stop cannot reverse it; use Undo or another selection.":
                "Avatar publication was accepted and continues after Stop. The saved or displayed selection may still change; inspect avatar.model before retrying.";
            bool Current(out string error)
            {
                error="The room changed while loading; avatar selection was not saved.";
                if(cancellation.IsCancellationRequested||!editor||!editor.isActiveAndEnabled||!avatar||
                    editor.RuntimeGate.Held||editor.Ownership.Suspended||editor.WriteGate.Frozen||
                    !ReferenceEquals(editor.journal,journal)||editor.TemporarySessionId!=session)return false;
                if(edit!=null&&!edit.Current(journal,allowRuntimeProgress:true))return false;
                if(edit==null&&editor.ObjectRevision("maestro")!=revision)return false;
                if(!editor.CanEditObject("maestro",false,out error))return false;
                if(editor.GetComponent<AnimationWorkshop>()?.HasUnfinishedAuthoring("maestro")==true){
                    error="Finish saving or discard Maestro's unfinished animation before selecting its model";return false;
                }
                error=null;return true;
            }
            async Task Run()
            {
                try{
                    bool ready=await avatar.SelectModel(hash,editor.Models,CommitPrepared,cancellation.Token,()=>failure);
                    if(!ready){
                        failure??=avatar?avatar.ModelStatus:"The avatar runtime closed before display completed";
                        if(accepted&&applied&&!temporary)failure=storage.PreserveUnconfirmedSelection(failure);
                        return;
                    }
                    success=true;
                }catch(Exception){
                    failure=accepted?"Avatar publication was accepted but display completion was not confirmed; inspect or reopen the workspace.":
                        "Avatar selection was not saved; the previous avatar remains.";
                    if(accepted&&!temporary)failure=storage.PreserveUnconfirmedSelection(failure);
                }finally{
                    ended=true;
                    try{if(editor&&failure!=null)editor.SetStatus(failure);}
                    catch(Exception){/* A feedback listener cannot strand the accepted writer. */}
                    finally{
                        try{native?.Dispose();}
                        finally{
                            native=null;edit?.Dispose();edit=null;dispatch?.Dispose();dispatch=null;
                            try{await DrainWorkers().ConfigureAwait(false);}
                            finally{try{write?.Dispose();}finally{write=null;cancellation.Dispose();released=true;}}
                        }
                    }
                }
            }
            async Task<bool> CommitPrepared()
            {
                if(!Current(out failure))return false;
                try{
                    if(!temporary){
                        dispatch=editor.HoldSaveDispatch(out var older,out failure);if(dispatch==null)return false;
                        await older;
                        if(!Current(out failure))return false;
                        editor.CompleteSave();
                        if(!Current(out failure))return false;
                    }
                    selected=editor.Read("maestro");
                    if((selected.modelHash??"")!=hash)selected.walkClip=editor.IncludedAvatar?.Hash==hash?editor.IncludedAvatar.WalkClipIndex+1:0;
                    selected.modelHash=hash.Length==0?null:hash;
                    if(!journal.Prepare(new[]{selected},Array.Empty<string>(),out edit,out failure,scope:RoomEditScope.Content))return false;
                    native=RoomEditPreparation.TryCreate(editor,edit,out failure);if(native==null)return false;
                    if(!temporary){
                        preparing=storage.PrepareSaveAsync(edit.Snapshot(),cancellation.Token);
                        prepared=await preparing;
                        if(!Current(out failure))return false;
                        publication=storage.StartPreparedPublication(prepared,checkpoint,awaitAcceptance:true);
                        while(!publication.AwaitingAcceptance&&!publication.Completion.IsCompleted){
                            if(!Current(out failure))return false;
                            await Task.Yield();
                        }
                        if(publication.Completion.IsCompleted){failure=(await publication.Completion).Error;return false;}
                        if(!Current(out failure)||!edit.ReservePublication(out failure))return false;
                        if(!publication.Authorize()){failure="Avatar publication could not be accepted";return false;}
                        accepted=true;
                        // This future contains only worker work, so even legacy flush
                        // callers cannot wait on a continuation on their own thread.
                        saving=editor.saveTask=ObservePublication(storage,publication.Completion);
                        dispatch.Dispose();dispatch=null;
                        failure=await saving;
                        if(failure!=null)return false;
                    }else{
                        if(!Current(out failure))return false;
                        accepted=true;
                    }
                    // Accepted publishing is irreversible. Pause/Stop may end its
                    // observer, but cannot discard the confirmed selection.
                    if(!editor||!avatar||!ReferenceEquals(editor.journal,journal)||editor.TemporarySessionId!=session){
                        failure="Avatar selection was saved after its runtime closed; reopen the workspace to display it.";return false;
                    }
                    bool advanced=edit.RuntimeProgressChanged;
                    if(!edit.Accept(journal,out failure,allowRuntimeProgress:true)){
                        if(!temporary)failure=storage.PreserveUnconfirmedSelection("Saved selection could not be applied to its journal.");
                        return false;
                    }
                    applied=true;
                    result=new JObject {["target"]="maestro",["modelHash"]=hash,["revision"]=journal.ObjectRevision("maestro"),["temporary"]=temporary};
                    editor.MarkDirty();
                    if(!temporary){editor.dirty=advanced;editor.lastSaveError=null;}
                    editor.ReconcileEdit(edit,native,false);
                    // Capture the receipt before Changed/ModelChanged can author a
                    // later edit. That later edit must not change this result.
                    edit.Dispose();edit=null;
                    native.Dispose();native=null;
                    return true;
                }catch(OperationCanceledException){
                    failure="Avatar selection was cancelled before publication; the previous avatar remains.";return false;
                }catch(Exception){
                    failure=accepted?"Avatar publication was accepted but application was not confirmed; inspect or reopen before retrying.":
                        "Avatar selection was not saved; the previous avatar remains.";
                    if(accepted&&!temporary)failure=storage.PreserveUnconfirmedSelection(failure);
                    return false;
                }finally{
                    if(!accepted)publication?.CancelBeforeStart();
                }
            }
            static async Task<string> ObservePublication(RoomStorage storage,Task<RoomStorage.PublicationResult> task)
            {
                try{
                    var value=await task.ConfigureAwait(false);
                    return value.Saved?null:value.MayHavePublished?storage.PreserveUnconfirmedSelection(value.Error):value.Error;
                }catch(Exception){return storage.PreserveUnconfirmedSelection("The publication worker did not confirm completion.");}
            }
            // Pure worker draining also lets lifecycle checkpoints wait without
            // waiting for an owner-thread model-load or display continuation.
            async Task DrainWorkers()
            {
                if(!accepted){cancellation.Cancel();publication?.CancelBeforeStart();}
                if(publication!=null)try{await publication.Completion.ConfigureAwait(false);}catch(Exception){}
                if(saving!=null)try{await saving.ConfigureAwait(false);}catch(Exception){}
                if(preparing!=null)try{(await preparing.ConfigureAwait(false))?.Dispose();}catch(Exception){}
                prepared?.Dispose();
            }
            internal Task QueueLifecycle()
            {
                if(!accepted)Dispose();
                var snapshot=journal.Snapshot();
                if(accepted&&!applied){
                    var target=snapshot.objects.Single(x=>x.id=="maestro");
                    target.modelHash=selected.modelHash;target.walkClip=selected.walkClip;
                }
                // Never queue the old avatar behind a possibly published new one.
                // Failed/uncertain publication blocks this follow-up through storage.
                return editor.QueueLifecycleSnapshot(snapshot,Task.WhenAll((Task)editor.saveTask??Task.CompletedTask,DrainWorkers()));
            }
            public bool Poll(out bool ready,out string error){ready=ended&&success;error=failure;return !ended||success;}
            public bool Complete(out string error){if(!Poll(out var ready,out error))return false;if(!ready){error="Avatar selection is still loading or saving";return false;}return true;}
            public void Dispose(){if(!ended&&!accepted){failure="Avatar selection cancelled before publication";cancellation.Cancel();publication?.CancelBeforeStart();}}
        }
    }
}
