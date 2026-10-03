// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        sealed class PendingPose
        {
            public string Target,RoomSession,Error;
            public int Revision;
            public JointPose[] Joints;
        }
        PendingPose pendingPose;
        public bool HasUnsavedPose=>pendingPose!=null;
        const string RetainedPosePrompt="Pose retained — Save pose retries; Discard pose removes it";
        bool SaveCurrentPose()
        {
            var pose=new PendingPose {Target=targetId,RoomSession=editor.TemporarySessionId,
                Revision=editor.ObjectRevision(targetId),Joints=avatar.PoseRig.Capture()};
            if(Save(editor.Read(targetId).motion,true,pose.Joints))return true;
            pose.Error=saveError;pendingPose=pose;PoseEdited();saveError=RetainedPosePrompt;
            Say(saveError);return false;
        }
        internal bool CanSaveRetainedPose(out string error,bool manual=false)
        {
            error="There is no retained pose";var pose=pendingPose;if(pose==null)return false;
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"Room editor is unavailable";return false;}
            if(pose.RoomSession!=editor.TemporarySessionId||editor.ObjectRevision(pose.Target)!=pose.Revision){error="Maestro or the room changed; discard this retained pose";return false;}
            var item=editor.Find(pose.Target);var model=item?item.GetComponent<MaestroAvatar>():null;
            if(!model||model.ModelBusy||editor.AnyHeld||model.PoseRig&&model.PoseRig.IsHolding){error="Finish loading and release objects or joints first";return false;}
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen){error="Room saving is unavailable";return false;}
            return editor.Ownership.CanAcquire(ownershipId,manual?RoomActorRole.Control:RoomActorRole.Program,RecordingClaims(pose.Target),out error);
        }
        public bool SaveRetainedPose(out string error,bool manual=true)
        {
            error="There is no retained pose";var pose=pendingPose;
            if(pose==null){Say(error);return false;}
            bool Fail(string message){pendingPose.Error=message;Say(RetainedPosePrompt+". "+message);return false;}
            if(!CanSaveRetainedPose(out error,manual))return Fail(error);
            // Only the physical retry may interrupt a program. Shared calls retain
            // program priority and cannot replace another live actor.
            if(!editor.Ownership.TryAcquire(ownershipId,"Save retained pose",manual?RoomActorRole.Control:RoomActorRole.Program,
                RecordingClaims(pose.Target),null,out var lease,out error))return Fail(error);
            using(lease){
                saving=true;
                try {
                    // Recheck after interruption callbacks; they can persist edits.
                    if(pose.RoomSession!=editor.TemporarySessionId||editor.ObjectRevision(pose.Target)!=pose.Revision){error="Maestro or the room changed; discard this retained pose";return Fail(error);}
                    if(!editor.WriteAnimation(pose.Target,pose.Revision,editor.Read(pose.Target).motion,pose.Joints,true,out error))return Fail(error);
                    pendingPose=null;saveError=null;EndPoseSession();editor.RestorePose(pose.Target);
                } finally {saving=false;}
            }
            Say("Retained pose saved — Undo restores the previous pose");return true;
        }
        public void SaveRetainedPose(){if(posing){if(!ResolvePose(poseSession,poseVersion,"save",out _,out var error))Say(error);}else SaveRetainedPose(out _);}
        public void DiscardRetainedPose()
        {
            if(pendingPose==null){if(posing){if(!ResolvePose(poseSession,poseVersion,"discard",out _,out var error))Say(error);}else Say("There is no retained pose");return;}
            pendingPose=null;saveError=null;EndPoseSession();Say("Retained pose discarded — saved pose and animation kept");
        }
    }
}
