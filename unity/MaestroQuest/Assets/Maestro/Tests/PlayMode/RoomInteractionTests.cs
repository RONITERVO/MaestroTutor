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
