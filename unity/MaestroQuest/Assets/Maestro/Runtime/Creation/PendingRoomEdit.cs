// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        PendingPersistedEdit pendingPersistedEdit;
        bool PendingEditSave=>pendingPersistedEdit!=null&&!pendingPersistedEdit.Drained;
        void CancelPendingEdit()=>pendingPersistedEdit?.Dispose();

        // The common capability path (book, agent and programs) owns this operation.
        // Synchronous API callers retain their existing completion contract.
        internal bool BeginRotation(string id,Quaternion rotation,out PendingPersistedEdit pending,out string error)
        {
            pending=null;
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!CanEditObject(id,false,out error))return false;
            if(!Frame.Read(Find(id).transform,out _,out _,out _)){error="The object needs a valid uniform room frame before placement";return false;}
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;IDisposable pose=null;var poseOwner=new object();
            try {
                var data=Pose(Read(id),Find(id).transform);data.rotation=rotation;
                if(!journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out error))return false;
                pose=Find(id).GetComponent<RigidRoomItem>()?.HoldForEdit(poseOwner);
                pending=pendingPersistedEdit=new PendingPersistedEdit(this,edit,write,pose,
                    ()=>CanChangeObject(id,false,true,false,out var issue,poseOwner)?null:issue,"Object rotated",true);
                edit=null;write=null;pose=null;return true;
            } finally {pose?.Dispose();edit?.Dispose();write?.Dispose();}
        }

        // Detached document encoding, staging and durable flush run off-thread. Native preparation,
        // conditional publication and journal acceptance form one non-yielding
        // owner-thread commit. Final filesystem publication remains synchronous.
        internal sealed class PendingPersistedEdit:IDisposable
        {
            readonly RoomEditor editor;
            readonly RoomJournal journal;
            readonly RoomStorage storage;
            readonly RoomJournal.PreparedEdit edit;
            readonly IDisposable write,pose;
            readonly Func<string> validate;
            readonly string message;
            readonly bool applyPose,temporary;
            readonly CancellationTokenSource cancellation=new();
            Task<RoomStorage.PreparedSave> preparation;
            Task cleanup;
            RoomStorage.PreparedSave prepared;
            bool ended,success,published,committing;
            string failure;
            internal PendingPersistedEdit(RoomEditor editor,RoomJournal.PreparedEdit edit,IDisposable write,IDisposable pose,Func<string> validate,string message,bool applyPose)
            {
                this.editor=editor;journal=editor.journal;storage=editor.storage;this.edit=edit;this.write=write;
                this.pose=pose;this.validate=validate;this.message=message;this.applyPose=applyPose;temporary=editor.TemporaryRoom;
            }
            internal bool Drained=>ended&&(cleanup==null||cleanup.IsCompleted);
            internal bool Published=>published;
            internal string InterruptionStatus=>published?"The room edit was saved. Stopping cannot retract it; inspect the room before retrying.":committing?"The room edit has begun committing; inspect its outcome before retrying.":null;
            string CurrentIssue()
            {
                if(!editor||!editor.isActiveAndEnabled||!editor.Frame.Valid||editor.Ownership.Suspended||editor.WriteGate.Frozen||
                    !ReferenceEquals(editor.journal,journal)||editor.TemporaryRoom!=temporary)
                    return "The room edit ended because its workspace is no longer available";
                if(!edit.Current(journal,allowRuntimeProgress:true))return "The room changed while the edit was being prepared; inspect it before retrying";
                return validate();
            }
            // Returns false only for a terminal failure. Ready means Complete may
            // attempt publication; it does not mean the room has already changed.
            internal bool Poll(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(ended){ready=success;return success;}
                error=CurrentIssue();if(error!=null)return Fail(error);
                if(temporary){ready=true;return true;}
                if(editor.saveTask!=null&&!editor.saveTask.IsCompleted)return true;
                editor.CompleteSave();
                if(preparation==null)preparation=storage.PrepareSaveAsync(edit.Snapshot(),cancellation.Token);
                if(!preparation.IsCompleted)return true;
                if(preparation.IsCanceled||preparation.IsFaulted){
                    error=preparation.Exception?.GetBaseException().Message??"Room save preparation was cancelled";return Fail(error);
                }
                prepared=preparation.Result;ready=true;return true;
            }
            internal bool Complete(out string error)
            {
                if(ended){error=failure;return success;}
                if(committing){error="The room edit is already completing";return false;}
                if(!Poll(out var ready,out error))return false;
                if(!ready){error="Room edit is still preparing";return false;}
                committing=true;
                try {
                    using var native=RoomEditPreparation.TryCreate(editor,edit,out error);
                    if(native==null)return Fail(error);
                    error=CurrentIssue();if(error!=null)return Fail(error);
                    bool runtimeChanged=edit.RuntimeProgressChanged;
                    if(!temporary){
                        if(!storage.PublishPrepared(prepared,out error))return Fail(error);
                        published=true;
                    }
                    if(!edit.Accept(journal,out error,allowRuntimeProgress:true))return Fail(error);
                    // Publish revision/save status before native Changed callbacks.
                    // A callback can capture a newer viewpoint or make another edit;
                    // its dirty state must not be cleared by this older save.
                    editor.MarkDirty();
                    if(!temporary){editor.dirty=runtimeChanged;editor.lastSaveError=null;}
                    editor.Reconcile(edit.ChangedObjects,applyPose,preparation:native);
                    success=true;End();editor.SetStatus(message);return true;
                } catch(Exception) {
                    error=published?"The edit was saved but its native completion could not be confirmed. Reload the room before retrying.":"The room edit could not complete; no prepared change was saved.";
                    return Fail(error);
                } finally {committing=false;}
            }
            bool Fail(string error){success=false;failure=error??"Room edit failed";End();return false;}
            void End()
            {
                if(ended)return;ended=true;
                try {pose?.Dispose();}
                finally {
                    edit.Dispose();
                    // No Unity object is accessed by cleanup. Retirement can destroy
                    // the owner while the detached worker drains, without publication.
                    cleanup=Release(preparation,prepared,write,cancellation);
                }
            }
            static async Task Release(Task<RoomStorage.PreparedSave> task,RoomStorage.PreparedSave prepared,IDisposable write,CancellationTokenSource cancellation)
            {
                cancellation.Cancel();
                try {if(task!=null)try{(await task.ConfigureAwait(false))?.Dispose();}catch(Exception){}prepared?.Dispose();}
                finally {write.Dispose();cancellation.Dispose();}
            }
            public void Dispose(){if(!ended&&!committing)Fail("Room edit was cancelled before publication");}
        }
    }
}
