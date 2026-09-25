// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class RoomPhysicsTests
    {
        GameObject root;
        RoomPhysicsWorld world;
        RoomItem ball;
        RigidRoomItem rigid;
        Rigidbody body;
        XRInteractionManager manager;
        float previousCaptureDelta;
        [UnitySetUp] public IEnumerator SetUp()
        {
            // XRI uses a short, timestamped throw buffer. Editor import/render
            // stalls must not age all samples out of this synthetic 72 Hz input.
            previousCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f/72;
            root = new GameObject("Synthetic physical room"); manager = root.AddComponent<XRInteractionManager>();
            world = root.AddComponent<RoomPhysicsWorld>(); RoomPhysicsLayers.Configure();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform,false);
            floor.transform.position = new Vector3(0,-.1f,0); floor.transform.localScale = new Vector3(10,.2f,10); floor.layer = RoomPhysicsLayers.Scanned;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.transform.SetParent(root.transform,false);
            sphere.transform.position = new Vector3(0,1.2f,1); sphere.transform.localScale = Vector3.one * .13f; sphere.layer = RoomPhysicsLayers.Item;
            ball = sphere.AddComponent<RoomItem>(); ball.Configure(new[] { sphere.GetComponent<Collider>() });
            rigid = sphere.AddComponent<RigidRoomItem>(); rigid.Initialize(ball); rigid.Configure(world,ItemPhysics.Bouncy,.6f); body = sphere.GetComponent<Rigidbody>();
            yield return null;
        }
        [UnityTest] public IEnumerator ScanGateGravityFloorBounceAndPauseUseActualPhysics()
        {
            world.StartPhysics(); yield return new WaitForSeconds(.15f);
            Assert.That(ball.transform.position.y,Is.EqualTo(1.2f).Within(.001f)); Assert.That(body.isKinematic,Is.True);
            world.SetSurfaces(true,"Synthetic room ready"); world.StartPhysics();
            bool fell = false, bounced = false;
            for (int i = 0; i < 65; i++)
            {
                yield return new WaitForFixedUpdate();
                fell |= body.linearVelocity.y < -1;
                bounced |= fell && body.linearVelocity.y > 1;
                Assert.That(ball.transform.position.y,Is.GreaterThan(.04f),"Floor penetration: body="+body.position.y+", radius="+ball.GetComponent<SphereCollider>().radius+", scale="+ball.transform.lossyScale+", dt="+Time.fixedDeltaTime);
            }
            Assert.That(fell && bounced,Is.True);
            world.PausePhysics(); var paused = ball.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(paused,ball.transform.position),Is.LessThan(.001f)); Assert.That(body.isKinematic,Is.True);
        }
        [UnityTest] public IEnumerator ThrowBouncesOffWallAndAnimationOwnershipStopsGravity()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform,false); wall.layer = RoomPhysicsLayers.Scanned;
            wall.transform.position = new Vector3(1,1,1); wall.transform.localScale = new Vector3(.08f,3,4);
            Physics.SyncTransforms(); world.SetSurfaces(true,"Ready"); world.StartPhysics();
            Assert.That(rigid.Launch(new Vector3(8,1,0),Vector3.up*3),Is.True);
            bool reversed = false;
            for (int i = 0; i < 25; i++) { yield return new WaitForFixedUpdate(); reversed |= body.linearVelocity.x < -1; Assert.That(ball.transform.position.x,Is.LessThan(1)); }
            Assert.That(reversed,Is.True);
            var animation = new object(); rigid.SetAnimationOwner(animation,true); var held = ball.transform.position;
            yield return new WaitForSeconds(.15f); Assert.That(Vector3.Distance(held,ball.transform.position),Is.LessThan(.001f));
            Assert.That(rigid.Launch(Vector3.up,Vector3.zero),Is.False);
            rigid.SetAnimationOwner(animation,false); Assert.That(body.isKinematic,Is.False);
            world.SetSurfaces(false,"Lost room"); Assert.That(body.isKinematic,Is.True); Assert.That(world.Running,Is.False);
        }
        [UnityTest] public IEnumerator GrabReleaseThrowsAndCanceledTrackingDoesNotFling()
        {
            world.SetSurfaces(true,"Ready"); world.StartPhysics();
            var hand = new GameObject("Synthetic controller"); hand.SetActive(false); hand.transform.SetParent(root.transform,false); hand.transform.position = new Vector3(0,1.2f,0);
            var ray = hand.AddComponent<XRRayInteractor>(); ray.enableUIInteraction = false; ray.interactionManager = manager; ray.keepSelectedTargetValid = true; ray.manipulateAttachTransform = false;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue, manualPerformed = true, manualValue = 1 };
            hand.SetActive(true); manager.SelectEnter((IXRSelectInteractor)ray,ball.Grab);
            for (int i = 0; i < 20; i++) { hand.transform.position += Vector3.right*.03f; yield return new WaitForSeconds(.02f); }
            ray.selectInput.manualPerformed = false; ray.selectInput.manualValue = 0;
            manager.SelectExit((IXRSelectInteractor)ray,ball.Grab);
            yield return null; yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x,Is.GreaterThan(.2f),"Release did not preserve hand velocity");
            ray.selectInput.manualPerformed = true; ray.selectInput.manualValue = 1;
            manager.SelectEnter((IXRSelectInteractor)ray,ball.Grab);
            for (int i = 0; i < 8; i++) { hand.transform.position += Vector3.right*.035f; yield return new WaitForFixedUpdate(); }
            hand.SetActive(false); yield return null; yield return new WaitForFixedUpdate();
            Assert.That(ball.Grab.isSelected,Is.False); Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.05f),"Tracking cancellation replayed a throw");
        }
        [UnityTest] public IEnumerator ControllerContactPushesAndRestoresHeldObjectExclusionAfterPause()
        {
            ball.transform.position = new Vector3(0,.067f,1); rigid.Teleported();
            world.SetSurfaces(true,"Ready"); world.StartPhysics();
            var owner = root.AddComponent<Maestro.Quest.Book.BookControllerInput>(); owner.PhysicsWorld = world;
            var hand = new GameObject("Physical test controller"); hand.SetActive(false); hand.transform.SetParent(root.transform,false);
            hand.transform.position = new Vector3(-.3f,.067f,1);
            var ray = hand.AddComponent<XRRayInteractor>(); ray.enableUIInteraction = false; ray.interactionManager = manager;
            ray.keepSelectedTargetValid = true; ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue };
            hand.SetActive(true);
            var contact = new GameObject("Physical test contact"); contact.transform.SetParent(root.transform,false);
            var pusher = contact.AddComponent<TrackedPusher>(); pusher.Source = hand.transform; pusher.Interactor = ray; pusher.Input = owner;
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 30; i++) { hand.transform.position += Vector3.right*.02f; yield return new WaitForFixedUpdate(); }
            Assert.That(ball.transform.position.x,Is.GreaterThan(.12f),"Tracked contact did not push the loose ball");
            ray.selectInput.manualPerformed = true; ray.selectInput.manualValue = 1;
            manager.SelectEnter((IXRSelectInteractor)ray,ball.Grab);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            var sphere = contact.GetComponent<SphereCollider>(); var target = ball.Grab.colliders[0];
            Assert.That(Physics.GetIgnoreCollision(sphere,target),Is.True);
            world.PausePhysics(); yield return new WaitForFixedUpdate();
            Assert.That(sphere.enabled,Is.False);
            world.StartPhysics(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(sphere.enabled,Is.True); Assert.That(Physics.GetIgnoreCollision(sphere,target),Is.True);
            hand.SetActive(false); yield return new WaitForFixedUpdate();
            Assert.That(sphere.enabled,Is.False);
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(root); Time.captureDeltaTime = previousCaptureDelta; yield return null; }
    }
}
