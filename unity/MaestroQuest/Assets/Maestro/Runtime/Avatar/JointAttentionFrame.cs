// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    // Measured only at construction/rest fitting. Bone axes need not match Unity's
    // forward axis. Parent motion carries the neutral frame, while gaze stays bounded.
    internal sealed class JointAttentionFrame
    {
        internal readonly Transform Joint, Parent;
        readonly Quaternion parentFrame, jointFrame;
        internal JointAttentionFrame(Transform actor, Transform joint)
        {
            Joint=joint; Parent=joint.parent;
            parentFrame=Quaternion.Inverse(Parent.rotation)*actor.rotation;
            jointFrame=Quaternion.Inverse(actor.rotation)*joint.rotation;
        }
        internal bool Valid=>Joint&&Parent&&Joint.parent==Parent&&Joint.gameObject.activeInHierarchy&&TwoBoneFootSolver.Uniform(Joint);
        internal Quaternion Neutral=>Parent.rotation*parentFrame;
        internal Quaternion Rotation(float yaw,float pitch)=>Neutral*Quaternion.Euler(pitch,yaw,0)*jointFrame;
        internal bool Angles(Vector3 point,out float yaw,out float pitch,out bool limited)
        {
            yaw=pitch=0;limited=false;
            if(!Valid||!Creation.RoomRecipe.Finite(point))return false;
            var local=Quaternion.Inverse(Neutral)*(point-Joint.position);
            if(local.sqrMagnitude<.000001f)return false;
            float y=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float p=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            limited=Mathf.Abs(y)>60||Mathf.Abs(p)>30;
            yaw=Mathf.Clamp(y,-60,60);pitch=Mathf.Clamp(p,-30,30);return true;
        }
    }
}
