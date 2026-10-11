// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Transfers a sampled body's planar displacement to the room proxy without changing its visible pose.</summary>
    internal sealed class AvatarAuthoredTravel
    {
        readonly MaestroAvatar avatar;readonly ImportedModel model;readonly AvatarSpatialMotion spatial;
        readonly RoomMotionFrame space;
        readonly Func<float,bool> sample;readonly Vector3 start,scale,modelPosition;readonly Quaternion rotation;
        readonly Vector3 cycle;readonly float duration;readonly bool loop;
        Vector3 accepted,compensation;float acceptedTime,rise;
        public string Error {get;private set;}
        public AvatarAuthoredTravel(MaestroAvatar avatar,ImportedModel model,AvatarSpatialMotion spatial,Func<float,bool> sample,float duration,bool loop)
        {
            this.avatar=avatar;this.model=model;this.spatial=spatial;this.sample=sample;this.duration=duration;this.loop=loop;
            space=spatial.MotionFrame;scale=avatar.transform.lossyScale;modelPosition=model.transform.localPosition;
            if(!space.TryRead(out var roomFrame)){Error=RoomMotionFrame.Changed;return;}
            start=accepted=roomFrame.PointToRoom(avatar.transform.position);rotation=roomFrame.RotationToRoom(avatar.transform.rotation);
            sample(0);var first=Displacement();sample(duration);cycle=Displacement()-first;
        }
        Vector3 Displacement()=>Vector3.ProjectOnPlane(avatar.PoseRig.ImportedHipOffset,Vector3.up);
        public bool Sample(float time)
        {
            if(Error!=null)return false;
            if(!space.TryRead(out var roomFrame))return Fail(RoomMotionFrame.Changed);
            if(!spatial.CanContinueAuthored(out var error))return Fail(error);
            var frame=avatar.transform;
            if(Vector3.Distance(roomFrame.PointToRoom(frame.position),accepted)*roomFrame.MetresPerUnit>.002f||Quaternion.Angle(roomFrame.RotationToRoom(frame.rotation),rotation)>.01f||Vector3.Distance(frame.lossyScale,scale)>.0001f)return Fail("Authored motion stopped because Maestro's placement or size changed");
            float at=loop?time%duration:Mathf.Min(time,duration);int cycles=loop?(int)Math.Floor(time/duration):0;
            model.transform.localPosition=modelPosition;
            if(!sample(at))return Fail("The authored motion can no longer be sampled");
            var offset=Displacement();float nextRise=Mathf.Max(0,avatar.PoseRig.ImportedHipOffset.y)*scale.y;
            var next=roomFrame.PointToWorld(start)+frame.TransformVector(offset+cycle*cycles);
            if(!spatial.TryAuthoredStep(next,Mathf.Max(rise,nextRise),out error)){
                // Restore the last accepted sample before exposing an error to the owner.
                sample(acceptedTime);model.transform.localPosition=modelPosition+compensation;avatar.PoseRig.CaptureImportedPose();return Fail(error);
            }
            frame.position=next;
            // Counter-translate the model container. All nodes, including unexposed joints,
            // keep the original sampled world pose while the room proxy follows the body.
            compensation=-offset;model.transform.localPosition=modelPosition+compensation;
            accepted=roomFrame.PointToRoom(next);acceptedTime=at;rise=nextRise;avatar.PoseRig.CaptureImportedPose();return true;
        }
        bool Fail(string error){Error=error;return false;}
        public void End(){if(model)model.transform.localPosition=modelPosition;if(spatial)spatial.RememberAuthoredPlacement();}
    }
}
