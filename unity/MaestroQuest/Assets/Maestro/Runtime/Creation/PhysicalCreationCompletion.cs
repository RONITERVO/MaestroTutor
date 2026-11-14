// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        sealed class ManualCreation
        {
            internal CapabilityOperation Operation;
            internal RoomOwnership.Lease Lease;
            internal Action Completed;
            internal string Context,Label;
            internal readonly TaskCompletionSource<bool> Finished=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal float Deadline;
        }
        delegate bool StartManualEdit(out CapabilityOperation operation,out string error);
        ManualCreation manualCreation;
        internal bool ManualCreationPending=>manualCreation!=null;
        internal Task<bool> ManualCreationCompletion {get;private set;}=Task.FromResult(false);
        internal Action<string> ManualCreationCheckpoint {get;set;}

        void CreateManually(RoomObjectKind kind)
        {
            if(kind!=RoomObjectKind.Block&&kind!=RoomObjectKind.Ball&&kind!=RoomObjectKind.Cylinder)return;
            var position=SpawnPosition();var color=Paint;var checkpoint=ManualCreationCheckpoint;string created=null;
            var previous=selected;uint version=selectionVersion;
            StartManualCreation(null,false,(out CapabilityOperation operation,out string error)=>
                PersistedEditOperation.StartCreation(this,
                    (out PendingPersistedEdit pending,out string issue)=>BeginCreatePrimitive(kind,"",position,1,color,out created,out pending,out issue,checkpoint),
                    ()=>new JObject {["objectId"]=created},out operation,out error),
                ()=>SelectCompletedCreation(created,previous,version));
        }
        void PaintManually(Color color)
        {
            Paint=new Color(color.r,color.g,color.b,1);
            if(DrawingMode){SetStatus("Pencil colour chosen for the next stroke");return;}
            string target=selected;var data=Read(target);
            if(data==null||data.IsBuiltIn){SetStatus("Paint chosen for your next creation");return;}
            var captured=Paint;var checkpoint=ManualCreationCheckpoint;
            StartManualCreation(target,true,(out CapabilityOperation operation,out string error)=>
                PersistedEditOperation.Start(this,target,
                    (out PendingPersistedEdit pending,out string issue)=>BeginPaint(target,captured,out pending,out issue,checkpoint),
                    out operation,out error,(out string issue)=>CanEditObject(target,true,out issue)),null);
        }
        // Physical settings share the same completion, cancellation and retained-
        // draft rules as paint/erase. Capture before either ownership or saving yields.
        internal void ConfigurePhysicsManually(string target,ObjectPhysicsSettings settings)
        {
            var captured=settings==null?null:new ObjectPhysicsSettings {mode=settings.mode,shape=settings.shape,mass=settings.mass};
            var data=Read(target);
            if(!RoomControls.SetPhysics(data,captured,out var error)){SetStatus(error);return;}
            int revision=ObjectRevision(target);var checkpoint=ManualCreationCheckpoint;
            StartManualCreation(target,true,(out CapabilityOperation operation,out string issue)=>
                PersistedEditOperation.Start(this,target,
                    (out PendingPersistedEdit pending,out string failure)=>BeginConfigurePhysics(target,revision,captured,out pending,out failure,checkpoint),
                    out operation,out issue,(out string failure)=>CanConfigurePhysics(target,revision,captured,out failure)),
                null,"Physics tools",string.IsNullOrWhiteSpace(data.name)?data.kind.ToString():data.name);
        }
        void DuplicateManually()
        {
            string target=selected;var data=Read(target);var live=Find(target);
            if(data==null||data.IsBuiltIn||!live){SetStatus("Select one of your creations to duplicate");return;}
            if(!Frame.Read(live.transform,out var position,out _,out _)){SetStatus("The source needs a valid uniform room frame before copying");return;}
            position+=Vector3.right*.18f;int revision=ObjectRevision(target);string name=data.name,created=null;
            uint version=selectionVersion;var checkpoint=ManualCreationCheckpoint;
            StartManualCreation(target,false,(out CapabilityOperation operation,out string error)=>
                PersistedEditOperation.StartCreation(this,
                    (out PendingPersistedEdit pending,out string issue)=>BeginCopyObject(target,revision,name,position,out created,out pending,out issue,checkpoint),
                    ()=>new JObject {["objectId"]=created},out operation,out error,target,
                    (out string issue)=>CanCopyObject(target,revision,out issue)),
                ()=>SelectCompletedCreation(created,target,version));
        }
        void EraseManually()
        {
            string target=selected;var data=Read(target);
            if(data==null||data.IsBuiltIn){SetStatus("Select one of your creations to erase");return;}
            var checkpoint=ManualCreationCheckpoint;
            StartManualCreation(target,true,(out CapabilityOperation operation,out string error)=>
                PersistedEditOperation.Start(this,target,
                    (out PendingPersistedEdit pending,out string issue)=>BeginDelete(target,out pending,out issue,checkpoint),
                    out operation,out error,(out string issue)=>CanDeleteObject(target,out issue)),
                ()=>{if(selected==target){selected=null;selectionVersion++;UpdateSelection();}});
        }
        internal Action CaptureCompletedSelection(string id)
        {
            string previous=selected;uint version=selectionVersion;
            return ()=>{
                if(!this||previous!=null&&previous!=id&&GetComponent<AnimationWorkshop>()?.HasUnfinishedAuthoring(previous)==true)return;
                SelectCompletedCreation(id,previous,version);
            };
        }
        void SelectCompletedCreation(string id,string previous,uint version)
        {
            // A delayed save must not steal a newer hand/book selection.
            if(selected==previous&&selectionVersion==version&&Read(id)!=null)SelectSavedObject(id);
        }
        bool StartManualCreation(string target,bool replaceTargetControl,StartManualEdit start,Action completed,string label="Creation tools",string subject=null)
        {
            if(manualCreation!=null){SetStatus("Wait for the pending object edit");return false;}
            if(!isActiveAndEnabled||journal==null||Ownership.Suspended){SetStatus("Room actions are not ready");return false;}
            if(WriteGate.Frozen){SetStatus(Persistence.WorkspaceWriteGate.FrozenReason);return false;}
            if(storage.ReadOnly){SetStatus(storage.UnavailableReason);return false;}
            // Retain a short human-readable subject even if selection changes while saving.
            if(subject?.Length>48)subject=subject.Substring(0,45)+"...";
            var request=new ManualCreation {Completed=completed,Deadline=Time.realtimeSinceStartup+30,Label=label,Context=subject==null?null:label+": "+subject};
            manualCreation=request;ManualCreationCompletion=request.Finished.Task;
            try {
                // The physical tool may replace the exact target's controls,
                // never broadcast an unrelated whole-room authoring stop.
                var claims=target==null?Array.Empty<BehaviourCatalog.Claim>():new[]{new BehaviourCatalog.Claim(target,"wholeTarget")};
                if(!Ownership.TryAcquire("creation:"+Guid.NewGuid().ToString("N"),label,RoomActorRole.Control,claims,
                    notice=>{if(ReferenceEquals(manualCreation,request))CancelManualCreation(true,notice.Message);},
                    out request.Lease,out var error,preservePlacement:true,replaceControl:replaceTargetControl)){
                    EndManualCreation(request);SetStatus(error);return false;
                }
                if(!ReferenceEquals(manualCreation,request)){EndManualCreation(request);return false;}
                // Taking over a recorder/poser can freeze a new save or retained
                // draft. Keep its target intact until that session is resolved.
                if(replaceTargetControl&&GetComponent<AnimationWorkshop>()?.HasUnfinishedAuthoring(target)==true){
                    EndManualCreation(request);SetStatus("Finish saving or discard this object's unfinished animation (retained pose, take or keyframe edit) before editing it");return false;
                }
                if(!start(out request.Operation,out error)){EndManualCreation(request);SetStatus(error);return false;}
                if(!ReferenceEquals(manualCreation,request)){EndManualCreation(request);return false;}
                SetStatus("Preparing and saving object edit");
                PollManualCreation();return !request.Finished.Task.IsCompleted||request.Finished.Task.Result;
            } catch {EndManualCreation(request);throw;}
        }
        void PollManualCreation()
        {
            var request=manualCreation;if(request?.Operation==null)return;
            try {
                var state=request.Operation.State(out var error);
                if(!ReferenceEquals(manualCreation,request))return;
                if(state==RuleActionState.Preparing&&Time.realtimeSinceStartup<request.Deadline)return;
                bool success=state==RuleActionState.Ready&&request.Operation.Complete(out error);
                if(state==RuleActionState.Preparing)error="Object edit timed out. "+(request.Operation.InterruptionStatus??"No edit was accepted.");
                if(!success&&state!=RuleActionState.Preparing&&request.Operation.InterruptionStatus is string accepted)
                    error=(error??"Object edit did not complete")+". "+accepted;
                // State may observe a save finishing after Update's first poll.
                // Consume that same completed future before reporting its detailed
                // tool outcome, so next frame's generic save status cannot erase it.
                CompleteSave();
                if(!ReferenceEquals(manualCreation,request))return;
                var completed=success?request.Completed:null;
                EndManualCreation(request,success);
                if(success)completed?.Invoke();
                else SetStatus((request.Context==null?"":request.Context+". ")+(error??"Object edit did not complete; inspect the room before retrying"));
            } catch {EndManualCreation(request);throw;}
        }
        void EndManualCreation(ManualCreation request,bool success=false)
        {
            if(ReferenceEquals(manualCreation,request))manualCreation=null;
            try{request.Operation?.Stop(false);}
            finally{try{request.Lease?.Dispose();}finally{request.Finished.TrySetResult(success);}}
        }
        internal void CancelManualCreation(bool report,string reason="Object edit stopped")
        {
            var request=manualCreation;if(request==null)return;
            string status=request.Operation?.InterruptionStatus;
            EndManualCreation(request);
            if(report&&isActiveAndEnabled)SetStatus(reason+". "+(status??"No edit was accepted."));
        }
    }
}
