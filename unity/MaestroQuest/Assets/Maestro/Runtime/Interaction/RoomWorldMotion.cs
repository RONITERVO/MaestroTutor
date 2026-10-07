// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    // Implemented on an item's root when its pose belongs to physical tracking.
    internal interface IPhysicalRoomBinding { bool PhysicalFrame { get; } }

    /// <summary>Transfers one virtual frame and its native bodies without teleporting
    /// authored entities, changing action ownership, or adding artificial impulses.</summary>
    internal sealed class RoomWorldMotion
    {
        readonly Transform content;
        readonly RoomPhysicsWorld world;
        readonly List<Rigidbody> bodies=new();
        readonly List<BodyPose> poses=new();
        readonly List<Joint> joints=new();
        readonly Dictionary<Rigidbody,bool> physical=new();
        internal RoomWorldMotion(Transform owner,RoomPhysicsWorld physics) { content=owner;world=physics; }
        internal bool Ready=>content&&content.gameObject.activeInHierarchy&&FrameReady;
        bool FrameReady {
            get { var frame=new RoomFrame(content);return content&&frame.Valid&&
                Mathf.Abs(frame.MetresPerUnit-1)<.00001f&&Vector3.Dot(content.up,Vector3.up)>.99999f; }
        }
        RoomFrame preparedFrame;
        Vector3 preparedPosition;
        Quaternion preparedRotation;
        bool prepared;
        internal bool SetPose(Vector3 position,Quaternion rotation,out string error)
        {
            if(!PreparePose(position,rotation,out error))return false;
            ApplyPreparedPose();return true;
        }
        // Main-thread transaction: prepare, persist, then apply with no callbacks or
        // physics steps between. Admission must never fail after the durable write.
        internal bool PreparePose(Vector3 position,Quaternion rotation,out string error)
        {
            prepared=false;
            error="World movement needs an upright, unscaled content frame";
            if(!FrameReady||!RoomRecipe.Finite(position)||!MotionFrame.ValidRotation(rotation)||Vector3.Dot(rotation*Vector3.up,Vector3.up)<.99999f)return false;
            // Moving active virtual bodies relative to real colliders needs swept
            // admission for the entire assembly. Never silently teleport through it.
            if(world&&world.Running&&world.RealCollisions){error="Pause physics or turn off real-room collisions before moving the world";return false;}
            bodies.Clear();poses.Clear();physical.Clear();joints.Clear();content.GetComponentsInChildren(true,bodies);
            foreach(var body in bodies) {
                var item=body.GetComponent<RoomItem>();var binding=body.GetComponent<IPhysicalRoomBinding>();
                physical[body]=binding?.PhysicalFrame==true||item&&item.Grab&&item.Grab.isSelected;
            }
            content.GetComponentsInChildren(true,joints);
            foreach(var joint in joints) {
                var own=joint.GetComponent<Rigidbody>();
                if(!own||!physical.TryGetValue(own,out var fixedOwn))continue;
                bool fixedOther=!joint.connectedBody||!physical.TryGetValue(joint.connectedBody,out var fixedValue)||fixedValue;
                if(fixedOwn!=fixedOther){error="A connection crosses physical and virtual frames; release it before moving the world";return false;}
            }
            Physics.SyncTransforms();
            preparedFrame=new RoomFrame(content);
            foreach(var body in bodies)poses.Add(new BodyPose(body,physical[body]));
            preparedPosition=position;preparedRotation=rotation.normalized;prepared=true;
            error=null;return true;
        }
        internal void ApplyPreparedPose()
        {
            if(!prepared)throw new System.InvalidOperationException("World movement has not been prepared");
            prepared=false;
            var before=preparedFrame;
            content.SetPositionAndRotation(preparedPosition,preparedRotation);
            var after=new RoomFrame(content);
            // No callbacks or physics step run between sampling and restoring all
            // bodies. Reuse buffers; do not recreate joints or toggle kinematic state.
            foreach(var pose in poses)pose.Apply(before,after);
            Physics.SyncTransforms();
            // Pose/velocity assignments wake PhysX bodies. Restore sleeping only
            // after the whole island has moved and its transforms are synchronized.
            foreach(var pose in poses)if(pose.Sleeping&&!pose.Body.isKinematic)pose.Body.Sleep();
        }
        readonly struct BodyPose
        {
            internal readonly Rigidbody Body;
            internal readonly bool Sleeping;
            readonly bool fixedPose,kinematic;
            readonly Vector3 position,velocity,spin;
            readonly Quaternion rotation;
            readonly RigidRoomItem rigid;
            internal BodyPose(Rigidbody value,bool physical) {
                Body=value;fixedPose=physical;kinematic=value.isKinematic;Sleeping=!kinematic&&value.IsSleeping();
                position=value.position;rotation=value.rotation;velocity=kinematic?Vector3.zero:value.linearVelocity;spin=kinematic?Vector3.zero:value.angularVelocity;
                rigid=value.GetComponent<RigidRoomItem>();
            }
            internal void Apply(RoomFrame before,RoomFrame after) {
                var p=fixedPose?position:after.PointToWorld(before.PointToRoom(position));
                var q=fixedPose?rotation:after.RotationToWorld(before.RotationToRoom(rotation));
                Body.transform.SetPositionAndRotation(p,q);Body.position=p;Body.rotation=q;
                if(!kinematic){Body.linearVelocity=fixedPose?velocity:after.VectorToWorld(before.VectorToRoom(velocity));
                    Body.angularVelocity=fixedPose?spin:after.DirectionToWorld(before.DirectionToRoom(spin));}
                if(!fixedPose&&rigid)rigid.MoveFrame(before,after);
            }
        }
    }
}
