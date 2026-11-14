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
        internal sealed class PendingTake {
            public RoomMotion Motion;public JointPose[] Pose;public bool SavePose;
            public int Revision;public string Error;public bool Applied;
        }
        public bool HasUnsavedRecording=>pendingTake!=null;
        internal bool IsSavingRecording=>recordingSave!=null&&!recordingSave.Ended;
        internal bool HasUnfinishedAuthoring(string id)=>ControlsTarget(id)||
            (IsRecording||HasUnsavedRecording)&&recordingTarget==id||pendingPose?.Target==id||pendingFrame?.Target==id;
        public string RecordingSessionId=>recordingSession;
        public JObject ObserveRecording()
        {
            bool active=IsRecording||HasUnsavedRecording;
            string error=(pendingTake?.Error??"").Replace("\r"," ").Replace("\n"," ");
            return new JObject {["sessionId"]=recordingSession,["target"]=active?recordingTarget:"",["phase"]=IsRecording?"recording":IsSavingRecording?"saving":HasUnsavedRecording?"unsaved":"idle",
                ["frames"]=recording?.Count??pendingTake?.Motion.frames.Length??0,["duration"]=recording!=null?recording[^1].time:pendingTake?.Motion.Duration??0,
                ["revision"]=pendingTake?.Revision??(active?editor.ObjectRevision(recordingTarget):0),["temporary"]=editor&&editor.TemporaryRoom,["error"]=error.Length>128?error[..128]:error};
        }
        static BehaviourCatalog.Claim[] RecordingClaims(string id)=>new[]{new BehaviourCatalog.Claim(id,"wholeTarget")};
        public bool CanStartRecording(string sessionId,string id,int revision,out string error,bool manual=false)
        {
            error="The recording session changed; inspect animation.recording first";
            if(sessionId!=recordingSession)return false;
            if(HasUnsavedFrameEdit){error=FrameEditPrompt;return false;}
            if(HasUnsavedPose){error=RetainedPosePrompt;return false;}
            if(IsRecording||HasUnsavedRecording){error="Finish or discard the current take first";return false;}
            if(!isActiveAndEnabled){error="The animation workshop is unavailable";return false;}
            if(!editor||editor.RuntimeGate.Held){error=editor?editor.RuntimeGate.Reason:"The room editor is unavailable";return false;}
            if(!editor.CanSaveRoom||editor.WriteGate.Frozen){error="The room is unavailable for saving";return false;}
            var item=editor.Find(id);if(!item||editor.ObjectRevision(id)!=revision){error="The object changed; inspect its current revision";return false;}
            if(editor.Read(id)?.modelGeometry?.meshCollision==true){error="Disable rigid mesh collision before recording object movement";return false;}
            if(item.PoseLocked){error="Scanned ink layers cannot record object movement";return false;}
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
            if(discard&&IsSavingRecording&&recordingSave.Accepted){error="The take has been applied and is still saving; wait for its outcome before discarding its buffer";return false;}
            if(!discard&&pendingTake?.Applied==true&&!IsSavingRecording){error="This take was already applied but saving was unconfirmed. Inspect or recover the room; do not save it again.";return false;}
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
        internal bool BeginFinishRecording(string sessionId,string id,out RecordingSaveAttempt attempt,out string error,Action<string> checkpoint=null)
        {
            attempt=null;if(!CanResolveRecording(sessionId,id,false,out error))return false;
            if(IsSavingRecording){attempt=recordingSave;return true;}
            FreezeTake();var take=pendingTake;take.Error=null;saveError=null;
            attempt=recordingSave=new RecordingSaveAttempt(this,take,sessionId,id,recordingRoomSession,checkpoint);
            // The frozen frames no longer need a live pose/controller owner.
            // Release before queued preparation; the take remains guarded by identity.
            ReleaseRecordingControls();
            if(!attempt.Start(out error))return false;
            Say("Saving recording — frames frozen; other objects can keep running");return true;
        }
        public bool DiscardRecording(string sessionId,out JObject result,out string error)
        {
            result=null;if(!CanResolveRecording(sessionId,null,true,out error))return false;
            recordingSave?.CancelBeforeAcceptance("Take discarded before saving");
            bool applied=pendingTake?.Applied==true;
            var view=ObserveRecording();result=RecordingResult("discarded",(int)view["frames"],(float)view["duration"],editor.ObjectRevision(recordingTarget));
            recording=null;pendingTake=null;recordingSave=null;recordingSession=Guid.NewGuid().ToString("N");saveError=null;
            ReleaseRecordingControls();Say(applied?"Take buffer discarded — the applied animation remains; recover saving before editing":"Take discarded — previous animation kept");return true;
        }
        public void ToggleRecord()
        {
            bool accepted=IsRecording||HasUnsavedRecording?BeginFinishRecording(recordingSession,recordingTarget,out _,out var error):StartRecording(recordingSession,targetId,targetId==null?0:editor.ObjectRevision(targetId),out _,out error,manual:true);
            if(!accepted)Say(error??saveError);
        }
        void FinishRecording(){if(IsRecording)BeginFinishRecording(recordingSession,recordingTarget,out _,out _);}
        public void DiscardTake(){if(!DiscardRecording(recordingSession,out _,out var error))Say(error);}
    }
}
