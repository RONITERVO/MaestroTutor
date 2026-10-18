// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        readonly List<PoseSaveAttempt> poseSaves=new();
        int poseSaveRevision;
        bool pollingPoseSaves;
        internal bool IsSavingPose=>poseSaves.Count>0;
        internal sealed class PoseSaveAttempt
        {
            readonly AnimationWorkshop owner;
            internal readonly PendingPose Pose;
            readonly Action<string> checkpoint;
            RoomEditor.EditAdmission admission;
            RoomEditor.PendingPersistedEdit pending;
            JObject result;
            string failure;
            bool ended,observedAcceptance;
            internal bool Ended=>ended;
            internal bool Accepted=>pending?.Accepted==true;
            internal JObject Result=>(JObject)result?.DeepClone();
            internal string InterruptionStatus=>Accepted?pending.InterruptionStatus:
                "Stopped observing this pose save. The pose session continues saving; inspect animation.posing or explicitly discard its unaccepted preview.";
            internal PoseSaveAttempt(AnimationWorkshop owner,PendingPose pose,Action<string> checkpoint)
            {this.owner=owner;Pose=pose;this.checkpoint=checkpoint;}
            internal bool Reserve(out string error)
            {
                error=Issue();if(error!=null)return false;
                return owner.editor.Ownership.Suspended||owner.editor.RuntimeGate.Held||
                    owner.editor.ReserveEdit(out admission,out error);
            }
            string Issue()
            {
                if(!owner||!owner.isActiveAndEnabled||!owner.editor||!owner.editor.isActiveAndEnabled)
                    return "The animation workshop is unavailable; pose retained";
                var editor=owner.editor;
                if(owner.poseSession!=Pose.Session||editor.TemporarySessionId!=Pose.RoomSession)
                    return "The room or pose session changed; inspect the retained pose";
                if(editor.ObjectRevision(Pose.Target)!=owner.poseSaveRevision||!editor.Find(Pose.Target))
                    return "Maestro changed while saving; inspect the retained pose";
                if(!editor.CanSaveRoom||editor.WriteGate.Frozen)return "Recover room saving before saving this pose";
                var item=editor.Find(Pose.Target);var model=item.GetComponent<MaestroAvatar>();
                if(!model||model.ModelBusy||model.UpperBodyActive||model.GetComponent<AvatarSpatialMotion>()?.Active==true)
                    return "Wait for Maestro's model, movement or gesture before retrying this pose";
                if(item.GetComponent<RigidRoomItem>() is RigidRoomItem body&&body.AnimationOwned&&!body.OwnedOnlyBy(owner))
                    return "Another animation or carried prop owns Maestro; pose retained";
                if(!editor.Ownership.Suspended&&!editor.Ownership.CanAcquire(owner.ownershipId,RoomActorRole.Program,
                    RecordingClaims(Pose.Target),out var error))return error;
                return null;
            }
            internal void ObserveAcceptance()
            {
                if(!Accepted||observedAcceptance)return;
                observedAcceptance=true;owner.poseSaveRevision=pending.AcceptedObjectRevision(Pose.Target);
            }
            internal bool Advance(out bool ready,out string error)
            {
                ready=false;error=failure;
                if(ended){ready=result!=null;return ready;}
                if(!Accepted){
                    error=Issue();if(error!=null)return false;
                    if(owner.editor.Ownership.Suspended||owner.editor.RuntimeGate.Held)return true;
                }
                if(pending==null){
                    if(admission==null&&!Reserve(out error))return false;
                    if(admission?.Valid!=true){error="The room edit queue ended; pose retained";return false;}
                    if(owner.editor.PendingEditSave||!admission.First)return true;
                    try{
                        int revision=owner.poseSaveRevision;
                        if(!owner.editor.BeginAuthoredPose(Pose.Target,revision,Pose.Joints,Issue,out pending,out error,checkpoint))return false;
                    }finally{admission.Dispose();}
                }
                bool valid=pending.Poll(out ready,out error);ObserveAcceptance();
                if(!valid)return false;
                if(!ready)return true;
                result=new JObject {["sessionId"]=Pose.Session,["phase"]="saved",["version"]=Pose.Version,
                    ["revision"]=pending.AcceptedObjectRevision(Pose.Target),["temporary"]=Pose.Temporary};
                ended=true;return true;
            }
            internal bool Poll(out bool ready,out string error)
            {
                if(!ended&&owner)owner.PollPoseSaves();
                ready=result!=null;error=failure;return !ended||ready;
            }
            internal void Fail(string error)
            {if(ended)return;ended=true;failure=error;admission?.Dispose();pending?.Dispose();}
            internal RoomEditor.PendingPersistedEdit Suspend()
            {
                if(Accepted&&!ended)return pending;
                admission?.Dispose();admission=null;pending?.Dispose();pending=null;return null;
            }
        }
        PendingPose CapturePose()=>new() {Target="maestro",RoomSession=editor.TemporarySessionId,Session=poseSession,
            Version=poseVersion,Revision=editor.ObjectRevision("maestro"),Joints=avatar.PoseRig.Capture(),Temporary=editor.TemporaryRoom};
        void ObservePoseAcceptance(){foreach(var save in poseSaves)save.ObserveAcceptance();}
        internal bool BeginPoseSave(string session,int version,bool finish,out PoseSaveAttempt attempt,out string error,
            bool manual=false,Action<string> checkpoint=null,bool lifecycle=false)
        {
            attempt=null;
            if(!lifecycle&&!CanUsePose(session,version,finish?"finish":"save",out error,manual))return false;
            error=null;ObservePoseAcceptance();
            if(poseSaves.Count>0){
                attempt=poseSaves.LastOrDefault(x=>x.Pose.Session==session&&x.Pose.Version==version);
                if(attempt!=null){if(finish)ReleasePoseControls();return true;}
            }
            bool retained=!posing;var pose=retained?pendingPose:CapturePose();
            if(pose==null){error="There is no pose to save";return false;}
            if(poseSaves.Count>=Rules.RuleScheduler.MaximumConcurrent){
                error="Too many pending pose saves; newest pose retained";FailPoseSaves(error);return false;
            }
            RoomOwnership.Lease lease=null;
            if(retained&&!IsSavingPose){
                if(!CanSaveRetainedPose(out error,manual))return false;
                if(!editor.Ownership.TryAcquire(ownershipId,"Save retained pose",manual?RoomActorRole.Control:RoomActorRole.Program,
                    RecordingClaims(pose.Target),null,out lease,out error))return false;
                if(pose.RoomSession!=editor.TemporarySessionId||pose.Revision!=editor.ObjectRevision(pose.Target)){
                    lease.Dispose();error="Maestro or the room changed; pose retained";return false;
                }
            }
            try{
                if(!IsSavingPose)poseSaveRevision=pose.Revision;
                pose.Error=null;saveError=null;pendingPose=pose;
                attempt=new PoseSaveAttempt(this,pose,checkpoint);poseSaves.Add(attempt);
                if(!attempt.Reserve(out error)){FailPoseSaves(error);return false;}
                if(finish||retained)ReleasePoseControls();
                Say("Saving pose — the captured version is queued");return true;
            }finally{lease?.Dispose();}
        }
        void PollPoseSaves()
        {
            if(pollingPoseSaves||poseSaves.Count==0)return;
            pollingPoseSaves=true;
            try{
                var save=poseSaves[0];
                if(!save.Advance(out var ready,out var error)){FailPoseSaves(error);return;}
                if(!ready)return;
                poseSaves.RemoveAt(0);
                if(poseSaves.Count>0)return;
                pendingPose=null;saveError=null;
                if(posing){if(poseVersion==save.Pose.Version)PoseEdited();}
                else EndPoseSession();
                Say(posing?"Pose version saved — newer live edits remain a preview":"Pose saved — Undo restores the previous pose");
            }finally{pollingPoseSaves=false;}
        }
        void FailPoseSaves(string reason)
        {
            if(poseSaves.Count==0)return;
            ObservePoseAcceptance();bool applied=poseSaves.Any(x=>x.Accepted);
            var latest=posing&&avatar&&avatar.PoseRig?CapturePose():pendingPose??poseSaves[^1].Pose;
            latest.Revision=poseSaveRevision;latest.Applied=applied;
            latest.Error=applied?"Pose applied; saving unconfirmed. Inspect or recover the room; do not replay it. "+reason:
                "Pose not saved; newest pose retained. "+reason;
            foreach(var save in poseSaves)save.Fail(latest.Error);
            poseSaves.Clear();pendingPose=latest;saveError=latest.Error;
            ReleasePoseControls();Say(saveError);
        }
        internal RoomEditor.PendingPersistedEdit SuspendPoseSaves()
        {
            RoomEditor.PendingPersistedEdit accepted=null;
            foreach(var save in poseSaves)accepted=save.Suspend()??accepted;
            return accepted;
        }
        internal RoomEditor.PendingPersistedEdit SuspendAuthoringSave()=>SuspendRecordingSave()??SuspendPoseSaves();
        void RetirePoseSaves(string reason){if(IsSavingPose)FailPoseSaves(reason);}
    }
}
