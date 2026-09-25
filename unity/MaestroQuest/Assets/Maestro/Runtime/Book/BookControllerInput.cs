// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maestro.Quest.Book
{
    public sealed class BookControllerInput : MonoBehaviour
    {
        public BookPointerRouter Router;
        public Transform TrackingSpace;
        public Camera DesktopCamera;
        Controller[] controllers;

        sealed class Controller : IDisposable
        {
            public readonly InputAction Position, Rotation, Tracked, Press;
            public bool Held;
            public Controller(string hand)
            {
                string device = "<XRController>{" + hand + "}/";
                Position = new InputAction(hand + " aim position", InputActionType.Value, device + "pointerPosition", expectedControlType: "Vector3");
                Rotation = new InputAction(hand + " aim rotation", InputActionType.Value, device + "pointerRotation", expectedControlType: "Quaternion");
                Tracked = new InputAction(hand + " tracked", InputActionType.Value, device + "isTracked", expectedControlType: "Button");
                Press = new InputAction(hand + " press", InputActionType.Button, device + "triggerPressed");
                Position.Enable(); Rotation.Enable(); Tracked.Enable(); Press.Enable();
            }
            public void Dispose() { Position.Dispose(); Rotation.Dispose(); Tracked.Dispose(); Press.Dispose(); }
        }

        void OnEnable() => controllers = new[] { new Controller("LeftHand"), new Controller("RightHand") };

        void Update()
        {
            if (!Router || !TrackingSpace) return;
            for (int i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller.Tracked.ReadValue<float>() < .5f)
                {
                    Router.Cancel(i); controller.Held = false; continue;
                }
                var ray = new Ray(TrackingSpace.TransformPoint(controller.Position.ReadValue<Vector3>()), TrackingSpace.TransformDirection(controller.Rotation.ReadValue<Quaternion>() * Vector3.forward));
                bool held = controller.Press.IsPressed();
                if (held && !controller.Held) Router.Begin(i, ray);
                else if (!held && controller.Held) Router.End(i, ray);
                else if (held) Router.Move(i, ray);
                controller.Held = held;
            }
#if UNITY_EDITOR
            if (!DesktopCamera || Mouse.current == null) return;
            var mouseRay = DesktopCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Mouse.current.leftButton.wasPressedThisFrame) Router.Begin(10, mouseRay);
            else if (Mouse.current.leftButton.wasReleasedThisFrame) Router.End(10, mouseRay);
            else if (Mouse.current.leftButton.isPressed) Router.Move(10, mouseRay);
#endif
        }

        void OnDisable()
        {
            if (controllers == null) return;
            for (int i = 0; i < controllers.Length; i++) { Router?.Cancel(i); controllers[i].Dispose(); }
            Router?.Cancel(10); controllers = null;
        }
    }
}
