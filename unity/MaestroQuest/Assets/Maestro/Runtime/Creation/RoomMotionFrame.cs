// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>A motion owner's stable authored space, sampled again at each physics/render use.
    /// Translation and yaw change presentation, not an action's stored trajectory.
    /// Scale, gravity-axis and lifetime changes require a fresh action.</summary>
    internal readonly struct RoomMotionFrame
    {
        readonly Transform owner;
        readonly float metresPerUnit;
        readonly bool admitted;
        internal const string Changed = "Room motion frame changed; restart the action";
        internal RoomMotionFrame(Transform source)
        {
            owner=source;var frame=new RoomFrame(source);metresPerUnit=frame.MetresPerUnit;
            admitted=source&&source.gameObject.activeInHierarchy&&frame.Valid&&Upright(frame);
        }
        static bool Upright(RoomFrame frame)=>Vector3.Dot(frame.DirectionToWorld(Vector3.up),Vector3.up)>.99999f;
        internal bool TryRead(out RoomFrame frame)
        {
            frame=new RoomFrame(owner);
            return admitted&&owner&&owner.gameObject.activeInHierarchy&&frame.Valid&&Upright(frame)&&
                Mathf.Abs(frame.MetresPerUnit-metresPerUnit)<=metresPerUnit*.00001f;
        }
    }
}
