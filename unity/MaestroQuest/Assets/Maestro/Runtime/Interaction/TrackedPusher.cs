// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Book;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Maestro.Quest.Interaction
{
    /// <summary>A small kinematic controller/hand volume pushes loose items without a rendered UI surface.</summary>
    public sealed class TrackedPusher : MonoBehaviour
    {
        public Transform Source;
        public XRRayInteractor Interactor;
        public BookControllerInput Input;
        Rigidbody body;
        SphereCollider sphere;
        readonly HashSet<Collider> ignored = new();
        readonly HashSet<Collider> next = new();
        void Awake()
        {
            gameObject.layer = RoomPhysicsLayers.Controller;
            body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            sphere = gameObject.AddComponent<SphereCollider>(); sphere.radius = .04f; sphere.enabled = false;
        }
        void FixedUpdate()
        {
            bool active = Source && Source.gameObject.activeInHierarchy && Input && Input.PhysicsWorld && Input.PhysicsWorld.Running;
            if (!active) { ResetIgnored(); sphere.enabled = false; return; }
            if (!sphere.enabled || (body.position-Source.position).sqrMagnitude > .25f)
            {
                ResetIgnored(); sphere.enabled = false; body.position = Source.position; body.rotation = Source.rotation;
                sphere.enabled = true;
            }
            else { body.MovePosition(Source.position); body.MoveRotation(Source.rotation); }
            next.Clear();
            foreach (var selected in Interactor.interactablesSelected) foreach (var collider in selected.colliders) if (collider) next.Add(collider);
            foreach (var collider in ignored) if (collider && !next.Contains(collider)) Physics.IgnoreCollision(sphere,collider,false);
            foreach (var collider in next) if (!ignored.Contains(collider)) Physics.IgnoreCollision(sphere,collider,true);
            ignored.Clear(); ignored.UnionWith(next);
        }
        void ResetIgnored()
        {
            foreach (var collider in ignored) if (collider) Physics.IgnoreCollision(sphere,collider,false);
            ignored.Clear();
        }
    }
}
