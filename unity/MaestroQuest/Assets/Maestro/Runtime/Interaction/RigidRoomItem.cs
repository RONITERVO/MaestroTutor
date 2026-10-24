// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Maestro.Quest.Interaction
{
    public enum ItemPhysics { Fixed, Solid, Bouncy }
    public enum ItemCollider { Automatic, Box, Sphere }

    /// <summary>Coordinates PhysX, XRI and animation ownership; never replays an old impulse after interruption.</summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class RigidRoomItem : MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner
    {
        RoomItem item;
        Rigidbody body;
        RoomPhysicsWorld world;
        RoomGrabContactInputs grabInputs;
        internal bool ContactTracking=>RoomEnvironmentBinding.RestorationReady(item)&&isActiveAndEnabled&&item&&item.isActiveAndEnabled&&world&&world.Running&&Dynamic&&!AnimationOwned;
        internal bool ContactEnvironmentReady=>InputBindingAvailable&&geometryReady&&world&&world.CanSimulate(transform.position,item);
        PhysicsMaterial material;
        Creation.NativePhysicsResources.Lease nativeBody;
        readonly HashSet<object> owners = new();
        readonly HashSet<object> constraints = new();
        public void SetConstraintBlocked(object owner,bool blocked){bool changed=blocked?constraints.Add(owner):constraints.Remove(owner);if(changed){MotionRevision++;Refresh();}}
        ItemPhysics profile;
        bool wasMoving, canceled, boundaryActive, geometryReady = true;
        float quietSince;
        Vector3 lastGoodPosition;
        Quaternion lastGoodRotation;
        public event Action<RoomItem> Settled;
        public event Action<RoomItem,Collider,Vector3,float> ContactStarted;
        bool RecordCollision(Collision collision) {
            InputEditor?.RecordPhysicsContact(this,collision.collider);
            if(!collision.collider||collision.collider.GetComponent<Creation.RoomCollisionProxy>() is not Creation.RoomCollisionProxy proxy)return false;
            InputEditor?.QueueDormantContact(this,proxy);return true;
        }
        void OnCollisionStay(Collision collision)=>RecordCollision(collision);
        void OnCollisionEnter(Collision collision) {
            if(RecordCollision(collision))return;
            if(ContactStarted==null||!isActiveAndEnabled||!geometryReady||AnimationOwned||!world||!world.CanSimulate(transform.position,item)||Dynamic&&!(InputEditor?InputEditor.PhysicsStepDynamic(this):Allowed)||collision.contactCount==0)return;
            // Unity may reuse Collision objects. Copy the observation immediately;
            // no callback object or mutable body state enters the program queue.
            var point=collision.GetContact(0).point;float speed=collision.relativeVelocity.magnitude;
            if(!float.IsFinite(point.sqrMagnitude)||!float.IsFinite(speed))return;
            ContactStarted.Invoke(item,collision.collider,point,speed);
        }
        public uint MotionRevision {get;private set;}
        public uint PlacementRevision {get;private set;}
        public bool TryReadMotion(out bool available,out float speed,out float angularSpeed) {
            available=false;speed=angularSpeed=0;
            if(!isActiveAndEnabled||!body||!item||!item.isActiveAndEnabled||!item.Grab||!Dynamic)return false;
            available=Allowed&&!item.Grab.isSelected&&!body.isKinematic;
            if(available){speed=body.linearVelocity.magnitude;angularSpeed=body.angularVelocity.magnitude;}
            return float.IsFinite(speed)&&float.IsFinite(angularSpeed);
        }
        public bool GeometryReady => geometryReady;
        public bool Dynamic => profile != ItemPhysics.Fixed;
        public bool Simulating => body && !body.isKinematic;
        public bool AnimationOwned => owners.Count > 0;
        internal bool OwnedOnlyBy(object owner)=>owner!=null&&owners.Count==1&&owners.Contains(owner);
        // A pending placement edit holds only its own body, not the world. A
        // cancelled unchanged hold restores the admitted trajectory; a teleport,
        // grab, physics transition or competing owner invalidates that trajectory.
        internal IDisposable HoldForEdit(object owner)
        {
            if(owner==null||owners.Count!=0)throw new InvalidOperationException("The object already has a motion owner");
            var velocity=FlowMotion;SetAnimationOwner(owner,true);
            return new EditHold(this,owner,MotionRevision,velocity.Linear,velocity.Angular);
        }
        sealed class EditHold:IDisposable
        {
            RigidRoomItem source;readonly object owner;readonly uint revision;readonly Vector3 linear,angular;
            internal EditHold(RigidRoomItem source,object owner,uint revision,Vector3 linear,Vector3 angular)
            {this.source=source;this.owner=owner;this.revision=revision;this.linear=linear;this.angular=angular;}
            public void Dispose()
            {
                var previous=source;source=null;if(!previous)return;
                bool restore=previous.OwnedOnlyBy(owner)&&previous.MotionRevision==revision;
                previous.SetAnimationOwner(owner,false);
                if(!restore||!previous.body||previous.item.Grab.isSelected)return;
                previous.preparedVelocity=linear;previous.preparedSpin=angular;previous.preparedMomentum=true;
                previous.Refresh();
            }
        }
        public void Initialize(RoomItem target)=>InitializePrepared(target,null);
        internal void InitializePrepared(RoomItem target,Creation.NativePhysicsResources.Lease reservation)
        {
            nativeBody=reservation;
            item = target; body = target.GetComponent<Rigidbody>();
            grabInputs=target.gameObject.AddComponent<RoomGrabContactInputs>();grabInputs.Initialize(target,this);
            body.maxLinearVelocity = 15; body.maxAngularVelocity = 30;
            body.solverIterations = 8; body.solverVelocityIterations = 3;
            material = new PhysicsMaterial("User item physics");nativeBody?.OwnMaterial(material);
            foreach (var collider in item.Grab.colliders) collider.sharedMaterial = material;
            item.Grab.firstSelectEntered.AddListener(Grabbed);
            item.Grab.lastSelectExited.AddListener(Released);
            lastGoodPosition = transform.position; lastGoodRotation = transform.rotation;
        }
        public void Configure(RoomPhysicsWorld source, ItemPhysics value, float mass)
        {
            if(world!=source||profile!=value||body.mass!=Mathf.Clamp(mass,.05f,20)){grabInputs?.Suspend();InterruptPhysicsInputs();MotionRevision++;}
            if (world != source) { if (world) world.Changed -= PhysicsChanged; world = source; if (world) world.Changed += PhysicsChanged; }
            profile = value; body.mass = Mathf.Clamp(mass, .05f, 20);
            material.bounciness = value == ItemPhysics.Bouncy ? .72f : .1f;
            material.dynamicFriction = value == ItemPhysics.Bouncy ? .45f : .6f;
            material.staticFriction = .65f;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            foreach (var collider in item.Grab.colliders) if (collider) collider.sharedMaterial = material;
            Refresh();
        }
        public void SetGeometryReady(bool ready) { if(geometryReady!=ready){grabInputs?.Suspend();InterruptPhysicsInputs();MotionRevision++;}geometryReady = ready; Refresh(); }
        public void SetAnimationOwner(object owner, bool owns)
        {
            bool changed=owns?owners.Add(owner):owners.Remove(owner);if(changed){grabInputs?.Suspend();InterruptPhysicsInputs();MotionRevision++;}
            Refresh();
        }
        void PhysicsChanged(){if(!ContactTracking){grabInputs?.Suspend();InterruptPhysicsInputs();}MotionRevision++;Refresh();}
        // Owning preparation does not admit motion: virtual ground may still be dormant.
        bool PhysicsPreparationEnabled => !nativeResourcesReleased && InputBindingAvailable && isActiveAndEnabled && item && item.isActiveAndEnabled && Dynamic && geometryReady && owners.Count == 0 && world && world.SimulationActive;
        bool PhysicsEnabled => PhysicsPreparationEnabled && world.CanSimulate(transform.position,item);
        bool BaseAllowed=>PhysicsEnabled&&!collisionInterrupted&&constraints.Count==0;
        // Held-body sweeps own contact inputs; XRI retains its body/throw setup.
        bool PhysicsInputsAdmitted => item&&item.Grab&&item.Grab.isSelected ? (!grabInputs||grabInputs.Ready)&&(!InputEditor||InputEditor.PhysicsIslandReady(item,true,out _)) : InputsCurrent;
        bool Allowed => BaseAllowed && PhysicsInputsAdmitted;
        void Grabbed(SelectEnterEventArgs _) { grabInputs?.Reset();InterruptPhysicsInputs();MotionRevision++;boundaryActive=false;canceled = false; wasMoving = true; Refresh(); }
        void Released(SelectExitEventArgs args)
        {
            bool staleContact=grabInputs&&!grabInputs.FinishGrab();
            InterruptPhysicsInputs();MotionRevision++;canceled = args.isCanceled||staleContact;
            var restored=item.GetComponent<RoomEnvironmentBinding>();
            if(restored&&restored.RestorationHeld){bool deliberate=!args.isCanceled;canceled=true;StopVelocity();if(deliberate)restored.RetryRestoration();}
            if (canceled) { StopVelocity(); item.Grab.throwOnDetach = false; }
            Refresh();
        }
        public void Refresh()
        {
            if (!body || !item || !item.Grab) return;
            // Reconciliation configures members before all peers exist. There is
            // no PhysX step inside publication; recheck the complete accepted set.
            if(InputEditor?.SpatialInputsChanging==true){refreshAfterPublication=true;return;}
            refreshAfterPublication=false;
            // The body override affects contacts only; scan geometry remains available
            // for physical placement, visual overlays and independent acoustics.
            int scanBit=1<<RoomPhysicsLayers.Scanned;
            body.excludeLayers=world&&!world.IncludesRealRoom(item)?body.excludeLayers.value|scanBit:body.excludeLayers.value&~scanBit;
            if(InputEditor)InputEditor.PreparePhysicsIsland(item);else PreparePhysicsInputs();
            bool allowed = Allowed;
            if(!world||!world.Running||!Dynamic||!geometryReady||AnimationOwned)boundaryActive=false;
            item.Grab.throwOnDetach = allowed && !canceled && (!grabInputs||grabInputs.ThrowReady);
            item.Grab.throwVelocityScale = 1;
            item.Grab.throwAngularVelocityScale = 1;
            item.Grab.movementType = allowed ? XRBaseInteractable.MovementType.VelocityTracking : XRBaseInteractable.MovementType.Instantaneous;
            // XRI owns its body setup while held. Changing movementType above updates that setup too.
            if (item.Grab.isSelected) return;
            if (!allowed) ClearBodyVelocity();
            if (!allowed) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = !allowed; body.useGravity = allowed;
            // Speculative CCD also covers rotating/resized primitives and kinematic controller contacts.
            if (allowed) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = allowed ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            body.linearDamping = .02f; body.angularDamping = .08f;
            if(allowed)RestorePreparedMotion();
        }
        public void StopVelocity(){DiscardPreparedMotion();ClearBodyVelocity();}
        void ClearBodyVelocity()
        {
            if (body && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        public void Teleported()
        {
            grabInputs?.Suspend();InterruptPhysicsInputs();PlacementRevision++;MotionRevision++;boundaryActive=false;StopVelocity(); canceled = true;
            lastGoodPosition = transform.position; lastGoodRotation = transform.rotation;
            if (body) { body.position = transform.position; body.rotation = transform.rotation; }
            Refresh();
        }
        internal void MoveFrame(Creation.RoomFrame before,Creation.RoomFrame after)
        {
            if(preparedMomentum){
                preparedVelocity=after.VectorToWorld(before.VectorToRoom(preparedVelocity));
                preparedSpin=after.DirectionToWorld(before.DirectionToRoom(preparedSpin));
            }
            lastGoodPosition=after.PointToWorld(before.PointToRoom(lastGoodPosition));
            lastGoodRotation=after.RotationToWorld(before.RotationToRoom(lastGoodRotation));
        }
        internal string MotionUnavailableReason => !CanReceivePhysicsAction(out var reason)?reason:"Waiting for object physics to resume";
        public bool CanReceivePhysicsAction(out string error)
        {
            error=null;
            if(nativeResourcesReleased||!body||!item||!item.Grab) {error="Object physics is unavailable";return false;}
            if(!Dynamic) {error="Choose solid or bouncy physics for this object first";return false;}
            if(!geometryReady) {error="Object collision geometry is still loading";return false;}
            if(item.Grab.isSelected) {error="Release the object before changing its motion";return false;}
            if(constraints.Count>0) {error="A physical connection is suspended; inspect its state";return false;}
            if(AnimationOwned) {error="An animation or carried prop owns this object";return false;}
            if(item.TryGetComponent<RoomEnvironmentBinding>(out var restored)&&restored.RestorationHeld){error=restored.RestorationReason;return false;}
            if(!world||!world.CanSimulate(transform.position,item)) {error="Start physics with the selected ground ready first";return false;}
            if(!InputsCurrent){error=PhysicsInputStatus;return false;}
            return true;
        }
        // Impulse is in Newton-seconds, in world axes. Reuse the same launch
        // limits and ownership checks as a controller/recorded throw.
        public bool ApplyImpulse(Vector3 impulse,out string error)
        {
            if(!CanReceivePhysicsAction(out error))return false;
            if(!float.IsFinite(impulse.sqrMagnitude)) {error="Invalid impulse";return false;}
            if(Launch(body.linearVelocity+impulse/body.mass,body.angularVelocity))return true;
            error="Object physics changed before the push";return false;
        }
        public bool ClearMotion(out string error)
        {
            if(!CanReceivePhysicsAction(out error))return false;
            StopVelocity();return true; // Gravity/collisions continue; this is not a freeze.
        }
        public bool Launch(Vector3 velocity, Vector3 angularVelocity)
        {
            Refresh();
            if (!Allowed || item.Grab.isSelected || !float.IsFinite(velocity.sqrMagnitude) || !float.IsFinite(angularVelocity.sqrMagnitude)) return false;
            canceled = false; Refresh(); body.linearVelocity = Vector3.ClampMagnitude(velocity,15); body.angularVelocity = Vector3.ClampMagnitude(angularVelocity,30); body.WakeUp(); wasMoving = true; return true;
        }
        internal bool PhysicsBoundaryExceeded=>boundaryActive&&!collisionInterrupted&&InputBindingAvailable&&ContactTracking&&geometryReady&&item.Grab&&!item.Grab.isSelected&&world.SimulationActive&&!world.CanSimulate(body.position,item);
        internal void ResetAcceptedPhysicsPose()=>boundaryActive=false;
        internal bool HasAcceptedPhysicsPose=>boundaryActive&&body&&float.IsFinite(lastGoodPosition.sqrMagnitude)&&float.IsFinite(Quaternion.Dot(lastGoodRotation,lastGoodRotation));
        internal void RememberAcceptedPhysicsPose(){
            if(!body||body.isKinematic||!Allowed||item.Grab.isSelected)return;
            boundaryActive=true;lastGoodPosition=body.position;lastGoodRotation=body.rotation;
        }
        internal void RestoreAcceptedPhysicsPose(){
            if(!HasAcceptedPhysicsPose)return;
            // This is an interrupted native step, not a new user teleport. Keep
            // its observations and hold until every affected actor is restored.
            transform.SetPositionAndRotation(lastGoodPosition,lastGoodRotation);
            body.position=lastGoodPosition;body.rotation=lastGoodRotation;PlacementRevision++;
        }
        void FixedUpdate()
        {
            if (!body || !Dynamic) return;
            if(InputEditor)InputEditor.PreparePhysicsIsland(item);else PreparePhysicsInputs(); // Before PhysX and liquid forces.
            if(Allowed==body.isKinematic)Refresh();
            // Unsupported dormant items do not stop an otherwise valid region. Only
            // a body that was actually simulating can cross its admitted boundary.
            if (PhysicsBoundaryExceeded)
            {
                if(InputEditor)InputEditor.InterruptPhysicsBoundaries();
                else { InterruptAtCollisionGuard();RestoreAcceptedPhysicsPose();Refresh(); }
                return;
            }
            if(!body.isKinematic&&!Creation.PhysicsWorkResources.Consume(this,out _)){
                HoldPreparedMotion();SetInputsReady(false);Refresh();return;
            }
            RememberAcceptedPhysicsPose();
        }
        void LateUpdate()
        {
            if (!body || !Dynamic || AnimationOwned || item.Grab.isSelected) return;
            // A newly accepted actor may become ready after collider synchronization,
            // even when another profile kept the global world ready throughout.
            if (Allowed == body.isKinematic) Refresh();
            if(PhysicsPreparationEnabled&&(!PhysicsEnabled||collisionInterrupted||!InputsCurrent)){quietSince=Time.unscaledTime;return;} // Missing ground or pending inputs are not settlement.
            bool moving = !body.isKinematic && !body.IsSleeping() && (body.linearVelocity.sqrMagnitude > .0025f || body.angularVelocity.sqrMagnitude > .01f);
            if (moving) { quietSince = Time.unscaledTime; wasMoving = true; }
            else if (wasMoving && Time.unscaledTime - quietSince > .6f) { wasMoving = false; Settled?.Invoke(item); }
        }
        void OnDisable() { Creation.PhysicsWorkResources.Release(this);grabInputs?.Reset();InterruptPhysicsInputs();MotionRevision++;boundaryActive=false;StopVelocity(); if (body) { body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.isKinematic = true; body.useGravity = false; } }
        bool nativeResourcesReleased;
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void OnDestroy()=>ReleaseNativeResources();
        void ReleaseNativeResources()
        {
            if(nativeResourcesReleased)return;nativeResourcesReleased=true;Creation.PhysicsWorkResources.Release(this);InterruptPhysicsInputs();
            StopVelocity();if(body){body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.isKinematic=true;body.useGravity=false;}
            if (world) world.Changed -= PhysicsChanged;
            if (item && item.Grab) { item.Grab.firstSelectEntered.RemoveListener(Grabbed); item.Grab.lastSelectExited.RemoveListener(Released); }
            if(nativeBody!=null)nativeBody.Dispose();else ArtResources.Release(material);
        }
    }
}
