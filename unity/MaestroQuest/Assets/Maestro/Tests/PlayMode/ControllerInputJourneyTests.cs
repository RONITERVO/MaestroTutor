// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Maestro.Quest.Tests
{
    public sealed class ControllerInputJourneyTests
    {
        [UnityTest]
        public IEnumerator BoundControllerGripMovesReleasesAndTriggerClicksThroughRealRouter() => Journey(false);

        [UnityTest]
        public IEnumerator BookPageSurfaceDoesNotBlockItsOwnersGrip() => Journey(true);

        static IEnumerator Journey(bool page)
        {
            const string layout = "MaestroInputJourneyTouch";
            InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>(layout);
            var device = (OculusTouchControllerProfile.OculusTouchController)InputSystem.AddDevice(layout);
            InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.RightHand);
            var root = new GameObject("Input journey", typeof(XRInteractionManager));
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.SetParent(root.transform, false);
            target.transform.position = new Vector3(20, 1, 1);
            target.transform.localScale = Vector3.one * .2f;
            var item = target.AddComponent<RoomItem>();
            item.Configure(new[] { target.GetComponent<Collider>() });
            int clicks = 0;
            // The app builds page/tool colliders after configuring their movable owner.
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = page ? "Browser page" : "Tray button";
            surface.transform.SetParent(target.transform, false);
            surface.transform.localPosition = new Vector3(0, 0, -.6f);
            surface.transform.localScale = new Vector3(1, 1, .05f);
            if (page) surface.AddComponent<BookPageTarget>();
            else surface.AddComponent<RuleToolAction>().Command = () => clicks++;
            var router = root.AddComponent<BookPointerRouter>();
            var input = root.AddComponent<BookControllerInput>();
            input.Router = router; input.TrackingSpace = root.transform;
            void Send(float x, bool grip = false, bool trigger = false, bool tracked = true)
            {
                using (StateEvent.From(device, out var state))
                {
                    device.isTracked.WriteValueIntoEvent(tracked ? 1f : 0f, state);
                    device.GetChildControl<Vector3Control>("pointerPosition").WriteValueIntoEvent(new Vector3(x, 1, 0), state);
                    device.GetChildControl<QuaternionControl>("pointerRotation").WriteValueIntoEvent(Quaternion.identity, state);
                    device.grip.WriteValueIntoEvent(grip ? 1f : 0f, state);
                    device.gripPressed.WriteValueIntoEvent(grip ? 1f : 0f, state);
                    device.trigger.WriteValueIntoEvent(trigger ? 1f : 0f, state);
                    device.triggerPressed.WriteValueIntoEvent(trigger ? 1f : 0f, state);
                    InputSystem.QueueEvent(state);
                }
                InputSystem.Update();
            }
            try
            {
                Physics.SyncTransforms();
                Send(20); yield return null; yield return null;
                Send(20, grip:true); yield return null; yield return null; yield return null;
                var diagnostics = input.ReadInputDiagnostics();
                Assert.That((bool)diagnostics["hands"][1]["gripPressed"], Is.True);
                Assert.That(item.Grab.isSelected, Is.True, diagnostics.ToString());
                yield return new WaitForSeconds(.25f); // XRI finishes its default attach easing.
                Send(20.25f, grip:true); yield return null; yield return null;
                Assert.That(target.transform.position.x, Is.EqualTo(20.25f).Within(.02f));
                Send(20.25f); yield return null; yield return null;
                Assert.That(item.Grab.isSelected, Is.False);
                yield return new WaitForFixedUpdate(); // Released collider pose reaches the physics query world.
                yield return null;
                if (!page)
                {
                    Send(20.25f, trigger:true); yield return null; yield return null;
                    var pressedEvidence = input.ReadInputDiagnostics().ToString();
                    Send(20.25f); yield return null; yield return null;
                    Assert.That(clicks, Is.EqualTo(1), pressedEvidence + "\nReleased: " + input.ReadInputDiagnostics());
                }
                Send(20.25f, grip:true); yield return null; yield return null;
                Assert.That(item.Grab.isSelected, Is.True);
                Send(20.25f, grip:true, tracked:false); yield return null; yield return null;
                Assert.That(item.Grab.isSelected, Is.False);
                Assert.That((bool)input.ReadInputDiagnostics()["hands"][1]["active"], Is.False);
            }
            finally
            {
                input.enabled = false;
                Object.Destroy(root);
                InputSystem.RemoveDevice(device);
                InputSystem.RemoveLayout(layout);
            }
            yield return null;
        }
    }
}
