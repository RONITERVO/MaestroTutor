// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        internal sealed class PendingPose
        {
            public string Target,RoomSession,Session,Error;
            public int Revision,Version;
            public bool Applied,Temporary;
            public JointPose[] Joints;
        }
        PendingPose pendingPose;
        public bool HasUnsavedPose=>pendingPose!=null;
        const string RetainedPosePrompt="Pose retained — Save pose retries; Discard pose removes it";
        bool SaveCurrentPose(bool finish=false)=>BeginPoseSave(poseSession,poseVersion,finish,out _,out _,manual:true,lifecycle:true);
        internal bool CanSaveRetainedPose(out string error,bool manual=false)
        {
            error="There is no retained pose";var pose=pendingPose;if(pose==null)return false;
            if(IsSavingPose){error=null;return true;}
            if(pose.Applied){error="This pose was applied but saving was unconfirmed. Inspect or recover the room; do not save it again.";return false;}
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"Room editor is unavailable";return false;}
            if(pose.RoomSession!=editor.TemporarySessionId||editor.ObjectRevision(pose.Target)!=pose.Revision){error="Maestro or the room changed; discard this retained pose";return false;}
            var item=editor.Find(pose.Target);var model=item?item.GetComponent<MaestroAvatar>():null;
            if(!model||model.ModelBusy||editor.AnyHeld||model.PoseRig&&model.PoseRig.IsHolding){error="Finish loading and release objects or joints first";return false;}
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen){error="Room saving is unavailable";return false;}
            return editor.Ownership.CanAcquire(ownershipId,manual?RoomActorRole.Control:RoomActorRole.Program,RecordingClaims(pose.Target),out error);
        }
        // True means the session accepted a save request. Observe the completion
        // or animation.posing; acceptance alone is not a durable result.
        public bool SaveRetainedPose(out string error,bool manual=true)=>
            BeginPoseSave(poseSession,poseVersion,!posing,out _,out error,manual);
        public void SaveRetainedPose(){if(!SaveRetainedPose(out var error))Say(error);}
        public void DiscardRetainedPose()
        {
            if(!DiscardPose(poseSession,poseVersion,out _,out var error))Say(error);
        }
        internal bool DiscardPose(string session,int version,out Newtonsoft.Json.Linq.JObject result,out string error)
        {
            result=null;if(!CanUsePose(session,version,"discard",out error))return false;
            result=PoseResult("discarded");
            bool applied=pendingPose?.Applied==true;
            foreach(var save in poseSaves)save.Fail("Pose discarded before native acceptance");
            poseSaves.Clear();pendingPose=null;saveError=null;
            ReleasePoseControls();
            if(poseSession==session)EndPoseSession();
            Say(applied?"Pose buffer discarded — applied edits remain; recover saving before editing":"Unsaved pose discarded — previous saved poses remain");
            return true;
        }
    }
}
