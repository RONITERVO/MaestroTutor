// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class AnimationWorkshop
    {
        string poseSession=Guid.NewGuid().ToString("N");
        int poseVersion;
        bool resolvingPose;
        public string PoseSessionId=>poseSession;
        public int PoseVersion=>poseVersion;
        void PoseEdited(){if(poseVersion==1000000){poseSession=Guid.NewGuid().ToString("N");poseVersion=0;}poseVersion++;}
        void EndPoseSession(){poseSession=Guid.NewGuid().ToString("N");poseVersion=0;}
        JObject PoseResult(string phase)=>new() {["sessionId"]=poseSession,["phase"]=phase,["version"]=poseVersion,
            ["revision"]=editor.ObjectRevision("maestro"),["temporary"]=editor.TemporaryRoom};
        public JObject ObservePosing()
        {
            var rig=editor?editor.Find("maestro")?.GetComponent<MaestroAvatar>()?.PoseRig:null;
            string error=(pendingPose?.Error??"").Replace("\r"," ").Replace("\n"," ");
            var joints=HasUnsavedPose?pendingPose.Joints:posing&&rig?rig.Capture():Array.Empty<JointPose>();
            return new JObject {["sessionId"]=poseSession,["phase"]=posing?"posing":HasUnsavedPose?"unsaved":"idle",["version"]=poseVersion,
                ["revision"]=pendingPose?.Revision??(editor?editor.ObjectRevision("maestro"):0),["holding"]=posing&&rig&&rig.IsHolding,
                ["temporary"]=editor&&editor.TemporaryRoom,["joints"]=new JArray(joints.Where(j=>HasUnsavedPose||rig&&rig.Bone(j.joint)).Select(j=>j.joint.ToString())),["error"]=error.Length>128?error[..128]:error};
        }
        public JObject ReadPoseJoint(string session,int version,PoseJoint joint)
        {
            if(session!=poseSession||version!=poseVersion||!posing&&!HasUnsavedPose)return null;
            if(posing&&(!avatar||avatar.PoseRig.IsHolding||!avatar.PoseRig.Bone(joint)))return null;
            var found=(pendingPose?.Joints??avatar.PoseRig.Capture()).FirstOrDefault(j=>j.joint==joint);if(found==null)return null;
            var q=found.rotation;return new JObject {["x"]=q.x,["y"]=q.y,["z"]=q.z,["w"]=q.w};
        }
        internal bool CanStartPose(string session,int revision,out string error,bool manual=false)
        {
            error="The pose session changed; inspect animation.posing first";if(session!=poseSession)return false;
            if(posing||HasUnsavedPose||IsRecording||HasUnsavedRecording||controlling){error="Finish or resolve current animation authoring first";return false;}
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"Room editor is unavailable";return false;}
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen){error="Room saving is unavailable";return false;}
            if(editor.DrawingInProgress){error="Finish the current stroke before posing";return false;}
            var model=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if(!model||!model.PoseRig||model.ModelBusy){error="Wait for Maestro to finish loading";return false;}
            if(editor.AnyHeld||model.PoseRig.IsHolding){error="Release objects and joints before posing";return false;}
            if(editor.ObjectRevision("maestro")!=revision){error="Maestro changed; inspect animation.posing again";return false;}
            return editor.Ownership.CanAcquire(ownershipId,manual?RoomActorRole.Control:RoomActorRole.Program,RecordingClaims("maestro"),out error,replaceControl:manual);
        }
        internal bool StartPose(string session,int revision,out JObject result,out string error,bool manual=false,JointPose[] initial=null)
        {
            result=null;if(!CanStartPose(session,revision,out error,manual))return false;
            editor.Select(editor.Find("maestro"));SelectionChanged();if((editor.DrawingMode||editor.SculptMode)&&!editor.PutPencilAwayForPose(out error))return false;
            if(!TakeControl(notify:manual)){error=Status;return false;}
            avatar.SetEditing(true);avatar.PoseRig.SetManual(true);avatar.PoseRig.Apply(initial);avatar.PoseRig.SetPosing(true);
            avatar.PoseRig.PoseChanged+=SavePose;avatar.PoseRig.PoseEdited+=PoseEdited;
            foreach(var collider in target.Grab.colliders)collider.enabled=false;
            target.Grab.enabled=false;posing=true;saveError=null;PoseEdited();result=PoseResult("posing");
            Say("Pose with joint handles or chat; Stop saves; recording captures movement");return true;
        }
        internal bool CanUsePose(string session,int version,string operation,out string error)
        {
            error="The pose changed; inspect animation.posing before editing";
            if(session!=poseSession||version!=poseVersion||!posing&&!HasUnsavedPose)return false;
            if(operation=="discard"&&HasUnsavedPose){error=null;return true;}
            if(HasUnsavedPose&&operation!="rotate")return CanSaveRetainedPose(out error);
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"Room editor is unavailable";return false;}
            if(editor.Ownership.Suspended){error="Room actions are paused";return false;}
            if(editor.WriteGate.Frozen){error="The workspace is being preserved";return false;}
            if(IsRecording&&operation!="rotate"||HasUnsavedRecording){error="Finish or discard the recording first";return false;}
            if(operation=="rotate"&&!posing){error="Save or discard the retained pose before editing";return false;}
            var model=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if(!model||model.ModelBusy||editor.AnyHeld||model.PoseRig.IsHolding){error="Finish loading and release objects or joints first";return false;}
            error=null;return true;
        }
        internal bool CanRotatePose(string session,int version,JointPose[] edits,out string error)
        {
            if(!CanUsePose(session,version,"rotate",out error))return false;
            if(edits==null||edits.Length is <1 or >8||!MotionFrame.ValidJoints(edits)||edits.Any(j=>!avatar.PoseRig.Bone(j.joint))){error="Choose distinct supported joints with normalized rotations";return false;}
            error=null;return true;
        }
        internal bool RotatePose(string session,int version,JointPose[] edits,out JObject result,out string error)
        {
            result=null;if(!CanRotatePose(session,version,edits,out error))return false;
            foreach(var edit in edits)avatar.PoseRig.RotateCanonical(edit.joint,edit.rotation);
            result=PoseResult("posing");Say(IsRecording?"Pose adjusted — recording continues":"Pose adjusted — save or finish to keep it");return true;
        }
        void ReleasePoseControls(){bool previous=resolvingPose;resolvingPose=true;try{Stop();}finally{resolvingPose=previous;}}
        internal bool ResolvePose(string session,int version,string operation,out JObject result,out string error)
        {
            result=null;if(!CanUsePose(session,version,operation,out error))return false;
            if(operation=="discard"){
                result=PoseResult("discarded");if(HasUnsavedPose)DiscardRetainedPose();else ReleasePoseControls();
                Say("Pose preview discarded — last saved pose kept");return true;
            }
            if(HasUnsavedPose){
                result=PoseResult("saved");if(!SaveRetainedPose(out error,manual:false)){result=null;return false;}
                result["revision"]=editor.ObjectRevision("maestro");return true;
            }
            if(!SaveCurrentPose()){error=pendingPose?.Error??Status;ReleasePoseControls();return false;}
            result=PoseResult(operation=="finish"?"saved":"posing");
            if(operation=="finish")ReleasePoseControls();
            Say(operation=="finish"?"Pose saved; editing finished":"Pose saved; editing continues");return true;
        }
    }
}
