// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Maestro.Quest.Book
{
    // Sample the tracked pose and select reader before XRI processes its frame.
    [DefaultExecutionOrder(-200)]
    public sealed class BookControllerInput : MonoBehaviour
    {
        public BookPointerRouter Router;
        public Transform TrackingSpace;
        public Camera DesktopCamera;
        public RoomInteraction Room;
        public RoomEditor Editor;
        public SpatialDrawing Drawing;
        public RoomPhysicsWorld PhysicsWorld;
        HandInput[] hands;
        bool paused, focused = true;
        Material pointerMaterial;
        public Transform ControllerAnchor(int index) => hands != null && index >= 0 && index < hands.Length && hands[index].WasTracked && !hands[index].UsingHand && hands[index].Root.activeSelf ? hands[index].Root.transform : null;

        sealed class HandInput : IDisposable
        {
            public readonly InputAction Position, Rotation, Tracked, Press, Grip, Restore;
            public readonly GestureOwnership Trigger = new(), Squeeze = new();
            public readonly GameObject Root, Beam, Tip, Pusher;
            public readonly XRRayInteractor Interactor;
            public readonly XRInputButtonReader Select;
            public bool PageHeld, DrawingHeld, WasTracked, UsingHand;

            public HandInput(string hand, Transform parent, Material material, BookControllerInput owner)
            {
                string device = "<XRController>{" + hand + "}/";
                Position = new InputAction(hand + " aim position", InputActionType.Value, device + "pointerPosition", expectedControlType: "Vector3");
                Rotation = new InputAction(hand + " aim rotation", InputActionType.Value, device + "pointerRotation", expectedControlType: "Quaternion");
                Tracked = new InputAction(hand + " tracked", InputActionType.Value, device + "isTracked", expectedControlType: "Button");
                Press = new InputAction(hand + " page", InputActionType.Button, device + "triggerPressed");
                Grip = new InputAction(hand + " hold", InputActionType.Button, device + "gripPressed");
                Restore = new InputAction(hand + " restore room", InputActionType.Button, device + "secondaryButton");
                Root = new GameObject(hand + " interaction"); Root.SetActive(false); Root.transform.SetParent(parent, false);
                Root.AddComponent<ControllerIdentity>().PointerId = hand == "LeftHand" ? 0 : 1;
                Select = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue, manualFramePerformed = -1, manualFrameCompleted = -1 };
                Interactor = Root.AddComponent<XRRayInteractor>();
                Interactor.enableUIInteraction = false;
                Interactor.selectInput = Select;
                Interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
                Interactor.maxRaycastDistance = 2;
                Interactor.raycastMask = RoomPhysicsLayers.InteractionMask;
                Interactor.referenceFrame = parent;
                // A grip can reach the cover behind the browser's separate page mesh colliders.
                Interactor.hitClosestOnly = false;
                Interactor.keepSelectedTargetValid = true;
                Interactor.useForceGrab = false;
                Interactor.manipulateAttachTransform = false;
                Pusher = new GameObject(hand + " physical contact"); Pusher.transform.SetParent(parent,false);
                var pusher = Pusher.AddComponent<TrackedPusher>(); pusher.Source = Root.transform; pusher.Interactor = Interactor; pusher.Input = owner;
                Beam = Visual(PrimitiveType.Cylinder, "Physical pointer", parent, material);
                Tip = Visual(PrimitiveType.Sphere, "Pointer tip", parent, material);
                Tip.transform.localScale = Vector3.one * .005f;
                Position.Enable(); Rotation.Enable(); Tracked.Enable(); Press.Enable(); Grip.Enable(); Restore.Enable();
            }

            static GameObject Visual(PrimitiveType primitive, string label, Transform parent, Material material)
            {
                var item = GameObject.CreatePrimitive(primitive); item.name = label; item.transform.SetParent(parent, false);
                item.GetComponent<Collider>().enabled = false; ArtResources.Release(item.GetComponent<Collider>());
                item.GetComponent<Renderer>().sharedMaterial = material; item.SetActive(false); return item;
            }

            public void SetSelect(bool pressed)
            {
                if (pressed != Select.manualPerformed)
                {
                    if (pressed) Select.manualFramePerformed = Time.frameCount;
                    else Select.manualFrameCompleted = Time.frameCount;
                }
                Select.manualPerformed = pressed; Select.manualValue = pressed ? 1 : 0;
            }

            public void Cancel()
            {
                SetSelect(false); Root.SetActive(false); Beam.SetActive(false); Tip.SetActive(false);
                Trigger.Cancel(); Squeeze.Cancel(); PageHeld = false; DrawingHeld = false; WasTracked = false;
            }

            public void Dispose()
            {
                Cancel(); Position.Dispose(); Rotation.Dispose(); Tracked.Dispose(); Press.Dispose(); Grip.Dispose(); Restore.Dispose();
                ArtResources.Release(Root); ArtResources.Release(Beam); ArtResources.Release(Tip); ArtResources.Release(Pusher);
            }
        }

        void OnEnable()
        {
            pointerMaterial = IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88"), 0);
            hands = new[] { new HandInput("LeftHand", transform, pointerMaterial,this), new HandInput("RightHand", transform, pointerMaterial,this) };
        }

        void Update()
        {
            if (!Router || !TrackingSpace || paused || !focused) return;
            for (int i = 0; i < hands.Length; i++) UpdateHand(i);
#if UNITY_EDITOR
            if (!DesktopCamera || Mouse.current == null) return;
            var mouseRay = DesktopCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Mouse.current.leftButton.wasPressedThisFrame) Router.Begin(10, mouseRay);
            else if (Mouse.current.leftButton.wasReleasedThisFrame) Router.End(10, mouseRay);
            else if (Mouse.current.leftButton.isPressed) Router.Move(10, mouseRay);
            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame) RestoreRoom();
