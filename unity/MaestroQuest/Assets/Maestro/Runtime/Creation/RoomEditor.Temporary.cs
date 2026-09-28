// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class TemporaryRoomView {
        public bool active,pending;
        public string id="",saveId="",phase="idle",error="";
        public int savedRevision;
    }
    public sealed class TemporarySaveReceipt {
        public readonly string Id,SessionId;
        public bool Pending {get;internal set;}=true;
        public string Error {get;internal set;}
        public int SavedRevision {get;internal set;}
        internal TemporarySaveReceipt(string id,string sessionId) {Id=id;SessionId=sessionId;}
    }
    // One shared live fork; catalog actions own public execution and receipts.
    public sealed partial class RoomEditor
    {
        RoomJournal savedJournal;
        Task<string> temporarySave;
        RoomDocument savingSnapshot;
        public bool TemporaryRoom => savedJournal!=null;
        public bool CanSaveRoom=>storage!=null&&!storage.ReadOnly;
        public string TemporarySessionId {get;private set;}=Guid.NewGuid().ToString("N");
        public TemporarySaveReceipt LastTemporarySave {get;private set;}
        public string TemporarySaveId {get;private set;}="";
        public TemporaryRoomView ObserveTemporaryRoom()=>new() {active=TemporaryRoom,pending=TemporarySavePending,id=TemporarySessionId,saveId=TemporarySaveId,savedRevision=TemporarySaveRevision,
            phase=TemporarySavePending?"pending":TemporarySaveError!=null?"failed":TemporarySaveRevision>0?"saved":"idle",error=TemporarySaveError??""};
        internal void PollTemporarySave()=>CompleteTemporarySave();
        public bool TemporarySavePending => temporarySave!=null;
        public string TemporarySaveError {get;private set;}
        public int TemporarySaveRevision {get;private set;}

        internal bool CanChangeTemporaryBoundary(out string error)
        {
            error=null;
            if(journal==null) {error="Room editor is not ready";return false;}
            if(AnyHeld) {error="Release held objects before changing the temporary room";return false;}
            foreach(var item in objects.Values) {
                if(!item)continue;
                var avatar=item.GetComponent<MaestroAvatar>();
                if(avatar && (avatar.ModelBusy || avatar.PoseRig && avatar.PoseRig.IsHolding)) {
                    error="Finish loading and release avatar joints first";return false;
                }
            }
            return true;
        }
        public bool BeginTemporaryRoom(out string error,bool stopAuthoring=true)
        {
            error=null;
            if(TemporaryRoom) {error="A temporary room is already active";return false;}
            if(!CanChangeTemporaryBoundary(out error))return false;
            if(storage.ReadOnly) {error="This room is unavailable for saving";return false;}
            // This is an explicit manual boundary, not a scheduler action. Stop
            // current authoring first so its final frame belongs to the base.
            if(stopAuthoring)Editing?.Invoke();
            if(!CanChangeTemporaryBoundary(out error))return false;
            CapturePhysicsPlacements();
            // Establish the durable base once, with the same serialized writer
            // as existing edits. Per-action writes are bypassed in the fork.
            saveTask?.GetAwaiter().GetResult();saveTask=null;
            if(!storage.Save(journal.Snapshot(),out error)) {SetStatus(error);return false;}
            dirty=false;savedJournal=journal;journal=journal.Fork();
            TemporarySaveError=null;TemporarySaveRevision=0;TemporarySaveId="";TemporarySessionId=Guid.NewGuid().ToString("N");Revision++;
            SetStatus("Temporary room started: changes stay here until you save a snapshot");return true;
        }
        public bool KeepTemporaryRoom(out string error)
        {
            error=null;CompleteTemporarySave();
            if(!TemporaryRoom) {error="Start a temporary room first";return false;}
            if(TemporarySavePending) {error="A temporary room snapshot is already saving";return false;}
            if(TemporarySaveRevision>=1000000) {error="End this temporary session before saving more snapshots";return false;}
            if(!CanChangeTemporaryBoundary(out error))return false;
            if(storage.ReadOnly) {error="This room is unavailable for saving";return false;}
            var workshop=GetComponent<AnimationWorkshop>();
            if(objects.Values.Any(item=>item && (item.GetComponent<RigidRoomItem>()?.AnimationOwned==true ||
                item.GetComponent<AvatarSpatialMotion>()?.Active==true || workshop && workshop.ControlsTarget(Identity(item))))) {
                error="Finish object movement and animation authoring before saving a snapshot";return false;
            }
            CapturePhysicsPlacements();
            var snapshot=journal.Snapshot();if(!snapshot.Validate(out error))return false;
            savingSnapshot=snapshot;TemporarySaveError=null;TemporarySaveId=Guid.NewGuid().ToString("N");
            LastTemporarySave=new TemporarySaveReceipt(TemporarySaveId,TemporarySessionId);
            temporarySave=Task.Run(()=>{storage.Save(snapshot,out var issue);return issue;});
            SetStatus("Saving temporary room snapshot: later changes remain temporary");return true;
        }
        void CompleteTemporarySave(bool wait=false)
        {
            if(temporarySave==null || !wait&&!temporarySave.IsCompleted)return;
            var error=temporarySave.GetAwaiter().GetResult();temporarySave=null;
            var snapshot=savingSnapshot;savingSnapshot=null;LastTemporarySave.Pending=false;LastTemporarySave.Error=error;
            if(error!=null) {
                TemporarySaveError=error;SetStatus(error);return;
            }
            // Only the exact successfully written snapshot enters saved Undo.
            // No scene reconciliation or action replay occurs at completion.
            if(!savedJournal.ApplySnapshot(snapshot,out error))throw new InvalidOperationException(error);
            TemporarySaveRevision++;LastTemporarySave.SavedRevision=TemporarySaveRevision;Revision++;
            SetStatus("Temporary room snapshot saved: later changes are still temporary");
        }
        public bool DiscardTemporaryRoom(out string error,bool stopAuthoring=true)
        {
            error=null;CompleteTemporarySave();
            if(!TemporaryRoom) {error="No temporary room is active";return false;}
            if(TemporarySavePending) {error="Wait for the dispatched snapshot save before discarding";return false;}
            if(!CanChangeTemporaryBoundary(out error))return false;
            if(stopAuthoring)Editing?.Invoke();
            if(!CanChangeTemporaryBoundary(out error))return false;
            // Discard restores document state, never historical velocity or a
            // running action. Leave physics paused after replacing colliders.
            PhysicsWorld?.PausePhysics();
            savedJournal.InvalidateChangedObservations(journal);
            journal=savedJournal;savedJournal=null;TemporarySessionId=Guid.NewGuid().ToString("N");TemporarySaveId="";TemporarySaveRevision=0;dirty=false;Revision++;
            if(journal.Read(selected)==null)selected=null;
            Reconcile();TemporarySaveError=null;
            SetStatus("Temporary changes discarded: returned to the last saved room");return true;
        }
    }
}
