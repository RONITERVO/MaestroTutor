// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        internal sealed class FrameEdit
        {
            internal string Session,Target,RoomSession,PoseSession,Message,Error;
            internal int Revision,SelectionSerial,SelectedFrame;
            internal RoomMotion Motion;internal JointPose[] Joints;
            internal bool SavePose,Temporary,Applied;
        }
        FrameEdit pendingFrame;
        FrameSaveAttempt frameSave;
        string frameSession=Guid.NewGuid().ToString("N");
        int selectionSerial;
        public bool HasUnsavedFrameEdit=>pendingFrame!=null;
        internal bool IsSavingFrameEdit=>frameSave!=null&&!frameSave.Ended;
        const string FrameEditPrompt="Animation edit retained — Save frame retries; Discard edit removes it";
        public JObject ObserveFrameEdit()
        {
            var d=pendingFrame;string error=(d?.Error??"").Replace("\r"," ").Replace("\n"," ");
            return new JObject {["sessionId"]=d?.Session??frameSession,["target"]=d?.Target??"",
                ["phase"]=IsSavingFrameEdit?"saving":d!=null?"unsaved":"idle",["revision"]=d?.Revision??0,
                ["frames"]=d?.Motion?.frames.Length??0,["duration"]=d?.Motion?.Duration??0,["loop"]=d?.Motion?.loop??false,
                ["writesPose"]=d?.SavePose??false,["poseSaved"]=d?.Joints!=null,["temporary"]=d?.Temporary??false,
                ["applied"]=d?.Applied??false,["error"]=error.Length>128?error[..128]:error};
        }
        internal bool QueueFrameEdit(RoomMotion motion,bool savePose,JointPose[] joints,string message,int nextFrame,Action<string> checkpoint=null)
        {
            if(HasUnsavedFrameEdit){Say(FrameEditPrompt);return false;}
            pendingFrame=new FrameEdit {Session=frameSession,Target=targetId,RoomSession=editor.TemporarySessionId,
                PoseSession=posing?poseSession:null,Revision=editor.ObjectRevision(targetId),SelectionSerial=selectionSerial,
                SelectedFrame=nextFrame,Motion=motion?.Copy(),Joints=MotionFrame.CopyJoints(joints),SavePose=savePose,
                Temporary=editor.TemporaryRoom,Message=message};
            return BeginFrameEditSave(frameSession,out _,out _,checkpoint);
        }
        internal bool CanResolveFrameEdit(string session,bool discard,out string error)
        {
            error="The animation edit changed; inspect animation.edit.pending";
            if(pendingFrame==null||pendingFrame.Session!=session)return false;
            if(discard){
                if(IsSavingFrameEdit&&frameSave.Accepted){error="The animation edit is applied and still saving; wait for its outcome";return false;}
                error=null;return true;
            }
            if(IsSavingFrameEdit){error=null;return true;}
            if(pendingFrame.Applied){error="Animation edit applied but saving unconfirmed; recover saving instead of replaying it";return false;}
            error=FrameEditIssue(pendingFrame);return error==null;
        }
        string FrameEditIssue(FrameEdit d)
        {
            if(!this||!isActiveAndEnabled||!editor||!editor.isActiveAndEnabled)return "Animation workshop is unavailable; edit retained";
            if(pendingFrame!=d||editor.TemporarySessionId!=d.RoomSession||editor.ObjectRevision(d.Target)!=d.Revision)
                return "The object or room changed; inspect or discard the retained animation edit";
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen)return "Recover room saving before retrying the animation edit";
            var item=editor.Find(d.Target);if(!item)return "The animation object is unavailable; edit retained";
            var model=item.GetComponent<MaestroAvatar>();
            var claims=RecordingClaims(d.Target);
            bool ownsTarget=ownershipLease?.Held==true&&editor.Ownership.Covers(ownershipId,claims);
            // Physical capture cooperates with the workshop's existing grab/pose.
            // A later retry without that lease must not affect a new physical actor.
            if(item.Grab.isSelected&&!(ownsTarget&&ownershipLease.AllowsGrab)||
                model&&model.PoseRig&&model.PoseRig.IsHolding&&!(ownsTarget&&posing&&d.PoseSession==poseSession))
                return "Release this object or joint before retrying the animation edit";
            if(model&&(model.ModelBusy||model.UpperBodyActive||model.GetComponent<AvatarSpatialMotion>()?.Active==true))
                return "Wait for the model, movement or gesture before retrying the animation edit";
            if(item.GetComponent<RigidRoomItem>() is RigidRoomItem body&&body.AnimationOwned&&!body.OwnedOnlyBy(this))
                return "Another animation owns the object; retained edits cannot interrupt it";
            if(!ownsTarget&&!editor.Ownership.Suspended&&!editor.Ownership.CanAcquire(ownershipId,RoomActorRole.Program,claims,out var issue))
                return issue;
            return null;
        }
        internal bool BeginFrameEditSave(string session,out FrameSaveAttempt attempt,out string error,Action<string> checkpoint=null)
        {
            attempt=null;if(!CanResolveFrameEdit(session,false,out error)){Say(error);return false;}
            if(IsSavingFrameEdit){attempt=frameSave;return true;}
            pendingFrame.Error=null;saveError=null;
            attempt=frameSave=new FrameSaveAttempt(this,pendingFrame,checkpoint);
            if(!attempt.Start(out error))return false;
            Say("Saving animation edit — captured values are queued");return true;
        }
        public void RetryFrameEdit(){if(!BeginFrameEditSave(frameSession,out _,out var error))Say(error);}
        public void DiscardFrameEdit(){if(!DiscardFrameEdit(frameSession,out _,out var error))Say(error);}
        internal bool DiscardFrameEdit(string session,out JObject result,out string error)
        {
            result=null;if(!CanResolveFrameEdit(session,true,out error))return false;
            bool applied=pendingFrame.Applied;
            result=new JObject {["sessionId"]=session,["target"]=pendingFrame.Target,["phase"]="discarded",
                ["revision"]=pendingFrame.Revision,["temporary"]=pendingFrame.Temporary};
            frameSave?.Retire("Animation edit discarded before acceptance");
            pendingFrame=null;frameSave=null;frameSession=Guid.NewGuid().ToString("N");saveError=pendingPose?.Error??pendingTake?.Error;
            Say(applied?"Edit buffer discarded — applied work remains; recover saving": "Animation edit discarded — previous saved animation remains");
            return true;
        }
        internal sealed class FrameSaveAttempt
        {
            readonly AnimationWorkshop owner;readonly FrameEdit edit;readonly Action<string> checkpoint;
            CapabilityOperation inner;RoomEditor.PendingPersistedEdit pending;
            string failure;JObject result;bool ended,polling,observedAcceptance;
            internal bool Ended=>ended;
            internal bool Accepted=>pending?.Accepted==true;
            internal JObject Result=>(JObject)result?.DeepClone();
            internal string InterruptionStatus=>Accepted?pending.InterruptionStatus:"Stopped observing the animation edit; inspect animation.edit.pending to retry or discard it.";
            internal FrameSaveAttempt(AnimationWorkshop owner,FrameEdit edit,Action<string> checkpoint)
            {this.owner=owner;this.edit=edit;this.checkpoint=checkpoint;}
            internal bool Start(out string error)
            {
                error=owner.FrameEditIssue(edit);if(error!=null)return Fail(error,out error);
                if(owner.editor.Ownership.Suspended||owner.editor.RuntimeGate.Held)return true;
                if(!PersistedEditOperation.Start(owner.editor,edit.Target,
                    (out RoomEditor.PendingPersistedEdit value,out string issue)=>{
                        bool started=owner.editor.BeginCapturedAnimation(edit.Target,edit.Revision,edit.Motion,edit.Joints,edit.SavePose,
                            ()=>owner.FrameEditIssue(edit),out value,out issue,checkpoint);pending=value;return started;
                    },out inner,out error,(out string issue)=>{issue=owner.FrameEditIssue(edit);return issue==null;},
                    ()=>new JObject {["sessionId"]=edit.Session,["target"]=edit.Target,["phase"]="saved",
                        ["revision"]=pending.AcceptedObjectRevision(edit.Target),["temporary"]=edit.Temporary}))return Fail(error,out error);
                return true;
            }
            internal void ObserveAcceptance()
            {
                if(!Accepted||observedAcceptance)return;observedAcceptance=true;edit.Applied=true;
                // Only our observed native acceptance may advance later pose saves.
                if(owner&&edit.Target=="maestro"&&edit.PoseSession==owner.poseSession&&owner.poseSaves.Count>0&&owner.poseSaveRevision==edit.Revision)
                    owner.poseSaveRevision=pending.AcceptedObjectRevision(edit.Target);
            }
            internal bool Poll(out bool ready,out string error)
            {
                ready=result!=null;error=failure;if(ended)return ready;
                if(polling){error=null;return true;}polling=true;bool wasSaving=owner.saving;owner.saving=true;
                try{
                    if(inner==null&&!Start(out error))return false;
                    if(inner==null)return true;
                    var state=inner.State(out error);ObserveAcceptance();
                    if(state==RuleActionState.Failed)return Fail(error,out error);
                    if(state!=RuleActionState.Ready)return true;
                    if(!inner.Complete(out error))return Fail(error,out error);
                    result=inner.Result;ended=true;ready=true;
                    if(owner&&owner.pendingFrame==edit){
                        owner.pendingFrame=null;owner.frameSession=Guid.NewGuid().ToString("N");owner.saveError=null;
                        if(owner.targetId==edit.Target&&owner.selectionSerial==edit.SelectionSerial)owner.selectedFrame=edit.SelectedFrame;
                        if(owner.posing&&edit.PoseSession==owner.poseSession)owner.PoseEdited();
                        owner.Say(edit.Temporary?"Animation edit kept temporarily — Keep saves it":edit.Message);
                    }
                    return true;
                }finally{owner.saving=wasSaving;polling=false;}
            }
            bool Fail(string reason,out string error)
            {
                ObserveAcceptance();ended=true;edit.Applied=Accepted;
                failure=error=Accepted?"Animation edit applied; saving unconfirmed. Inspect or recover the room. "+reason:
                    "Animation edit not saved; captured values retained. "+reason;
                edit.Error=failure;inner?.Stop(false);
                if(owner&&owner.pendingFrame==edit){owner.saveError=failure;owner.Say(failure);}
                return false;
            }
            internal RoomEditor.PendingPersistedEdit Suspend()
            {
                if(ended)return null;if(Accepted)return pending;
                inner?.Stop(false);inner=null;pending=null;return null;
            }
            internal void CancelBeforeAcceptance(string reason){if(!ended&&!Accepted)Fail(reason,out _);}
            internal void Retire(string reason){if(!ended)Fail(reason,out _);}
        }
        void PollFrameSave(){if(IsSavingFrameEdit)frameSave.Poll(out _,out _);}
        void ObserveFrameAcceptance()=>frameSave?.ObserveAcceptance();
    }
}
