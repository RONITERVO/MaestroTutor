// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Transfers accepted sampled body travel to the room proxy without changing its visible pose.</summary>
    internal sealed class AvatarAuthoredTravel
    {
        readonly MaestroAvatar avatar;readonly ImportedModel model;readonly AvatarSpatialMotion spatial;
        readonly RoomMotionFrame space;readonly RoomPlaybackClock clock;readonly ImportedMotionPose initial;readonly ImportedMotionTimeline timeline;
        readonly Func<float,bool> sample;readonly Vector3 start,scale,modelPosition;readonly Quaternion rotation;
        readonly Vector3 cycle;readonly float duration;readonly bool loop;
        RoomContactInputs inputs;Vector3 accepted,compensation;float acceptedTime,acceptedElapsed,rise,pendingTime;
        bool ended,hasAccepted;
        public string Error {get;private set;}
        internal bool Preparing {get;private set;}
        internal float RequestedTime {get;private set;}
        internal Vector3 BaseModelPosition=>modelPosition;
        bool Owned=>!ended&&avatar&&avatar.isActiveAndEnabled&&model&&avatar.CustomModel==model&&spatial&&spatial.CanContinueAuthored(out _);
        public AvatarAuthoredTravel(MaestroAvatar avatar,ImportedModel model,AvatarSpatialMotion spatial,Func<float,bool> sample,float duration,bool loop,ImportedMotionTimeline timeline,RoomPlaybackClock clock=null,ImportedMotionPose initial=null)
        {
            this.avatar=avatar;this.model=model;this.spatial=spatial;this.sample=sample;this.duration=duration;this.loop=loop;this.timeline=timeline;this.clock=clock??new RoomPlaybackClock();
            space=spatial.MotionFrame;scale=avatar.transform.lossyScale;modelPosition=model.transform.localPosition;this.initial=initial??new ImportedMotionPose(model,modelPosition);
            this.clock.Start();Renew();
            if(!space.TryRead(out var roomFrame)){Fail(RoomMotionFrame.Changed);return;}
            start=accepted=roomFrame.PointToRoom(avatar.transform.position);rotation=roomFrame.RotationToRoom(avatar.transform.rotation);
            if(timeline==null||!float.IsFinite(duration)||duration<=0||!sample(0)){Fail("The authored motion can no longer be sampled");return;}
            var first=Displacement();if(!sample(duration)){Fail("The authored motion can no longer be sampled");return;}cycle=Displacement()-first;Restore();
        }
        void Renew(){inputs?.Dispose();inputs=new RoomContactInputs(spatial.AuthoredEditor,"maestro",()=>Owned);}
        Vector3 Displacement()=>Vector3.ProjectOnPlane(avatar.PoseRig.ImportedHipOffset,Vector3.up);
        public bool Sample(float time)
        {
            if(Error!=null||ended)return false;
            if(!Owned)return Fail("Authored motion lost its target, tracking or room readiness");
            if(!space.TryRead(out var roomFrame))return Fail(RoomMotionFrame.Changed);
            if(!clock.Valid(out var error))return Fail(error);
            var frame=avatar.transform;
            if(Vector3.Distance(roomFrame.PointToRoom(frame.position),accepted)*roomFrame.MetresPerUnit>.002f||Quaternion.Angle(roomFrame.RotationToRoom(frame.rotation),rotation)>.01f||Vector3.Distance(frame.lossyScale,scale)>.0001f)return Fail("Authored motion stopped because Maestro's placement or size changed");
            // A pending pose owns its sample time. A later caller cannot queue a
            // jump through the waiting interval or skip the final requested pose.
            if(Preparing)time=pendingTime;
            RequestedTime=time;
            if(!timeline.Interval(hasAccepted?acceptedElapsed:0,time,duration,loop,out var times,out error))return Fail(error);
            var points=new Vector3[times.Length];var offsets=new Vector3[times.Length];var rises=new float[times.Length];
            Bounds body=default,ground=default;var previous=frame.position;float previousRise=rise;
            for(int i=0;i<times.Length;i++){
                model.transform.localPosition=modelPosition;
                if(!sample(times[i].At))return Fail("The authored motion can no longer be sampled");
                offsets[i]=Displacement();rises[i]=Mathf.Max(0,avatar.PoseRig.ImportedHipOffset.y)*scale.y;
                points[i]=roomFrame.PointToWorld(start)+frame.TransformVector(offsets[i]+cycle*times[i].Cycle);
                RoomWalkingPreparation.Envelopes(previous,points[i],.25f*scale.y,1.7f*scale.y+Mathf.Max(previousRise,rises[i]),out var stepBody,out var stepGround);
                if(i==0){body=stepBody;ground=stepGround;}else{body.Encapsulate(stepBody);ground.Encapsulate(stepGround);}
                previous=points[i];previousRise=rises[i];
            }
            if(inputs.SourceChanged){clock.Pause(this,true);spatial.ReleaseAuthoredInputs();Renew();}
            if(!inputs.Require(body,ground,avatar.GetComponent<RoomItem>().WaterTraversal.mode!="ignore",out error)){
                if(!inputs.Pending)return Fail(error);
                Preparing=true;pendingTime=time;clock.Pause(this,true);Restore();return false;
            }
            Preparing=false;clock.Pause(this,false);
            // A fitted prop can independently hold the same playback clock.
            if(clock.Paused){Restore();return false;}
            previous=frame.position;previousRise=rise;
            for(int i=0;i<points.Length;i++){
                if(!spatial.TryAuthoredStep(previous,points[i],Mathf.Max(previousRise,rises[i]),out error))return Fail(error);
                previous=points[i];previousRise=rises[i];
            }
            // Commit only after the whole sampled interval is ready and clear.
            // Intermediate sampling never publishes a pose or partial placement.
            int last=points.Length-1;frame.position=points[last];
            compensation=-offsets[last];model.transform.localPosition=modelPosition+compensation;
            accepted=roomFrame.PointToRoom(points[last]);acceptedTime=times[last].At;acceptedElapsed=time;rise=rises[last];hasAccepted=true;avatar.PoseRig.CaptureImportedPose();return true;
        }
        void Restore(){
            if(!model||!model.Ready||!avatar||!avatar.PoseRig||avatar.CustomModel!=model)return;
            if(hasAccepted){sample(acceptedTime);model.transform.localPosition=modelPosition+compensation;}else initial.Restore();
            avatar.PoseRig.CaptureImportedPose();
        }
        bool Fail(string error){Restore();Error=error;Preparing=false;inputs?.Dispose();if(spatial)spatial.ReleaseAuthoredInputs();clock.Pause(this,true);return false;}
        internal JObject Observe()=>ended?RoomContactInputs.State("inactive",false,null):Error!=null?RoomContactInputs.State("unavailable",false,Error):inputs.Observe();
        public void End(){if(ended)return;ended=true;inputs?.Dispose();clock.Pause(this,false);if(model)model.transform.localPosition=modelPosition;if(spatial)spatial.RememberAuthoredPlacement();}
    }
}
