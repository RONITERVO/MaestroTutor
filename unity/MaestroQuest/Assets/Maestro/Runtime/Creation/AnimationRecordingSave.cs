// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        RecordingSaveAttempt recordingSave;
        // The recorder owns publication. A receipt only observes it; stopping a
        // caller must not cancel a take started or finished by physical controls.
        internal sealed class RecordingSaveAttempt
        {
            readonly AnimationWorkshop owner;
            readonly PendingTake take;
            readonly string session,target,roomSession;
            readonly bool temporary;
            readonly Action<string> checkpoint;
            RoomEditor.EditAdmission admission;
            RoomEditor.PendingPersistedEdit pending;
            JObject result;
            string failure;
            bool ended,polling;
            internal bool Ended=>ended;
            internal bool Accepted=>pending?.Accepted==true;
            internal JObject Result=>(JObject)result?.DeepClone();
            internal string InterruptionStatus=>Accepted?pending.InterruptionStatus:
                "Stopped observing this recording save. The recording session continues saving; inspect animation.recording or explicitly discard its unaccepted take.";
            internal RecordingSaveAttempt(AnimationWorkshop owner,PendingTake take,string session,string target,string roomSession,Action<string> checkpoint)
            {
                this.owner=owner;this.take=take;this.session=session;this.target=target;this.roomSession=roomSession;
                this.checkpoint=checkpoint;temporary=owner.editor.TemporaryRoom;
            }
            string Issue()
            {
                if(!owner||!owner.isActiveAndEnabled||!owner.editor||!owner.editor.isActiveAndEnabled)
                    return "The animation workshop is unavailable; frames retained";
                var editor=owner.editor;
                if(owner.recordingSession!=session||owner.pendingTake!=take||editor.TemporarySessionId!=roomSession)
                    return "The room or recording session changed; discard this retained take";
                if(editor.ObjectRevision(target)!=take.Revision||!editor.Find(target))
                    return "The recorded object changed; inspect it before saving the retained take";
                var item=editor.Find(target);
                if(item.GetComponent<RigidRoomItem>() is RigidRoomItem body&&body.AnimationOwned&&!body.OwnedOnlyBy(owner))
                    return "Another animation or carried prop now owns the recorded object; stop it before retrying this take";
                var model=item.GetComponent<MaestroAvatar>();
                if(model&&(model.ModelBusy||model.UpperBodyActive||model.GetComponent<AvatarSpatialMotion>()?.Active==true))
                    return "Wait for Maestro's current model, movement or gesture before retrying this take";
                if(!editor.CanSaveRoom||editor.WriteGate.Frozen)
                    return "Recover room saving before saving this take";
                return null;
            }
            internal bool Start(out string error)
            {
                error=Issue();if(error!=null)return Fail(error,out error);
                if(!owner.editor.Ownership.Suspended&&!owner.editor.RuntimeGate.Held&&!owner.editor.ReserveEdit(out admission,out error))return Fail(error,out error);
                return true;
            }
            internal bool Poll(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(ended){ready=result!=null;return ready;}
                if(polling){error=null;return true;}
                polling=true;
                try{
                    // Pausing freezes capture immediately; publication may resume
                    // when native room interaction resumes. Never apply a prepared
                    // edit through a suspended ownership boundary.
                    if(!Accepted&&owner&&owner.editor&&(owner.editor.Ownership.Suspended||owner.editor.RuntimeGate.Held)){
                        error=Issue();if(error!=null)return Fail(error,out error);
                        error=null;return true;
                    }
                    if(pending==null){
                        error=Issue();if(error!=null)return Fail(error,out error);
                        if(admission==null&&!owner.editor.ReserveEdit(out admission,out error))return Fail(error,out error);
                        if(admission?.Valid!=true)return Fail("The room edit queue ended; frames retained",out error);
                        if(owner.editor.PendingEditSave||!admission.First)return true;
                        try{
                            if(!owner.editor.BeginCapturedAnimation(target,take.Revision,take.Motion,take.Pose,take.SavePose,
                                Issue,out pending,out error,checkpoint))return Fail(error,out error);
                        }finally{admission.Dispose();}
                    }
                    if(!pending.Poll(out ready,out error))return Fail(error,out error);
                    if(!ready)return true;
                    // Freeze the acceptance receipt before anyone can edit the target
                    // again or start another recording while this observer is delayed.
                    result=new JObject {["sessionId"]=session,["target"]=target,["phase"]="saved",
                        ["frames"]=take.Motion.frames.Length,["duration"]=take.Motion.Duration,
                        ["revision"]=pending.AcceptedObjectRevision(target),["temporary"]=temporary};
                    ended=true;take.Applied=true;
                    if(owner&&owner.pendingTake==take&&owner.recordingSession==session){
                        owner.pendingTake=null;owner.recordingSession=Guid.NewGuid().ToString("N");
                        owner.selectedFrame=-1;owner.saveError=null;
                        owner.Say(temporary?"Recording kept in this temporary room — Keep saves it":"Recording saved — Undo restores the previous animation");
                    }
                    return true;
                }finally{polling=false;}
            }
            bool Fail(string reason,out string error)
            {
                ended=true;admission?.Dispose();take.Applied=Accepted;
                failure=error=take.Applied?
                    "Take applied; saving unconfirmed. Inspect or recover the room before retrying. "+reason:
                    "Take not saved; frames retained. "+reason;
                take.Error=failure;pending?.Dispose();
                if(owner&&owner.pendingTake==take){owner.saveError=failure;owner.Say(failure);}
                return false;
            }
            internal RoomEditor.PendingPersistedEdit SuspendPreparation()
            {
                // An accepted publication already owns detached saved data and
                // must remain observable across focus loss. Unaccepted candidates
                // are cancelled; the same frozen take rejoins at the tail on resume.
                if(Accepted&&!ended)return pending;
                if(ended)return null;
                admission?.Dispose();admission=null;pending?.Dispose();pending=null;
                return null;
            }
            internal bool CancelBeforeAcceptance(string reason)
            {
                if(ended||Accepted)return false;
                Fail(reason,out _);return true;
            }
            internal void Retire()
            {
                if(!ended)Fail(Accepted?"Workshop closed after acceptance; saving continues.":"Workshop closed before acceptance.",out _);
            }
        }
        internal RoomEditor.PendingPersistedEdit SuspendRecordingSave()=>recordingSave?.SuspendPreparation();
        void PollRecordingSave(){if(recordingSave!=null&&!recordingSave.Ended)recordingSave.Poll(out _,out _);}
    }
}
