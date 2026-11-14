// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    // Remove last frame's overlay before Maestro/retargeting evaluates its base pose.
    // RoomCatch explicitly applies the new overlay after those systems' LateUpdate.
    [DefaultExecutionOrder(90)]
    public sealed class AvatarCatchReach:MonoBehaviour
    {
        AvatarPoseRig rig;PoseJoint upper,lower,hand;Transform visibleUpper,visibleLower,visibleHand;
        Quaternion baseUpper,baseLower,lastUpper,lastLower,displayUpper,displayLower;bool applied,hasDisplay;
        public static AvatarCatchReach Begin(AvatarPoseRig rig,bool left){
            var value=rig.gameObject.AddComponent<AvatarCatchReach>();value.rig=rig;value.upper=left?PoseJoint.LeftUpperArm:PoseJoint.RightUpperArm;value.lower=left?PoseJoint.LeftLowerArm:PoseJoint.RightLowerArm;value.hand=left?PoseJoint.LeftHand:PoseJoint.RightHand;
            value.visibleUpper=rig.Bone(value.upper);value.visibleLower=rig.Bone(value.lower);value.visibleHand=rig.Bone(value.hand);
            if(!value.Valid){Destroy(value);return null;}return value;
        }
        public bool Valid=>rig&&visibleUpper&&visibleLower&&visibleHand&&rig.Bone(upper)==visibleUpper&&rig.Bone(lower)==visibleLower&&rig.Bone(hand)==visibleHand&&rig.CanonicalBone(upper)&&rig.CanonicalBone(lower)&&!rig.IsPosing;
        void Update()=>Restore();
        void Restore(){
            if(!applied)return;applied=false;if(!rig)return;
            var u=rig.CanonicalBone(upper);var l=rig.CanonicalBone(lower);
            // Never overwrite a pose authored after our overlay.
            if(u&&Quaternion.Angle(u.localRotation,lastUpper)<.01f)u.localRotation=baseUpper;
            if(l&&Quaternion.Angle(l.localRotation,lastLower)<.01f)l.localRotation=baseLower;
            rig.Apply(Array.Empty<JointPose>());
        }
        public bool Apply(Vector3 goal,RoomItem ball,RoomItem holder,Transform viewer,CatchClearance clearance,float dt,RoomPhysicsWorld world=null){
            if(!Valid||!float.IsFinite(dt)||dt<=0||dt>.25f)return false;
            Restore();var u=rig.CanonicalBone(upper);var l=rig.CanonicalBone(lower);baseUpper=u.localRotation;baseLower=l.localRotation;
            Vector3 pole=holder.transform.right*(upper==PoseJoint.LeftUpperArm?-1:1)-holder.transform.up;
            if(!CatchGeometry.TwoBone(visibleUpper.position,visibleLower.position,visibleHand.position,goal,pole,out var elbow,out var tip))return false;
            float armRadius=.035f*Mathf.Clamp(holder.transform.lossyScale.y,.25f,2);
            if(!clearance.Segment(ball,holder,viewer,visibleUpper.position,elbow,armRadius,world)||!clearance.Segment(ball,holder,viewer,elbow,tip,armRadius,world))return false;
            var rotation=Quaternion.FromToRotation(visibleLower.position-visibleUpper.position,elbow-visibleUpper.position)*visibleUpper.rotation;
            rig.Rotate(upper,Quaternion.RotateTowards(hasDisplay?rig.transform.rotation*displayUpper:visibleUpper.rotation,rotation,360*dt));
            rotation=Quaternion.FromToRotation(visibleHand.position-visibleLower.position,tip-visibleLower.position)*visibleLower.rotation;
            rig.Rotate(lower,Quaternion.RotateTowards(hasDisplay?rig.transform.rotation*displayLower:visibleLower.rotation,rotation,360*dt));
            lastUpper=u.localRotation;lastLower=l.localRotation;applied=true;
            // Joint limits and a moving torso may produce a different pose than the ideal solve.
            if(!clearance.Segment(ball,holder,viewer,visibleUpper.position,visibleLower.position,armRadius,world)||!clearance.Segment(ball,holder,viewer,visibleLower.position,visibleHand.position,armRadius,world)){Restore();rig.Apply(new[]{new JointPose{joint=upper,rotation=u.localRotation},new JointPose{joint=lower,rotation=l.localRotation}});return false;}
            displayUpper=Quaternion.Inverse(rig.transform.rotation)*visibleUpper.rotation;displayLower=Quaternion.Inverse(rig.transform.rotation)*visibleLower.rotation;hasDisplay=true;return true;
        }
        public void End(){Restore();enabled=false;Destroy(this);}
        void OnDisable()=>Restore();void OnDestroy()=>Restore();
    }
}
