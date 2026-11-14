// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Interaction;
using Maestro.Quest.Book;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class RoomInteractionTests
    {
        GameObject root;
        XRInteractionManager manager;
        RoomItem item;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Interaction test");
            manager = root.AddComponent<XRInteractionManager>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false); cube.transform.localPosition = Vector3.forward;
            cube.transform.localScale = Vector3.one * .15f;
            item = cube.AddComponent<RoomItem>(); item.Configure(new[] { cube.GetComponent<Collider>() });
            yield return null;
        }

        XRRayInteractor Hand(Vector3 position)
        {
            var hand = new GameObject("Test hand"); hand.SetActive(false); hand.transform.SetParent(root.transform, false);
            hand.transform.position = position;
            var ray = hand.AddComponent<XRRayInteractor>();
            ray.enableUIInteraction = false; ray.interactionManager = manager;
            ray.keepSelectedTargetValid = true; ray.manipulateAttachTransform = false;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue, manualPerformed = true, manualValue = 1 };
            hand.SetActive(true); return ray;
        }

        [UnityTest]
        public IEnumerator PalmRecoveryNeedsTheOtherHandAndHidesOnTrackingLoss()
        {
            var room = root.AddComponent<RoomInteraction>(); room.Register(item,true);
            var viewer = new GameObject("Viewer"); viewer.transform.SetParent(root.transform,false); viewer.transform.position = new Vector3(0,1.55f,0); room.Viewer = viewer.transform;
            var world = root.AddComponent<RoomPhysicsWorld>(); world.SetSurfaces(true,"Ready"); world.StartPhysics();
            var control = new GameObject("Palm recovery"); control.transform.SetParent(root.transform,false);
            var recall = control.AddComponent<PalmRecoveryButton>(); recall.Build(0,room,world,root.transform,viewer.transform);
            recall.SetPalmPose(new Pose(new Vector3(0,1.3f,.4f),Quaternion.identity));
            Assert.That(recall.GetComponent<Collider>().enabled,Is.True);
            item.transform.position = Vector3.forward*5;
            recall.Activate(0); Assert.That(item.transform.position.z,Is.EqualTo(5),"The carrying hand must not activate its own button");
            recall.Activate(1); Assert.That(Vector3.Distance(item.transform.position,Vector3.forward),Is.LessThan(.001f));
            Assert.That(world.Running,Is.True,"Tool recovery must leave room physics running");
            recall.SetPalmPose(null); Assert.That(recall.GetComponent<Collider>().enabled,Is.False); Assert.That(recall.CanActivatePointer(1),Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScannedWallDoesNotBlockToolClicksOrGripRecovery()
        {
            RoomPhysicsLayers.Configure();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform,false);
            wall.transform.position = new Vector3(0,0,.5f); wall.transform.localScale = new Vector3(3,3,.05f); wall.layer = RoomPhysicsLayers.Scanned;
            int clicks = 0;
            var action = item.gameObject.AddComponent<RuleToolAction>(); action.Command = () => clicks++;
            var router = root.AddComponent<BookPointerRouter>();
            Physics.SyncTransforms();
            var pointer = new Ray(Vector3.zero,Vector3.forward);
            Assert.That(router.Begin(0,pointer),Is.True); router.End(0,pointer);
            Assert.That(clicks,Is.EqualTo(1),"A scanned surface swallowed the tool click");
            var hand = Hand(Vector3.zero); hand.raycastMask = RoomPhysicsLayers.InteractionMask;
            yield return null; yield return null;
            Assert.That(item.Grab.isSelected,Is.True,"XRI could not grab a tool behind a scanned surface");
            var room = root.AddComponent<RoomInteraction>();
            var viewer = new GameObject("Viewer"); viewer.transform.SetParent(root.transform,false); viewer.transform.position = new Vector3(0,1.55f,0);
            room.Viewer = viewer.transform; room.Register(item,true);
            hand.gameObject.SetActive(false); item.transform.position = Vector3.forward*5;
            room.RestoreInFrontOfViewer();
            Assert.That(item.Grab.isSelected,Is.False);
            Assert.That(Vector3.Distance(item.transform.position,Vector3.forward),Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator ObjectFollowsHandAndStaysWhenTrackingIsLost()
        {
            var hand = Hand(Vector3.zero);
            manager.SelectEnter((IXRSelectInteractor)hand, item.Grab);
            yield return new WaitForSeconds(.25f);
            var before = item.transform.position;
            hand.transform.position += Vector3.right * .3f;
            yield return null; yield return null;
            Assert.That(item.transform.position.x - before.x, Is.EqualTo(.3f).Within(.02f));
            hand.gameObject.SetActive(false);
            yield return null;
            Assert.That(item.Grab.isSelected, Is.False);
            var released = item.transform.position;
            yield return new WaitForSeconds(.1f);
            Assert.That(Vector3.Distance(item.transform.position, released), Is.LessThan(.001f));
            Assert.That(item.GetComponent<Rigidbody>().isKinematic, Is.True);
        }

        [UnityTest]
        public IEnumerator TwoHandsResizeWithinLimitsAndRoomRecoveryCancelsSelection()
        {
            var left = Hand(new Vector3(-.1f,0,0)); var right = Hand(new Vector3(.1f,0,0));
            manager.SelectEnter((IXRSelectInteractor)left, item.Grab);
            manager.SelectEnter((IXRSelectInteractor)right, item.Grab);
            yield return null; yield return null;
            float initial = item.transform.localScale.x;
            left.transform.position += Vector3.left * 2; right.transform.position += Vector3.right * 2;
            yield return null; yield return null;
            Assert.That(item.transform.localScale.x, Is.GreaterThan(initial));
            Assert.That(item.transform.localScale.x, Is.LessThanOrEqualTo(initial * 2 + .001f));
            item.RestoreHome();
            Assert.That(item.Grab.isSelected, Is.False);
            Assert.That(item.transform.localScale.x, Is.EqualTo(.15f).Within(.001f));
            Assert.That(Vector3.Distance(item.transform.localPosition, Vector3.forward), Is.LessThan(.001f));
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }
    }
}
