// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Rules;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace Maestro.Quest.Tests
{
    // Supplies actual subsystem joints; the app still reads MetaAimHand and routes its own input.
    public sealed class SculptInputHandProvider : XRHandSubsystemProvider
    {
        public static Vector3 Finger;
        public static bool Tracked;
        public override void Start() {}
        public override void Stop() {}
        public override void Destroy() {}
        public override void GetHandLayout(NativeArray<bool> joints) => joints[XRHandJointID.IndexTip.ToIndex()] = true;
        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType update,
            ref Pose leftRoot, NativeArray<XRHandJoint> left, ref Pose rightRoot, NativeArray<XRHandJoint> right)
        {
            rightRoot = new Pose(Finger, Quaternion.identity);
            right[XRHandJointID.IndexTip.ToIndex()] = XRHandProviderUtility.CreateJoint(Handedness.Right,
                Tracked ? XRHandJointTrackingState.Pose : XRHandJointTrackingState.None,
                XRHandJointID.IndexTip, rightRoot);
            return Tracked ? XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints : XRHandSubsystem.UpdateSuccessFlags.None;
        }
    }

    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator HandPackingSurvivesPageHoverButYieldsToPagePinchAndTrackingLoss() => HandSurfaceInput(true);
        [UnityTest] public IEnumerator HandSculptingSurvivesPageHoverButYieldsToPagePinchAndTrackingLoss() => HandSurfaceInput(false);

        IEnumerator HandSurfaceInput(bool packing)
        {
            var (id, field, capture) = packing ? PackingStage() : SculptStage();
            var contact = SculptContact(field);
            var before = editor.Read(id).heightFields[0].Copy();
            int originalCount = editor.Snapshot().objects.Length;
            const string descriptorId = "MaestroSculptInputHands", layout = "MaestroSculptInputAim";
            var descriptors = new List<XRHandSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            if (!descriptors.Any(d => d.id == descriptorId))
                XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo { id = descriptorId, providerType = typeof(SculptInputHandProvider) });
            descriptors.Clear(); SubsystemManager.GetSubsystemDescriptors(descriptors);
            var subsystem = descriptors.Single(d => d.id == descriptorId).Create();
            subsystem.Start();
            InputSystem.RegisterLayout<MetaAimHand>(layout);
            var prior = MetaAimHand.right;
            var device = (MetaAimHand)InputSystem.AddDevice(layout);
            MetaAimHand.right = device;
            var aim = contact + Vector3.up * .2f;
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Distant tray button"; button.transform.SetParent(root.transform, false);
            button.transform.position = aim + Vector3.forward * .6f; button.transform.localScale = Vector3.one * .1f;
            int clicks = 0; button.AddComponent<RuleToolAction>().Command = () => clicks++;
            var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor;
            var input = root.AddComponent<BookControllerInput>();
            input.Router = router; input.Editor = editor; input.TrackingSpace = root.transform;
            void Send(Vector3 fingertip, bool pinch = false, bool tracked = true)
            {
                SculptInputHandProvider.Finger = fingertip; SculptInputHandProvider.Tracked = tracked;
                subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                using (StateEvent.From(device, out var state))
                {
                    device.isTracked.WriteValueIntoEvent(tracked ? 1f : 0f, state);
                    device.aimFlags.WriteValueIntoEvent((int)MetaAimFlags.Valid, state);
                    device.devicePosition.WriteValueIntoEvent(aim, state);
                    device.deviceRotation.WriteValueIntoEvent(Quaternion.identity, state);
                    device.indexPressed.WriteValueIntoEvent(pinch ? 1f : 0f, state);
                    InputSystem.QueueEvent(state);
                }
            }
            try
            {
                Send(contact + Vector3.up * .2f); yield return null; yield return null;
                Assert.That((bool)input.ReadInputDiagnostics()["hands"][1]["usingHand"], Is.True);
                Assert.That((string)input.ReadInputDiagnostics()["hands"][1]["rayHit"], Does.EndWith("/Distant tray button"));
                Send(contact); yield return null; yield return null;
                Assert.That(capture.Active, Is.True, "Hovering a distant button must not cancel fingertip contact: " + editor.Status);
                string session = capture.SessionId;
                yield return null; yield return null;
                Assert.That(capture.SessionId, Is.EqualTo(session));
                Assert.That(editor.Read(id).heightFields[0].heights, Is.EqualTo(before.heights), "Contact is only a preview");
                Send(contact + Vector3.up * .2f); yield return null; yield return null;
                Assert.That(capture.Busy, Is.False, editor.Status);
                Assert.That(editor.Read(id).heightFields[0].VolumeLitres, Is.LessThan(before.VolumeLitres));
                Assert.That(editor.Snapshot().objects.Length, Is.EqualTo(originalCount + (packing ? 1 : 0)));
                if (packing) Assert.That(before.VolumeLitres - editor.Read(id).heightFields[0].VolumeLitres,
                    Is.EqualTo(editor.Read(PackedId()).materialStores[0].amountLitres).Within(.000001));
                var accepted = editor.Read(id).heightFields[0].Copy();
                // Use another clear footprint, outside the first packed ball.
                contact = SculptContact(field, .3f);
                Send(contact); yield return null; yield return null;
                Assert.That(capture.Active, Is.True, editor.Status);
                Send(contact, pinch:true); yield return null; yield return null;
                Assert.That(capture.Retained, Is.True, "An actual page pinch still interrupts the draft");
                Assert.That((bool)input.ReadInputDiagnostics()["hands"][1]["pageHeld"], Is.True);
                Send(contact); yield return null; yield return null;
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(editor.Read(id).heightFields[0].heights, Is.EqualTo(accepted.heights));
                capture.ResolveManual(true);
                yield return null; Assert.That(capture.Active, Is.False, "Must separate after page interaction");
                Send(contact + Vector3.up * .2f); yield return null; yield return null;
                Send(contact); yield return null; yield return null; Assert.That(capture.Active, Is.True, editor.Status);
                Send(contact, tracked:false); yield return null; yield return null;
                Assert.That(capture.Retained, Is.True, "Tracking loss cannot publish the pending edit");
                Assert.That(editor.Read(id).heightFields[0].heights, Is.EqualTo(accepted.heights));
                capture.ResolveManual(true);
            }
            finally
            {
                input.enabled = false;
                subsystem.Stop(); subsystem.Destroy();
                MetaAimHand.right = prior; InputSystem.RemoveDevice(device); InputSystem.RemoveLayout(layout);
                SculptInputHandProvider.Tracked = false;
            }
            yield return null;
        }
    }
}
