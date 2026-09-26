// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using UnityEngine;

namespace Maestro.Quest.Avatar
{
    // Sample after the avatar and its retargeter have presented this frame's pose.
    [DefaultExecutionOrder(250)]
    public sealed class AvatarHeldProp : MonoBehaviour
    {
        struct Sample { public float Time; public Vector3 Position; public Quaternion Rotation; }
        readonly List<Sample> samples=new();
        readonly Collider[] overlaps=new Collider[48];
        readonly RaycastHit[] hits=new RaycastHit[48];
        RoomEditor editor;
        MaestroAvatar avatar;
        RoomItem item;
        RigidRoomItem rigid;
        Rigidbody body;
        Transform hand;
        RuleStep step;
        Vector3 homePosition,centre;
        Quaternion homeRotation;
        float radius,began,duration,lastPoseAt;
        bool holding,requiresRoom,released;
        public string Error { get; private set; }
        public bool Released => released;
        public bool Holding => holding;
        public static bool CanAttach(RoomEditor editor,RuleStep step,out string error)
        {
            error=null;
            if (string.IsNullOrEmpty(step.propId)) return true;
            var avatar=editor.Find("maestro")?.GetComponent<MaestroAvatar>(); var item=editor.Find(step.propId);
            if (!RuleDocument.CanCarry(step) || !avatar || avatar.ModelBusy || !avatar.PoseRig || (avatar.ModelHash ?? "") != (step.propAvatarHash ?? ""))
            { error="Choose Maestro's current avatar and fit the prop again"; return false; }
            var hand=avatar.PoseRig.Bone(step.propHand == PropHand.Left ? PoseJoint.LeftHand : PoseJoint.RightHand);
            if (!hand) { error="This avatar has no mapped hand for the prop"; return false; }
            var rigid=item ? item.GetComponent<RigidRoomItem>() : null;
            if (!item || editor.Read(step.propId)?.IsBuiltIn != false || !rigid || !rigid.GeometryReady || item.Grab.isSelected)
            { error="Select a loaded creation and release it before using it as a prop"; return false; }
            if (step.propRelease != PropRelease.Return && (!rigid.Dynamic || !editor.PhysicsWorld || !editor.PhysicsWorld.CanSimulate(item.transform.position)))
            { error="Drop or throw needs a Solid/Bouncy prop and running aligned room physics"; return false; }
            return true;
        }
        public static AvatarHeldProp Begin(RoomEditor editor,RuleStep step,float duration,out string error)
        {
            if (!CanAttach(editor,step,out error)) return null;
            var item=editor.Find(step.propId); var rigid=item.GetComponent<RigidRoomItem>();
            if (rigid.AnimationOwned) { error="Another animation owns this prop"; return null; }
            var value=item.gameObject.AddComponent<AvatarHeldProp>(); value.editor=editor; value.item=item; value.rigid=rigid; value.body=item.GetComponent<Rigidbody>();
            value.avatar=editor.Find("maestro").GetComponent<MaestroAvatar>(); value.step=step.Copy();
            value.hand=value.avatar.PoseRig.Bone(step.propHand == PropHand.Left ? PoseJoint.LeftHand : PoseJoint.RightHand);
            value.homePosition=item.transform.localPosition; value.homeRotation=item.transform.localRotation;
            value.duration=duration; value.began=Time.unscaledTime; value.requiresRoom=editor.PhysicsWorld && editor.PhysicsWorld.Running;
            Physics.SyncTransforms(); var bounds=item.Grab.colliders[0].bounds;
            foreach (var collider in item.Grab.colliders) if (collider && collider.enabled) bounds.Encapsulate(collider.bounds);
            value.centre=item.transform.InverseTransformPoint(bounds.center); value.radius=bounds.extents.magnitude;
            rigid.SetAnimationOwner(value,true); value.holding=true;
            if (!value.Follow()) { error=value.Error; value.End(false); return null; }
            return value;
        }
        bool Obstacle(Collider collider,bool includeAvatar) => collider && !collider.isTrigger && !collider.transform.IsChildOf(item.transform) && (includeAvatar || !collider.transform.IsChildOf(avatar.transform));
        bool Clear(Vector3 position,Quaternion rotation,bool includeAvatar,bool sweep)
        {
            Physics.SyncTransforms();
            int mask=(1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment);
            var oldCentre=item.transform.TransformPoint(centre);
            var newCentre=position+rotation*Vector3.Scale(centre,item.transform.lossyScale);
            var delta=newCentre-oldCentre;
            if (sweep && delta.sqrMagnitude > .000001f)
            {
                int count=Physics.SphereCastNonAlloc(oldCentre,radius,delta.normalized,hits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                for (int i=0;i<count;i++) if (Obstacle(hits[i].collider,includeAvatar)) return false;
            }
            int found=Physics.OverlapSphereNonAlloc(newCentre,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if (found == overlaps.Length) return false;
            foreach (var collider in item.Grab.colliders)
            {
                if (!collider || !collider.enabled) continue;
                var relative=Quaternion.Inverse(item.transform.rotation)*(collider.transform.position-item.transform.position);
                var colliderPosition=position+rotation*relative;
                var colliderRotation=rotation*Quaternion.Inverse(item.transform.rotation)*collider.transform.rotation;
                for (int i=0;i<found;i++) if (Obstacle(overlaps[i],includeAvatar) && Physics.ComputePenetration(collider,colliderPosition,colliderRotation,overlaps[i],overlaps[i].transform.position,overlaps[i].transform.rotation,out _,out float depth) && depth > .001f) return false;
            }
            return true;
        }
        public bool Valid(out string error)
        {
            error=Error; if (error != null) return false;
            if (!holding) return true;
            if (!item || !avatar || !hand || avatar.ModelBusy || (avatar.ModelHash ?? "") != (step.propAvatarHash ?? "") || item.Grab.isSelected)
                error="Prop action stopped — its avatar or item changed";
            else if (requiresRoom && (!editor.PhysicsWorld || !editor.PhysicsWorld.CanSimulate(item.transform.position)))
                error="Prop action stopped — check room alignment and restart physics";
            if (error != null) Error=error;
            return error == null;
        }
        bool Follow()
        {
            if (!Valid(out _)) return false;
            float now=Time.unscaledTime;
            if (samples.Count > 0 && now-lastPoseAt > .25f) { Error="Motion was interrupted; try the prop action again"; return false; }
            float scale=avatar.transform.lossyScale.y;
            var position=hand.position+hand.rotation*(step.propOffset*scale); var rotation=hand.rotation*step.propRotation;
            if (!float.IsFinite(position.sqrMagnitude) || !MotionFrame.ValidRotation(rotation) ||
                requiresRoom && !editor.PhysicsWorld.CanSimulate(position) || !Clear(position,rotation,false,true))
            { Error="Prop path is blocked — adjust its fit, motion or Maestro's placement"; return false; }
            item.transform.SetPositionAndRotation(position,rotation); body.position=position; body.rotation=rotation;
            lastPoseAt=now;
            // Bound history by time as well as count, including fast desktop frames.
            if (samples.Count == 0 || now-samples[^1].Time >= .005f) samples.Add(new Sample { Time=now,Position=position,Rotation=rotation });
            while (samples.Count > 2 && (samples[1].Time < now-.1f || samples.Count > 24)) samples.RemoveAt(0);
            return true;
        }
        void LateUpdate()
        {
            if (!holding || Error != null) return;
            if (!Follow()) return;
            if (step.propRelease != PropRelease.Return && Time.unscaledTime-began >= duration*step.propReleaseAt) Release();
        }
        public void Finish()
        {
            if (holding && Error == null && step.propRelease != PropRelease.Return) Release();
        }
        bool Release()
        {
            if (!Valid(out _) || !holding) return false;
            if (!editor.PhysicsWorld || !editor.PhysicsWorld.CanSimulate(item.transform.position) || !Clear(item.transform.position,item.transform.rotation,true,false))
            { Error="Cannot release here — move the prop clear of Maestro and room surfaces"; return false; }
            var first=samples[0]; var last=new Sample { Time=Time.unscaledTime,Position=item.transform.position,Rotation=item.transform.rotation }; float dt=last.Time-first.Time;
            if (Time.unscaledTime-lastPoseAt > .25f) { Error="Motion was interrupted; try the prop action again"; return false; }
            Vector3 velocity=Vector3.zero,spin=Vector3.zero;
            if (step.propRelease == PropRelease.Throw)
            {
                if (dt < .02f) { Error="Throw needs more motion before release — choose a later release time"; return false; }
                velocity=(last.Position-first.Position)/dt;
                var delta=last.Rotation*Quaternion.Inverse(first.Rotation); delta.ToAngleAxis(out float angle,out var axis);
                if (angle > 180) angle-=360;
                if (Mathf.Abs(angle) > .001f) spin=axis*(angle*Mathf.Deg2Rad/dt);
            }
            holding=false; rigid.SetAnimationOwner(this,false);
            if (!rigid.Launch(velocity,spin)) { holding=true; rigid.SetAnimationOwner(this,true); Error="Room physics could not take ownership of the prop"; return false; }
            released=true; editor.RememberPlacement(step.propId); return true;
        }
        public void End(bool preservePlacement)
        {
            if (holding && item && !preservePlacement && !item.Grab.isSelected)
            { item.transform.SetLocalPositionAndRotation(homePosition,homeRotation); rigid.Teleported(); }
            holding=false; if (rigid) rigid.SetAnimationOwner(this,false);
            enabled=false; Destroy(this);
        }
        void OnDisable() { if (holding) End(false); }
        void OnDestroy() { if (holding) End(false); }
    }
}
