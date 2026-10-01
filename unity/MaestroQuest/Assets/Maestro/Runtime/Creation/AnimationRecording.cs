// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // One live authoring session. Session identities are never restored/replayed on launch.
    public sealed partial class AnimationWorkshop
    {
        string recordingSession=Guid.NewGuid().ToString("N"),recordingTarget,recordingRoomSession;
        bool resolvingRecording;
        PendingTake pendingTake;
        sealed class PendingTake {
            public RoomMotion Motion;public JointPose[] Pose;public bool SavePose;
            public int Revision;public string Error;
        }
        public bool HasUnsavedRecording=>pendingTake!=null;
        public string RecordingSessionId=>recordingSession;
        public JObject ObserveRecording()
        {
            bool active=IsRecording||HasUnsavedRecording;
            string error=(pendingTake?.Error??"").Replace("\r"," ").Replace("\n"," ");
            return new JObject {["sessionId"]=recordingSession,["target"]=active?recordingTarget:"",["phase"]=IsRecording?"recording":HasUnsavedRecording?"unsaved":"idle",
                ["frames"]=recording?.Count??pendingTake?.Motion.frames.Length??0,["duration"]=recording!=null?recording[^1].time:pendingTake?.Motion.Duration??0,
                ["revision"]=pendingTake?.Revision??(active?editor.ObjectRevision(recordingTarget):0),["temporary"]=editor&&editor.TemporaryRoom,["error"]=error.Length>128?error[..128]:error};
        }
        static BehaviourCatalog.Claim[] RecordingClaims(string id)=>new[]{new BehaviourCatalog.Claim(id,"wholeTarget")};
        public bool CanStartRecording(string sessionId,string id,int revision,out string error,bool manual=false)
        {
            error="The recording session changed; inspect animation.recording first";
            if(sessionId!=recordingSession)return false;
            if(IsRecording||HasUnsavedRecording){error="Finish or discard the current take first";return false;}
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"The room editor is unavailable";return false;}
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen){error="The room is unavailable for saving";return false;}
            var item=editor.Find(id);if(!item||editor.ObjectRevision(id)!=revision){error="The object changed; inspect its current revision";return false;}
            if(editor.AnyHeld||avatar&&avatar.PoseRig&&avatar.PoseRig.IsHolding){error="Release the object or joint before recording";return false;}
            if(controlling&&targetId!=id){error="Stop the other object's animation controls first";return false;}
            var model=item.GetComponent<MaestroAvatar>();if(model&&model.ModelBusy){error="Wait for Maestro to finish loading";return false;}
            // Only the trusted physical entry point may replace existing actors.
            // Agent/program starts use the same session but cannot promote their priority.
            if(!editor.Ownership.CanAcquire(ownershipId,manual?RoomActorRole.Control:RoomActorRole.Program,RecordingClaims(id),out error,replaceControl:manual))return false;
            if(!manual&&!ControlsTarget(id)&&item.GetComponent<RigidRoomItem>()?.AnimationOwned==true){error="Another animation or prop owns the object";return false;}
            error=null;return true;
        }
        public bool StartRecording(string sessionId,string id,int revision,out JObject result,out string error,bool manual=false)
        {
            result=null;if(!CanStartRecording(sessionId,id,revision,out error,manual))return false;
            editor.Select(editor.Find(id));SelectionChanged();StopPlayback();
            // Readiness already excludes conflicting actors. A global Starting
            // notification would cancel the very catalog invocation creating this take.
            if(!TakeControl(allowsGrab:true,notify:manual)){error=Status;return false;}
            recordingTarget=id;recordingRoomSession=editor.TemporarySessionId;
            recording=new List<MotionFrame>{Capture(0)};began=Time.unscaledTime;nextSample=.1f;saveError=null;
            result=RecordingResult("recording",recording.Count,0,editor.ObjectRevision(id));
            Say("Recording — Record saves; Discard take keeps the previous animation");return true;
        }
        public bool CanResolveRecording(string sessionId,string id,bool discard,out string error)
        {
            error="This take is no longer current; inspect animation.recording first";
            if(sessionId!=recordingSession||!IsRecording&&!HasUnsavedRecording||!discard&&id!=recordingTarget)return false;
            error=null;return true;
        }
        JObject RecordingResult(string phase,int frames,float duration,int revision)=>new() {
            ["sessionId"]=recordingSession,["target"]=recordingTarget,["phase"]=phase,["frames"]=frames,["duration"]=duration,["revision"]=revision,["temporary"]=editor.TemporaryRoom};
        void FreezeTake()
        {
            if(!IsRecording)return;
            float end=Mathf.Min(Time.unscaledTime-began,RoomMotion.MaximumSeconds);
            if(target&&end>recording[^1].time+.001f&&recording.Count<RoomMotion.MaximumFrames)recording.Add(Capture(end));
            pendingTake=new PendingTake {Motion=new RoomMotion {frames=recording.ToArray()},SavePose=posing,Pose=posing&&avatar?avatar.PoseRig.Capture():null,Revision=editor.ObjectRevision(recordingTarget)};
            recording=null;
        }
        void ReleaseRecordingControls()
        {
            bool previous=resolvingRecording;resolvingRecording=true;try{Stop();}finally{resolvingRecording=previous;}
        }
        public bool FinishRecording(string sessionId,string id,out JObject result,out string error)
        {
            result=null;if(!CanResolveRecording(sessionId,id,false,out error))return false;
            FreezeTake();var take=pendingTake;bool accepted=false;
            saving=true;
            try {
                if(recordingRoomSession!=editor.TemporarySessionId)error="The room session changed; discard this retained take";
                else accepted=editor.WriteAnimation(recordingTarget,take.Revision,take.Motion,take.Pose,take.SavePose,out error);
            } finally {saving=false;}
            if(!accepted){take.Error=error;saveError="Take not saved; frames retained. "+error;ReleaseRecordingControls();Say(saveError);error=saveError;return false;}
            result=RecordingResult("saved",take.Motion.frames.Length,take.Motion.Duration,editor.ObjectRevision(recordingTarget));
            pendingTake=null;recordingSession=Guid.NewGuid().ToString("N");selectedFrame=-1;saveError=null;
            ReleaseRecordingControls();Say("Recording saved — Undo restores the previous animation");return true;
        }
        public bool DiscardRecording(string sessionId,out JObject result,out string error)
        {
            result=null;if(!CanResolveRecording(sessionId,null,true,out error))return false;
            var view=ObserveRecording();result=RecordingResult("discarded",(int)view["frames"],(float)view["duration"],editor.ObjectRevision(recordingTarget));
            recording=null;pendingTake=null;recordingSession=Guid.NewGuid().ToString("N");saveError=null;
            ReleaseRecordingControls();Say("Take discarded — previous animation kept");return true;
        }
        public void ToggleRecord()
        {
            bool accepted=IsRecording||HasUnsavedRecording?FinishRecording(recordingSession,recordingTarget,out _,out var error):StartRecording(recordingSession,targetId,targetId==null?0:editor.ObjectRevision(targetId),out _,out error,manual:true);
            if(!accepted)Say(error??saveError);
        }
        void FinishRecording(){if(IsRecording)FinishRecording(recordingSession,recordingTarget,out _,out _);}
        public void DiscardTake(){if(!DiscardRecording(recordingSession,out _,out var error))Say(error);}
    }
}
