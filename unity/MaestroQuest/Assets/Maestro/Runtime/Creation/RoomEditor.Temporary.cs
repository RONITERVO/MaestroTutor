// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;

namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class TemporaryRoomView {
        public bool active,pending;
        public string id="",saveId="",phase="idle",error="";
        public int savedRevision;
    }
    public sealed class TemporarySaveReceipt {
        public readonly string Id,SessionId;
        public readonly bool IsBaseline;
        public bool Pending {get;internal set;}=true;
        public string Error {get;internal set;}
        public int SavedRevision {get;internal set;}
        internal TemporarySaveReceipt(string id,string sessionId,bool baseline) {Id=id;SessionId=sessionId;IsBaseline=baseline;}
    }
    // One shared live fork; catalog actions own public execution and receipts.
    public sealed partial class RoomEditor
    {
        RoomJournal savedJournal;
        bool savedBaseDurable;
        Task<string> temporarySave;
        RoomDocument savingSnapshot;
        TemporaryMemorySave savingMemory;
        RoomSnapshotTransaction.Snapshot savedPair;
        internal bool TemporaryStorageUncertain {get;private set;}
        ProgramMemoryStore TemporaryMemory=>GetComponent<RuleWorkshop>()?.Memory;
        public bool TemporaryRoom => savedJournal!=null;
        public bool CanSaveRoom=>storage!=null&&!storage.ReadOnly&&!TemporaryStorageUncertain;
        public string TemporarySessionId {get;private set;}=Guid.NewGuid().ToString("N");
        public TemporarySaveReceipt LastTemporarySave {get;private set;}
        public string TemporarySaveId {get;private set;}="";
        public TemporaryRoomView ObserveTemporaryRoom()=>new() {active=TemporaryRoom,pending=TemporarySavePending,id=TemporarySessionId,saveId=TemporarySaveId,savedRevision=TemporarySaveRevision,
            phase=TemporarySavePending?(LastTemporarySave.IsBaseline?"starting":"pending"):TemporarySaveError!=null?"failed":TemporarySaveRevision>0?"saved":TemporaryRoom?"ready":"idle",error=TemporarySaveError??""};
        internal void PollTemporarySave()=>CompleteTemporarySave();
        public bool TemporarySavePending => temporarySave!=null;
        public string TemporarySaveError {get;private set;}
        public int TemporarySaveRevision {get;private set;}

        internal RoomDocument RecoverySavedSnapshot()=>savedJournal?.Snapshot()??journal.Snapshot();
        internal bool CanChangeTemporaryBoundary(out string error,bool changingMemory=false)
        {
            error=null;
            if(journal==null) {error="Room editor is not ready";return false;}
            if(changingMemory&&TemporaryStorageUncertain){error="The paired room/memory save needs recovery. Keep this workspace open or recover it before discarding.";return false;}
            var memory=TemporaryMemory;
            if(changingMemory&&memory!=null){
                if(!memory.Ready||memory.Pending||memory.Error!=null){error=memory.Error??"Wait for remembered values to finish loading or saving";return false;}
                if(memory.Temporary!=TemporaryRoom){error="Temporary room and memory scopes do not match";return false;}
            }
            if(GetComponent<AnimationWorkshop>()?.HasUnsavedPose==true){error="Save or discard the retained pose before changing workspaces or temporary rooms";return false;}
            if(GetComponent<AnimationWorkshop>()?.HasUnsavedRecording==true){error="Save or discard the retained recording before changing workspaces or temporary rooms";return false;}
            if(Liquids?.Active==true){error="Finish pouring or pause room physics before changing the temporary room";return false;}
            if(DrawingInProgress){error="Save or discard the current stroke before changing workspaces or temporary rooms";return false;}
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
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            error=null;
            if(TemporaryRoom) {error="A temporary room is already active";return false;}
            if(!CanChangeTemporaryBoundary(out error,changingMemory:true))return false;
            if(storage.ReadOnly) {error="This room is unavailable for saving";return false;}
            // Direct manual callers finish authoring first. Catalog calls already
            // coordinate ownership and must not cancel their own scheduler run.
            if(stopAuthoring)Editing?.Invoke();
            if(!CanChangeTemporaryBoundary(out error,changingMemory:true))return false;
            CapturePhysicsPlacements();
            var baseline=journal.Snapshot();if(!baseline.Validate(out error))return false;
            // Capture on Unity's thread. Later edits go only to a separate live
            // fork, even while the baseline waits for an earlier autosave.
            try{TemporaryMemory?.BeginTemporary();}catch(Exception e){error=e.Message;return false;}
            savedPair=null;TemporaryStorageUncertain=false;
            savedJournal=journal;journal=journal.Fork();savedBaseDurable=false;dirty=false;
            TemporarySaveRevision=0;TemporarySessionId=Guid.NewGuid().ToString("N");Revision++;ClearConstructionSelection();
            var previous=saveTask;saveTask=null;
            DispatchTemporarySave(baseline,baseline:true,previous);
            SetStatus("Starting temporary room: saving its base; new edits stay temporary");return true;
        }
        void DispatchTemporarySave(RoomDocument snapshot,bool baseline,Task<string> previous=null)
        {
            savingSnapshot=snapshot;TemporarySaveError=null;TemporarySaveId=Guid.NewGuid().ToString("N");
            LastTemporarySave=new TemporarySaveReceipt(TemporarySaveId,TemporarySessionId,baseline);
            var target=storage; // The worker owns detached data, never scene/journal objects.
            try{savingMemory=TemporaryMemory is ProgramMemoryStore memory?new TemporaryMemorySave(memory,savedPair,snapshot,WriteGate):null;}
            catch(Exception e){savingMemory=null;temporarySave=Task.FromResult("Snapshot capture could not begin. "+e.Message);return;}
            var memorySave=savingMemory;
            temporarySave=Task.Run(async()=>{
                try {
                    if(previous!=null)await previous.ConfigureAwait(false);
                    if(memorySave!=null)return memorySave.Run();
                    target.Save(snapshot,out var issue);return issue;
                } catch(Exception) {
                    return "The room snapshot could not be saved. Temporary edits remain available; inspect storage before retrying.";
                }finally{memorySave?.Dispose();}
            });
        }

        public bool KeepTemporaryRoom(out string error)
        {
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            error=null;CompleteTemporarySave();
            if(!TemporaryRoom) {error="Start a temporary room first";return false;}
            if(TemporarySavePending) {error="A temporary room snapshot is already saving";return false;}
            if(TemporarySaveRevision>=1000000) {error="End this temporary session before saving more snapshots";return false;}
            if(!CanChangeTemporaryBoundary(out error,changingMemory:true))return false;
            if(storage.ReadOnly) {error="This room is unavailable for saving";return false;}
            var workshop=GetComponent<AnimationWorkshop>();
            if(objects.Values.Any(item=>item && (item.GetComponent<RigidRoomItem>()?.AnimationOwned==true ||
                item.GetComponent<AvatarSpatialMotion>()?.Active==true || workshop && workshop.ControlsTarget(Identity(item))))) {
                error="Finish object movement and animation authoring before saving a snapshot";return false;
            }
            CapturePhysicsPlacements();
            var snapshot=journal.Snapshot();if(!snapshot.Validate(out error))return false;
            DispatchTemporarySave(snapshot,baseline:false);
            SetStatus("Saving temporary room snapshot: later changes remain temporary");return true;
        }
        void CompleteTemporarySave(bool wait=false)
        {
            if(temporarySave==null || !wait&&!temporarySave.IsCompleted)return;
            var error=temporarySave.GetAwaiter().GetResult();temporarySave=null;
            var snapshot=savingSnapshot;savingSnapshot=null;var memorySave=savingMemory;savingMemory=null;
            if(memorySave!=null){
                TemporaryStorageUncertain=memorySave.Uncertain;
                if(error==null){
                    try {memorySave.Confirm();savedPair=memorySave.Pair;}
                    catch(Exception e){TemporaryStorageUncertain=true;error="Saved room/memory needs recovery before further saves. "+e.Message;}
                }
            }
            LastTemporarySave.Pending=false;LastTemporarySave.Error=error;
            if(error!=null) {
                TemporarySaveError=LastTemporarySave.IsBaseline?"Starting room was not saved. Edits remain temporary; Save snapshot retries, or Discard returns to the starting room. "+error:error;SetStatus(TemporarySaveError);return;
            }
            // Only the exact successfully written snapshot enters saved Undo.
            // No scene reconciliation or action replay occurs at completion.
            savedBaseDurable=true;
            if(LastTemporarySave.IsBaseline) {
                Revision++;SetStatus("Temporary room ready: its starting state is saved; later changes stay temporary");return;
            }
            if(!savedJournal.ApplySnapshot(snapshot,out error))throw new InvalidOperationException(error);
            TemporarySaveRevision++;LastTemporarySave.SavedRevision=TemporarySaveRevision;Revision++;
            SetStatus("Temporary room snapshot saved: later changes are still temporary");
        }
        public bool DiscardTemporaryRoom(out string error,bool stopAuthoring=true)
        {
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            error=null;CompleteTemporarySave();
            if(!TemporaryRoom) {error="No temporary room is active";return false;}
            if(TemporarySavePending) {error="Wait for the dispatched snapshot save before discarding";return false;}
            if(!CanChangeTemporaryBoundary(out error,changingMemory:true))return false;
            if(stopAuthoring)Editing?.Invoke();
            if(!CanChangeTemporaryBoundary(out error,changingMemory:true))return false;
            // Discard restores document state, never historical velocity or a
            // running action. Leave physics paused after replacing colliders.
            try{TemporaryMemory?.EndTemporary();}catch(Exception e){error=e.Message;return false;}
            savedPair=null;PhysicsWorld?.PausePhysics();
            savedJournal.InvalidateChangedObservations(journal);
            bool viewpointChanged=savedJournal.UpdateViewpoint(journal.Viewpoint);
            journal=savedJournal;savedJournal=null;TemporarySessionId=Guid.NewGuid().ToString("N");TemporarySaveId="";TemporarySaveRevision=0;dirty=!savedBaseDurable||viewpointChanged;saveAt=UnityEngine.Time.unscaledTime+.5f;Revision++;ClearConstructionSelection();
            if(journal.Read(selected)==null)selected=null;
            Reconcile();TemporarySaveError=null;
            SetStatus(savedBaseDurable?"Temporary changes discarded: returned to the last saved room":"Temporary changes discarded: returned to the starting room; saving its pending edits");return true;
        }
    }
}
