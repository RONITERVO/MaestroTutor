// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Persistence;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        IPersistedRoomEdit pendingEdit;
        internal bool PendingEditSave=>pendingEdit!=null&&!pendingEdit.Drained;
        // Owner-thread admission belongs to this room, so all capability callers
        // share arrival order. A waiter holds no snapshot, body or write lease.
        readonly LinkedList<EditAdmission> editAdmissions=new();
        internal bool ReserveEdit(out EditAdmission admission,out string error)
        {
            admission=null;
            if(editAdmissions.Count>=Rules.RuleScheduler.MaximumConcurrent){
                error="Too many waiting object edits; wait for one to finish or cancel it";return false;
            }
            admission=new EditAdmission(editAdmissions);error=null;return true;
        }
        internal sealed class EditAdmission:IDisposable
        {
            readonly LinkedListNode<EditAdmission> node;
            internal EditAdmission(LinkedList<EditAdmission> queue)=>node=queue.AddLast(this);
            internal bool Valid=>node.List!=null;
            internal bool First=>Valid&&ReferenceEquals(node.List.First,node);
            public void Dispose(){node.List?.Remove(node);}
        }
        void CancelPendingEdit(PendingPersistedEdit preserveAcceptedAuthoring=null)
        {
            editAdmissions.Clear();
            if(!ReferenceEquals(pendingEdit,preserveAcceptedAuthoring))pendingEdit?.Dispose();
        }

        // Personal stores can share ordered admission and workspace lifetime
        // without becoming room geometry or participating in temporary Undo.
        internal bool BeginPersistedStateEdit(Func<IDisposable,IPersistedRoomEdit> create,
            out IPersistedRoomEdit pending,out string error)
        {
            pending=null;
            if(PendingEditSave){error="Wait for the pending edit to finish or cancel it first";return false;}
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            try{pendingEdit=pending=create(write);write=null;error=null;return true;}
            finally{write?.Dispose();}
        }

        // The common capability path (book, agent and programs) owns this operation.
        // Synchronous API callers retain their existing completion contract.
        internal bool BeginRotation(string id,Quaternion rotation,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)=>
            BeginPlacement(id,data=>data.rotation=rotation,"Object rotated",out pending,out error,publicationCheckpoint);
        internal bool BeginMove(string id,Vector3 position,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)=>
            BeginPlacement(id,data=>data.position=position,"Object moved",out pending,out error,publicationCheckpoint);
        internal bool BeginResize(string id,float scale,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)=>
            BeginPlacement(id,data=>data.scale=scale,"Object resized",out pending,out error,publicationCheckpoint);
        bool BeginPlacement(string id,Action<RoomObjectData> change,string message,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint)
        {
            pending=null;
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!FinishLiquidPour(out error))return false;
            if(!CanEditObject(id,false,out error))return false;
            if(!Frame.Read(Find(id).transform,out _,out _,out _)){error="The object needs a valid uniform room frame before placement";return false;}
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;IDisposable pose=null;var poseOwner=new object();
            try {
                var data=Pose(Read(id),Find(id).transform);change(data);
                if(!journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out error))return false;
                pose=Find(id).GetComponent<RigidRoomItem>()?.HoldForEdit(poseOwner);
                pendingEdit=pending=new PendingPersistedEdit(this,edit,write,pose,
                    ()=>CanChangeObject(id,false,true,false,out var issue,poseOwner)?null:issue,message,true,publicationCheckpoint);
                edit=null;write=null;pose=null;return true;
            } finally {pose?.Dispose();edit?.Dispose();write?.Dispose();}
        }

        delegate bool PrepareContent(out RoomObjectData[] replacements,out string error);
        internal bool BeginPaint(string id,Color color,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)=>
            BeginContentEdit(()=>CanEditObject(id,true,out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{var data=Pose(Read(id),Find(id).transform);values=new[]{data};return PaintAppearance(data,color,out issue);},
                Array.Empty<string>(),"Object painted",out pending,out error,publicationCheckpoint);
        internal bool BeginDelete(string id,out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint=null)=>
            BeginContentEdit(()=>CanDeleteObject(id,out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{values=Array.Empty<RoomObjectData>();issue=null;return true;},new[]{id},"Object deleted — Undo restores it",out pending,out error,publicationCheckpoint);
        bool BeginContentEdit(Func<string> validate,PrepareContent prepare,string[] removals,string message,
            out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint)
        {
            return BeginPreparedEdit(validate,(out RoomJournal.PreparedEdit edit,out string issue)=>{
                edit=null;return prepare(out var replacements,out issue)&&journal.Prepare(replacements,removals,out edit,out issue);
            },message,out pending,out error,publicationCheckpoint);
        }
        delegate bool PrepareJournalEdit(out RoomJournal.PreparedEdit edit,out string error);
        // Resource definitions and object deltas share admission, publication and
        // native completion. Build one candidate only after its queue turn arrives.
        bool BeginPreparedEdit(Func<string> validate,PrepareJournalEdit prepare,string message,
            out PendingPersistedEdit pending,out string error,Action<string> publicationCheckpoint,Func<string> validatePending=null,IDisposable acceptanceLease=null,Func<string> validateApplied=null)
        {
            pending=null;
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!FinishLiquidPour(out error))return false;
            error=validate();if(error!=null)return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;
            try {
                if(!prepare(out edit,out error))return false;
                // Creation/component changes do not hold existing physical poses.
                // Deletion needs only saved identity, including unavailable models.
                pendingEdit=pending=new PendingPersistedEdit(this,edit,write,acceptanceLease,validatePending??validate,message,false,publicationCheckpoint,validateApplied);
                edit=null;write=null;return true;
            } finally {edit?.Dispose();write?.Dispose();}
        }

        // Preparation and final disk publication are detached workers. The owner
        // thread accepts/reconciles once, then observes the same ordered save future.
        // No worker holds a disk coordinator while awaiting native authorization.
        internal sealed class PendingPersistedEdit:IPersistedRoomEdit
        {
            readonly RoomEditor editor;
            readonly RoomJournal journal;
            readonly RoomStorage storage;
            readonly RoomJournal.PreparedEdit edit;
            readonly IDisposable write,pose;
            readonly Func<string> validate,validateApplied;
            readonly Action<string> checkpoint;
            readonly Action afterNativeApply;
            readonly string message;
            readonly bool applyPose,temporary;
            readonly CancellationTokenSource cancellation=new();
            Task<RoomStorage.PreparedSave> preparation;
            Task cleanup;
            Task<string> saving;
            RoomStorage.PreparedSave prepared;
            RoomStorage.PreparedPublication publication;
            bool ended,success,accepted,nativeApplied,committing,released;
            string failure;
            JObject acceptedWorldTime,acceptedWeather;
            internal PendingPersistedEdit(RoomEditor editor,RoomJournal.PreparedEdit edit,IDisposable write,IDisposable pose,Func<string> validate,string message,bool applyPose,Action<string> checkpoint,Func<string> validateApplied=null,Action afterNativeApply=null)
            {
                this.editor=editor;journal=editor.journal;storage=editor.storage;this.edit=edit;this.write=write;
                this.pose=pose;this.validate=validate;this.message=message;this.applyPose=applyPose;temporary=editor.TemporaryRoom;this.checkpoint=checkpoint;this.validateApplied=validateApplied;this.afterNativeApply=afterNativeApply;
            }
            public bool Drained=>released&&(cleanup==null||cleanup.IsCompleted);
            public bool Accepted=>accepted;
            internal int AcceptedLightingRevision=>accepted?edit.AcceptedLightingRevision:0;
            internal int AcceptedVisibilityRevision(string id)=>accepted?edit.AcceptedVisibilityRevision(id):0;
            internal int AcceptedObjectRevision(string id)=>accepted?edit.AcceptedObjectRevision(id):0;
            internal int AcceptedEnvironmentRevision(string id)=>accepted?edit.AcceptedEnvironmentRevision(id):0;
            internal int AcceptedAudioRevision(string id)=>accepted?edit.AcceptedAudioRevision(id):0;
            internal int AcceptedAppearanceRevision(string id)=>accepted?edit.AcceptedAppearanceRevision(id):0;
            internal int AcceptedRegionRevision(string id)=>accepted?edit.AcceptedRegionRevision(id):0;
            internal int AcceptedStructureRevision(string id)=>accepted?edit.AcceptedStructureRevision(id):0;
            internal JObject AcceptedWorldTimeObservation=>(JObject)acceptedWorldTime?.DeepClone();
            internal JObject AcceptedWeatherObservation=>(JObject)acceptedWeather?.DeepClone();
            internal bool Published=>edit.HasTemporalCheckpoint?saving?.IsCompletedSuccessfully==true&&saving.Result==null:
                publication?.Completion.IsCompletedSuccessfully==true&&publication.Completion.Result.Saved;
            public string InterruptionStatus=>accepted?
                temporary?"The temporary room edit was accepted. Stopping cannot undo it; use Undo or discard the temporary room. Keep a snapshot to save it.":
                Published?"The room edit was accepted and saved. Stopping cannot retract it; inspect the room before retrying.":
                "The live room edit was accepted. Saving is still pending or unconfirmed; stopping cannot retract it. Inspect its outcome before retrying.":null;
            string CurrentIssue()
            {
                if(!editor||!editor.isActiveAndEnabled||(applyPose&&!editor.Frame.Valid)||editor.Ownership.Suspended||editor.WriteGate.Frozen||
                    editor.saveDispatch.Frozen||!ReferenceEquals(editor.journal,journal)||editor.TemporaryRoom!=temporary)
                    return "The room edit ended because its workspace is no longer available";
                if(!edit.Current(journal,allowRuntimeProgress:true))return "The room changed while the edit was being prepared; inspect it before retrying";
                return validate();
            }
            // Preparation is observable without applying anything. Used by Poll and
            // by native callers that need an explicit preparation/acceptance boundary.
            internal bool Prepare(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(ended)return success;
                if(accepted){ready=true;return true;}
                error=CurrentIssue();if(error!=null)return Fail(error);
                if(temporary){ready=true;return true;}
                if(editor.saveTask!=null&&!editor.saveTask.IsCompleted)return true;
                editor.CompleteSave();
                // A save-status observer can stop or change the operation.
                if(ended){error=failure;return success;}
                error=CurrentIssue();if(error!=null)return Fail(error);
                // Clock position and fade origin are sampled when accepted, not
                // backdated to a detached worker's earlier snapshot. No IO is awaited here.
                if(edit.HasTemporalCheckpoint){ready=true;return true;}
                if(preparation==null)preparation=storage.PrepareSaveAsync(edit.Snapshot(),cancellation.Token);
                if(!preparation.IsCompleted)return true;
                if(preparation.IsCanceled||preparation.IsFaulted){
                    error=preparation.Exception?.GetBaseException().Message??"Room save preparation was cancelled";return Fail(error);
                }
                prepared=preparation.Result;
                publication??=storage.StartPreparedPublication(prepared,checkpoint,awaitAcceptance:true);
                if(publication.Completion.IsCompleted){
                    try{error=publication.Completion.GetAwaiter().GetResult().Error??"Room publication ended before native acceptance";}
                    catch(Exception){error="Room publication preparation failed; no native edit was accepted.";}
                    return Fail(error);
                }
                ready=publication.AwaitingAcceptance;return true;
            }
            // State polling advances the completion action. Ready is a confirmed
            // native + durable outcome, never merely a dispatched worker.
            public bool Poll(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(committing){error=null;return true;}
                if(ended){ready=success;return success;}
                if(!accepted){
                    if(!Prepare(out var preparedReady,out error))return false;
                    if(!preparedReady)return true;
                    if(!AcceptPrepared(out error))return false;
                }
                if(!temporary&&!saving.IsCompleted)return true;
                if(!temporary){
                    error=saving.GetAwaiter().GetResult();
                    if(error!=null)return Fail(error);
                }
                if(!nativeApplied)return Fail(error="The edit was accepted but native completion was not confirmed. Inspect the room before retrying.");
                success=true;End();ready=true;
                if(editor&&editor.isActiveAndEnabled)editor.SetStatus(message);
                return true;
            }
            bool AcceptPrepared(out string error)
            {
                committing=true;error=null;
                try {
                    using var native=RoomEditPreparation.TryCreate(editor,edit,out error);
                    if(native==null)return Fail(error);
                    error=CurrentIssue();if(error!=null)return Fail(error);
                    bool runtimeChanged=edit.RuntimeProgressChanged;
                    if(!edit.Accept(journal,out error,allowRuntimeProgress:true))return Fail(error);
                    accepted=true;
                    if(edit.HasTemporalCheckpoint){acceptedWorldTime=editor.ObserveWorldTime();acceptedWeather=editor.ObserveWeather();}
                    if(!temporary){
                        // Install the future and authorize before any Changed callback:
                        // a synchronous flush may wait on it, never on this Unity thread.
                        saving=editor.saveTask=edit.HasTemporalCheckpoint?
                            RoomCheckpointSave.Start(storage,edit.AcceptedTemporalSnapshot(),checkpoint):RoomCheckpointSave.Observe(storage,publication.Completion);
                        if(!edit.HasTemporalCheckpoint&&!publication.Authorize()){
                            publication.CancelBeforeStart();
                            throw new InvalidOperationException("Prepared publication lost acceptance ownership");
                        }
                    }
                    editor.MarkDirty();
                    if(!temporary){editor.dirty=runtimeChanged;editor.lastSaveError=null;}
                    editor.ReconcileEdit(edit,native,applyPose);
                    // Explicit native repair belongs to acceptance, not a later
                    // receipt observer which can stop while publication drains.
                    afterNativeApply?.Invoke();
                    // Some native adapters report readiness instead of throwing.
                    // An accepted save must never hide a failed native application.
                    error=validateApplied?.Invoke();if(error!=null)return Fail(error);
                    nativeApplied=true;
                    // The target can resume immediately after application; storage
                    // only owns detached data. End/Stop can dispose this lease again.
                    ReleaseResources();
                    return true;
                } catch(Exception) {
                    if(accepted){
                        if(!temporary&&publication!=null&&!publication.Started)publication.CancelBeforeStart();
                        error="The live edit was accepted but its native completion could not be confirmed. Inspect or reload the room before retrying.";
                    }else error="The room edit could not complete; no prepared change was accepted.";
                    return Fail(error);
                } finally {committing=false;}
            }
            public bool Complete(out string error)
            {
                if(committing){error="The room edit is already completing";return false;}
                if(!Poll(out var ready,out error))return false;
                if(!ready){error="Room edit is still preparing or saving";return false;}
                return true;
            }
            bool Fail(string error){success=false;failure=error??"Room edit failed";End();return false;}
            void End()
            {
                if(ended)return;ended=true;
                ReleaseResources();
            }
            void ReleaseResources()
            {
                if(released)return;released=true;
                if(!accepted)publication?.CancelBeforeStart();
                try {pose?.Dispose();}
                finally {
                    edit.Dispose();
                    // Retirement may destroy the editor now. Cleanup owns only pure
                    // data, cancellation and the workspace directory's write lease.
                    cleanup=Release(preparation,prepared,publication?.Completion,saving,write,cancellation);
                }
            }
            static async Task Release(Task<RoomStorage.PreparedSave> task,RoomStorage.PreparedSave prepared,
                Task<RoomStorage.PublicationResult> publication,Task<string> saving,IDisposable write,CancellationTokenSource cancellation)
            {
                cancellation.Cancel();
                try {
                    if(publication!=null)try{await publication.ConfigureAwait(false);}catch(Exception){}
                    if(saving!=null)try{await saving.ConfigureAwait(false);}catch(Exception){}
                    if(task!=null)try{(await task.ConfigureAwait(false))?.Dispose();}catch(Exception){}
                    prepared?.Dispose();
                }
                finally {write.Dispose();cancellation.Dispose();}
            }
            public void Dispose()
            {
                if(!ended&&!committing)Fail(accepted?
                    "The room edit was stopped after native acceptance. Saving continues; inspect the result before retrying.":
                    "Room edit was cancelled before native acceptance");
            }
        }
    }
}
