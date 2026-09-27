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
    public sealed class RigidRoomItem : MonoBehaviour
    {
        RoomItem item;
        Rigidbody body;
        RoomPhysicsWorld world;
        PhysicsMaterial material;
        readonly HashSet<object> owners = new();
        ItemPhysics profile;
        bool wasMoving, canceled, geometryReady = true;
        float quietSince;
        Vector3 lastGoodPosition;
        Quaternion lastGoodRotation;
        public event Action<RoomItem> Settled;
        public bool GeometryReady => geometryReady;
        public bool Dynamic => profile != ItemPhysics.Fixed;
        public bool Simulating => body && !body.isKinematic;
        public bool AnimationOwned => owners.Count > 0;
        public void Initialize(RoomItem target)
        {
            item = target; body = target.GetComponent<Rigidbody>();
            body.maxLinearVelocity = 15; body.maxAngularVelocity = 30;
            body.solverIterations = 8; body.solverVelocityIterations = 3;
            material = new PhysicsMaterial("User item physics");
            foreach (var collider in item.Grab.colliders) collider.sharedMaterial = material;
            item.Grab.firstSelectEntered.AddListener(Grabbed);
            item.Grab.lastSelectExited.AddListener(Released);
            lastGoodPosition = transform.position; lastGoodRotation = transform.rotation;
        }
        public void Configure(RoomPhysicsWorld source, ItemPhysics value, float mass)
        {
            if (world != source) { if (world) world.Changed -= Refresh; world = source; if (world) world.Changed += Refresh; }
            profile = value; body.mass = Mathf.Clamp(mass, .05f, 20);
            material.bounciness = value == ItemPhysics.Bouncy ? .72f : .1f;
            material.dynamicFriction = value == ItemPhysics.Bouncy ? .45f : .6f;
            material.staticFriction = .65f;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            foreach (var collider in item.Grab.colliders) if (collider) collider.sharedMaterial = material;
            Refresh();
        }
        public void SetGeometryReady(bool ready) { geometryReady = ready; Refresh(); }
        public void SetAnimationOwner(object owner, bool owns)
        {
            if (owns) owners.Add(owner); else owners.Remove(owner);
            Refresh();
        }
        bool Allowed => Dynamic && geometryReady && owners.Count == 0 && world && world.CanSimulate(transform.position);
        void Grabbed(SelectEnterEventArgs _) { canceled = false; wasMoving = true; }
        void Released(SelectExitEventArgs args)
        {
            canceled = args.isCanceled;
            if (canceled) { StopVelocity(); item.Grab.throwOnDetach = false; }
            Refresh();
        }
        public void Refresh()
        {
            if (!body || !item || !item.Grab) return;
            bool allowed = Allowed;
            item.Grab.throwOnDetach = allowed && !canceled;
            item.Grab.throwVelocityScale = 1;
            item.Grab.throwAngularVelocityScale = 1;
            item.Grab.movementType = allowed ? XRBaseInteractable.MovementType.VelocityTracking : XRBaseInteractable.MovementType.Instantaneous;
            // XRI owns its body setup while held. Changing movementType above updates that setup too.
            if (item.Grab.isSelected) return;
            if (!allowed) StopVelocity();
            if (!allowed) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = !allowed; body.useGravity = allowed;
            // Speculative CCD also covers rotating/resized primitives and kinematic controller contacts.
            if (allowed) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = allowed ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            body.linearDamping = .02f; body.angularDamping = .08f;
        }
        public void StopVelocity()
        {
            if (body && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        public void Teleported()
        {
            StopVelocity(); canceled = true;
            lastGoodPosition = transform.position; lastGoodRotation = transform.rotation;
            if (body) { body.position = transform.position; body.rotation = transform.rotation; }
            Refresh();
        }
        public bool CanReceivePhysicsAction(out string error)
        {
            error=null;
            if(!body||!item||!item.Grab) {error="Object physics is unavailable";return false;}
            if(!Dynamic) {error="Choose solid or bouncy physics for this object first";return false;}
            if(!geometryReady) {error="Object collision geometry is still loading";return false;}
            if(item.Grab.isSelected) {error="Release the object before changing its motion";return false;}
            if(AnimationOwned) {error="An animation or carried prop owns this object";return false;}
            if(!world||!world.CanSimulate(transform.position)) {error="Start room physics with valid scanned surfaces first";return false;}
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
            if (!Allowed || item.Grab.isSelected || !float.IsFinite(velocity.sqrMagnitude) || !float.IsFinite(angularVelocity.sqrMagnitude)) return false;
            canceled = false; Refresh(); body.linearVelocity = Vector3.ClampMagnitude(velocity,15); body.angularVelocity = Vector3.ClampMagnitude(angularVelocity,30); body.WakeUp(); wasMoving = true; return true;
        }
        void FixedUpdate()
        {
            if (!body || !Dynamic) return;
            if (world && world.Running && !world.CanSimulate(transform.position) && !item.Grab.isSelected)
            {
                transform.SetPositionAndRotation(lastGoodPosition,lastGoodRotation); Teleported();
                world.PausePhysics(); return;
            }
            if (Allowed && !item.Grab.isSelected) { lastGoodPosition = transform.position; lastGoodRotation = transform.rotation; }
        }
        void LateUpdate()
        {
            if (!body || !Dynamic || AnimationOwned || item.Grab.isSelected) return;
            if (!Allowed && !body.isKinematic) Refresh();
            bool moving = !body.isKinematic && !body.IsSleeping() && (body.linearVelocity.sqrMagnitude > .0025f || body.angularVelocity.sqrMagnitude > .01f);
            if (moving) { quietSince = Time.unscaledTime; wasMoving = true; }
            else if (wasMoving && Time.unscaledTime - quietSince > .6f) { wasMoving = false; Settled?.Invoke(item); }
        }
        void OnDisable() { StopVelocity(); if (body) { body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.isKinematic = true; body.useGravity = false; } }
        void OnDestroy()
        {
            if (world) world.Changed -= Refresh;
            if (item && item.Grab) { item.Grab.firstSelectEntered.RemoveListener(Grabbed); item.Grab.lastSelectExited.RemoveListener(Released); }
            ArtResources.Release(material);
        }
    }
}