#endif
        }

        void UpdateHand(int index)
        {
            var input = hands[index];
            var hand = index == 0 ? MetaAimHand.left : MetaAimHand.right;
            bool controllerTracked = input.Tracked.ReadValue<float>() > .5f;
            bool usingHand = !controllerTracked && hand != null && hand.isTracked.isPressed;
            var flags = usingHand ? (MetaAimFlags)hand.aimFlags.ReadValue() : MetaAimFlags.None;
            bool tracked = controllerTracked || (usingHand && (flags & MetaAimFlags.Valid) != 0 && (flags & MetaAimFlags.SystemGesture) == 0);
            if (!tracked) { Router.Cancel(index); Drawing?.Cancel(index); input.Cancel(); return; }
            if (!input.WasTracked || input.UsingHand != usingHand) { Router.Cancel(index); Drawing?.Cancel(index); input.Cancel(); }
            input.WasTracked = true; input.UsingHand = usingHand;
            var position = usingHand ? hand.devicePosition.ReadValue() : input.Position.ReadValue<Vector3>();
            var rotation = usingHand ? hand.deviceRotation.ReadValue() : input.Rotation.ReadValue<Quaternion>();
            input.Root.transform.SetPositionAndRotation(TrackingSpace.TransformPoint(position), TrackingSpace.rotation * rotation);
            input.Root.SetActive(true);
            var ray = new Ray(input.Root.transform.position, input.Root.transform.forward);
            bool hitSomething = Physics.Raycast(ray, out var hit, Router.MaximumDistance, Router.InteractionLayers, QueryTriggerInteraction.Ignore);
            bool page = hitSomething && (hit.collider.GetComponent<BookPageTarget>() || hit.collider.GetComponentInParent<PhysicalAction>());
            bool item = hitSomething && hit.collider.GetComponentInParent<RoomItem>();
            var pointed = page ? GestureTarget.Page : Editor && Editor.DrawingMode ? GestureTarget.Drawing : item ? (usingHand ? GestureTarget.Object : GestureTarget.Page) : GestureTarget.None;
            bool pressed = usingHand ? hand.indexPressed.isPressed : input.Press.IsPressed();
            var trigger = input.Trigger.Update(pressed, pointed);
            var squeeze = input.Squeeze.Update(!usingHand && input.Grip.IsPressed(), GestureTarget.Object);
            bool grabbing = squeeze == GestureTarget.Object || trigger == GestureTarget.Object;
            input.SetSelect(grabbing);
            if (grabbing && (trigger == GestureTarget.Page || trigger == GestureTarget.Drawing)) input.Trigger.Cancel();
            bool drawingHeld = trigger == GestureTarget.Drawing && !grabbing;
            if (drawingHeld && !input.DrawingHeld) Drawing?.Begin(index,ray);
            else if (!drawingHeld && input.DrawingHeld) Drawing?.End(index);
            else if (drawingHeld) Drawing?.Move(index,ray);
            input.DrawingHeld = drawingHeld;
            bool pageHeld = trigger == GestureTarget.Page && !grabbing;
            if (grabbing) Router.Cancel(index);
            else if (pageHeld && !input.PageHeld) Router.Begin(index, ray);
            else if (!pageHeld && input.PageHeld) Router.End(index, ray);
            else if (pageHeld) Router.Move(index, ray);
            input.PageHeld = pageHeld;
            bool pencil = Editor && Editor.DrawingMode && !page;
            DrawPointer(input, ray, pencil || (hitSomething && (page || item)), pencil ? ray.GetPoint(.12f) : hit.point);
            // B/Y recovers the room, including a book placed beyond reach.
            if (!usingHand && input.Restore.WasPressedThisFrame()) RestoreRoom();
        }

        void RestoreRoom() { CancelInputs(); Room?.RestoreInFrontOfViewer(); }

        static void DrawPointer(HandInput input, Ray ray, bool visible, Vector3 point)
        {
            input.Beam.SetActive(visible); input.Tip.SetActive(visible);
            if (!visible) return;
            var delta = point - ray.origin;
            input.Beam.transform.SetPositionAndRotation(ray.origin + delta * .5f, Quaternion.FromToRotation(Vector3.up, delta));
            input.Beam.transform.localScale = new Vector3(.0012f, delta.magnitude * .5f, .0012f);
            input.Tip.transform.position = point - ray.direction * .003f;
        }

        void CancelInputs()
        {
            if (hands == null) return;
            for (int i = 0; i < hands.Length; i++) { Router?.Cancel(i); Drawing?.Cancel(i); hands[i].Cancel(); }
            Router?.Cancel(10);
        }

        void OnApplicationPause(bool value) { paused = value; if (paused) CancelInputs(); }
        void OnApplicationFocus(bool value) { focused = value; if (!focused) CancelInputs(); }
        void OnDisable()
        {
            CancelInputs();
            if (hands != null) foreach (var hand in hands) hand.Dispose();
            hands = null; ArtResources.Release(pointerMaterial);
        }
    }
}
