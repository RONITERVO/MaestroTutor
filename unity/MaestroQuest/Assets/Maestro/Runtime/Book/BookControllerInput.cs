// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
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
    internal struct CosmosInputSnapshot
    {
        public bool LeftTracked, RightTracked;
        public bool LeftUsingHand, RightUsingHand;
        public Vector2 LeftStick, RightStick;
        public bool LeftStickClick, RightStickClick;
        public bool LeftPrimary, RightPrimary;
        public bool LeftSecondary, RightSecondary;
        public bool LeftGrip, RightGrip;
        public bool LeftTrigger, RightTrigger;
        public bool LeftTriggerJustPressed, RightTriggerJustPressed;
        public bool LeftPinching, RightPinching;
        public Vector3 LeftPinchPos, RightPinchPos;
        public bool HasLeftWrist, HasLeftPalm, HasRightFingerTip;
        public Vector3 LeftWristPos, LeftPalmPos, LeftPalmNormal, LeftForearmProximalDir;
        public Quaternion LeftWristRot;
        public Vector3 RightFingerTipPos;
        public bool HasPointerRay;
        public Vector3 PointerOrigin, PointerDir;
        public bool PointerSelectJustPressed;
        public Vector3 DesktopMove;
        public bool DesktopSprint;
        public Vector2 DesktopLookDelta;
        public float DesktopScrollY;
        public bool DesktopLookActive;
        public bool DesktopTabJustPressed;
        public bool DesktopRKeyHeld;
        public bool DesktopRightMouseHeld;
        public bool DesktopSpeedDownJustPressed;
        public bool DesktopSpeedUpJustPressed;
        public bool DesktopLocaleToggleJustPressed;
        public bool DesktopHomeJustPressed;
    }

    // Sample the tracked pose and select reader before XRI processes its frame.
    [DefaultExecutionOrder(-200)]
    public sealed partial class BookControllerInput : MonoBehaviour
    {
        public BookPointerRouter Router;
        public Transform TrackingSpace;
        public Camera DesktopCamera;
        public RoomInteraction Room;
        public RoomEditor Editor;
        public SpatialDrawing Drawing;
        public RoomPhysicsWorld PhysicsWorld;
        HandInput[] hands;
        readonly List<XRHandSubsystem> handSubsystems=new();XRHandSubsystem trackedHands;
        bool paused, focused = true;
        Material pointerMaterial;
        int cosmosSnapshotFrame = -1;
        CosmosInputSnapshot cosmosSnapshot;
        bool prevLeftTrig, prevRightTrig, prevLeftPinch, prevRightPinch;
        public Transform ControllerAnchor(int index) => hands != null && index >= 0 && index < hands.Length && hands[index].WasTracked && !hands[index].UsingHand && hands[index].Root.activeSelf ? hands[index].Root.transform : null;

        internal Newtonsoft.Json.Linq.JObject ObserveContact(int index)=>hands!=null&&index>=0&&index<hands.Length&&hands[index].Pusher
            ?hands[index].Pusher.GetComponent<TrackedPusher>().ObserveInputs():RoomContactInputs.State("inactive",false,"No tracked contact source");

        sealed class HandInput : IDisposable
        {
            public readonly InputAction Position, Rotation, Tracked, Press, Grip, Restore, Stick, Primary, StickClick;
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
                Stick = new InputAction(hand + " movement", InputActionType.Value, device + "primary2DAxis", expectedControlType: "Vector2");
                Primary = new InputAction(hand + " action", InputActionType.Button, device + "primaryButton");
                StickClick = new InputAction(hand + " stick action", InputActionType.Button, device + "{Primary2DAxisClick}");
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
                // RoomItem registers its page/button surfaces with their movable owner.
                Interactor.hitClosestOnly = false;
                Interactor.keepSelectedTargetValid = true;
                Interactor.useForceGrab = false;
                Interactor.manipulateAttachTransform = false;
                Pusher = new GameObject(hand + " physical contact"); Pusher.transform.SetParent(parent,false);
                var pusher = Pusher.AddComponent<TrackedPusher>(); pusher.Source = Root.transform; pusher.Interactor = Interactor; pusher.Input = owner;
                Beam = Visual(PrimitiveType.Cylinder, "Physical pointer", parent, material);
                Tip = Visual(PrimitiveType.Sphere, "Pointer tip", parent, material);
                Tip.transform.localScale = Vector3.one * .005f;
                Position.Enable(); Rotation.Enable(); Tracked.Enable(); Press.Enable(); Grip.Enable(); Restore.Enable(); Stick.Enable(); Primary.Enable(); StickClick.Enable();
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
                // Source swaps can deactivate/reactivate the aim in one frame.
                // Retire contact immediately rather than waiting for FixedUpdate.
                if(Pusher)Pusher.GetComponent<TrackedPusher>().CancelTrackedInput();
                SetSelect(false); Root.SetActive(false); Beam.SetActive(false); Tip.SetActive(false);
                Trigger.Cancel(); Squeeze.Cancel(); PageHeld = false; DrawingHeld = false; WasTracked = false;
            }

            public void Dispose()
            {
                Cancel(); Position.Dispose(); Rotation.Dispose(); Tracked.Dispose(); Press.Dispose(); Grip.Dispose(); Restore.Dispose(); Stick.Dispose(); Primary.Dispose(); StickClick.Dispose();
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
            if (!tracked) { Editor?.Liquids?.SetContactInput(index,null,usingHand); Router.Cancel(index); Drawing?.Cancel(index);Editor?.GetComponent<SpatialSculpting>()?.Cancel(index,input.Interactor); input.Cancel(); return; }
            if (!input.WasTracked || input.UsingHand != usingHand) { Router.Cancel(index); Drawing?.Cancel(index);Editor?.GetComponent<SpatialSculpting>()?.Cancel(index,input.Interactor); input.Cancel(); }
            input.WasTracked = true; input.UsingHand = usingHand;
            var position = usingHand ? hand.devicePosition.ReadValue() : input.Position.ReadValue<Vector3>();
            var rotation = usingHand ? hand.deviceRotation.ReadValue() : input.Rotation.ReadValue<Quaternion>();
            input.Root.transform.SetPositionAndRotation(TrackingSpace.TransformPoint(position), TrackingSpace.rotation * rotation);
            input.Root.SetActive(true);
            Vector3? waterContact=usingHand?(TryFingerPoint(index,out var tip)?tip:null):input.Root.transform.position;
            Editor?.Liquids?.SetContactInput(index,waterContact,usingHand);
            var ray = new Ray(input.Root.transform.position, input.Root.transform.forward);
            bool hitSomething = RoomPointerHit.Raycast(ray, out var hit, Router.MaximumDistance, Router.InteractionLayers, QueryTriggerInteraction.Ignore);
            bool page = hitSomething && (hit.collider.GetComponent<BookPageTarget>() || hit.collider.GetComponentInParent<PhysicalAction>());
            bool item = hitSomething && hit.collider.GetComponentInParent<RoomItem>();
            // Packing leaves ordinary props available for pinch-grab; only height surfaces own packing contact.
            bool packingGrip=usingHand&&item&&Editor&&Editor.GetComponent<SpatialSculpting>()?.CanGripWhilePacking(hit.collider.GetComponentInParent<RoomItem>())==true;
            var pointed = page ? GestureTarget.Page : packingGrip ? GestureTarget.Object : Editor && (Editor.DrawingMode||Editor.SculptMode) ? GestureTarget.Drawing : item ? (usingHand ? GestureTarget.Object : GestureTarget.Page) : GestureTarget.None;
            bool pressed = usingHand ? hand.indexPressed.isPressed : input.Press.IsPressed();
            var trigger = input.Trigger.Update(pressed, pointed);
            var squeeze = input.Squeeze.Update(!usingHand && input.Grip.IsPressed(), GestureTarget.Object);
            bool grabbing = squeeze == GestureTarget.Object || trigger == GestureTarget.Object;
            input.SetSelect(grabbing);
            if (grabbing && (trigger == GestureTarget.Page || trigger == GestureTarget.Drawing)) input.Trigger.Cancel();
            bool drawingHeld = trigger == GestureTarget.Drawing && !grabbing;
            var sculpt=Editor?Editor.GetComponent<SpatialSculpting>():null;
            Vector3? finger=null;
            if(sculpt&&sculpt.Enabled){
                if(usingHand){
                    if(!TryFingerPoint(index,out var fingertip))sculpt.Cancel(index,input.Interactor);
                    // A ray hovering a distant page does not own physical fingertip contact.
                    else if(trigger==GestureTarget.Page||grabbing)sculpt.Cancel(index);
                    else {finger=fingertip;sculpt.Finger(index,finger);}
                    drawingHeld=sculpt.Owns(index);
                }
                else if(drawingHeld&&!input.DrawingHeld)sculpt.Begin(index,ray);
                else if(!drawingHeld&&input.DrawingHeld)sculpt.End(index);
                else if(drawingHeld)sculpt.Move(index,ray);
            } else {
                if (drawingHeld && !input.DrawingHeld) Drawing?.Begin(index,ray);
                else if (!drawingHeld && input.DrawingHeld) Drawing?.End(index);
                else if (drawingHeld) Drawing?.Move(index,ray);
            }
            input.DrawingHeld = drawingHeld;
            bool pageHeld = trigger == GestureTarget.Page && !grabbing;
            if (grabbing) Router.Cancel(index);
            else if (pageHeld && !input.PageHeld) Router.Begin(index, ray);
            else if (!pageHeld && input.PageHeld) Router.End(index, ray);
            else if (pageHeld) Router.Move(index, ray);
            input.PageHeld = pageHeld;
            bool pencil = Editor && (Editor.DrawingMode||Editor.SculptMode) && !page;
            DrawPointer(input, ray, pencil || (hitSomething && (page || item)), pencil ? (sculpt&&sculpt.Enabled?(finger??sculpt.PointerPoint(ray)):(Drawing?Drawing.PointerPoint(ray):ray.GetPoint(.12f))) : hit.point);
            // B/Y recovers the room, including a book placed beyond reach.
            if (!usingHand && input.Restore.WasPressedThisFrame()) RestoreRoom();
        }

        bool TryFingerPoint(int index,out Vector3 point){
            point=default;if(!TrackingSpace)return false;
            if(trackedHands==null||!trackedHands.running){SubsystemManager.GetSubsystems(handSubsystems);trackedHands=null;foreach(var candidate in handSubsystems)if(candidate.running){trackedHands=candidate;break;}}
            if(trackedHands==null||!trackedHands.running)return false;
            var hand=index==0?trackedHands.leftHand:trackedHands.rightHand;
            if(!hand.isTracked||!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose))return false;
            point=TrackingSpace.TransformPoint(pose.position);return float.IsFinite(point.sqrMagnitude);
        }

        public ControllerFrame ReadMovement() => hands == null || paused || !focused ? default : new ControllerFrame {
            leftTracked=ControllerAnchor(0), rightTracked=ControllerAnchor(1),
            leftStick=hands[0].Stick.ReadValue<Vector2>(), rightStick=hands[1].Stick.ReadValue<Vector2>(),
            x=hands[0].Primary.IsPressed(), a=hands[1].Primary.IsPressed(), leftClick=hands[0].StickClick.IsPressed(), rightClick=hands[1].StickClick.IsPressed(),
            manipulating=hands[0].Select.manualPerformed || hands[1].Select.manualPerformed || hands[0].DrawingHeld || hands[1].DrawingHeld,
            busy=hands[0].Select.manualPerformed || hands[1].Select.manualPerformed || hands[0].PageHeld || hands[1].PageHeld || hands[0].DrawingHeld || hands[1].DrawingHeld
        };
        public void CancelAll() => CancelInputs();
        void RestoreRoom() => Room?.RestoreInFrontOfViewer();

        internal bool TryReadCosmosInput(out CosmosInputSnapshot snapshot)
        {
            if (hands == null || hands.Length < 2 || paused || !focused)
            {
                snapshot = default;
                return false;
            }
            if (cosmosSnapshotFrame == Time.frameCount)
            {
                snapshot = cosmosSnapshot;
                return true;
            }

            cosmosSnapshotFrame = Time.frameCount;
            var s = new CosmosInputSnapshot();
            var lh = hands[0];
            var rh = hands[1];

            s.LeftTracked = lh.WasTracked && lh.Root.activeSelf;
            s.RightTracked = rh.WasTracked && rh.Root.activeSelf;
            s.LeftUsingHand = lh.UsingHand;
            s.RightUsingHand = rh.UsingHand;

            s.LeftStick = lh.Stick.ReadValue<Vector2>();
            s.RightStick = rh.Stick.ReadValue<Vector2>();
            s.LeftStickClick = lh.StickClick.IsPressed();
            s.RightStickClick = rh.StickClick.IsPressed();
            s.LeftPrimary = lh.Primary.IsPressed();
            s.RightPrimary = rh.Primary.IsPressed();
            s.LeftSecondary = lh.Restore.IsPressed();
            s.RightSecondary = rh.Restore.IsPressed();
            s.LeftGrip = !lh.UsingHand && lh.Grip.IsPressed();
            s.RightGrip = !rh.UsingHand && rh.Grip.IsPressed();

            var metaL = MetaAimHand.left;
            var metaR = MetaAimHand.right;
            bool leftTrig = lh.UsingHand ? (metaL != null && metaL.indexPressed.isPressed) : lh.Press.IsPressed();
            bool rightTrig = rh.UsingHand ? (metaR != null && metaR.indexPressed.isPressed) : rh.Press.IsPressed();
            s.LeftTrigger = leftTrig;
            s.RightTrigger = rightTrig;
            s.LeftTriggerJustPressed = leftTrig && !prevLeftTrig;
            s.RightTriggerJustPressed = rightTrig && !prevRightTrig;
            prevLeftTrig = leftTrig;
            prevRightTrig = rightTrig;

            // Default pinch state from controllers (grip or trigger) and refine with XRHand joints when tracked.
            s.LeftPinching = s.LeftTracked && (s.LeftGrip || leftTrig);
            s.RightPinching = s.RightTracked && (s.RightGrip || rightTrig);
            if (s.LeftTracked) s.LeftPinchPos = lh.Root.transform.position;
            if (s.RightTracked) s.RightPinchPos = rh.Root.transform.position;

            if (TrackingSpace)
            {
                if (trackedHands == null || !trackedHands.running)
                {
                    SubsystemManager.GetSubsystems(handSubsystems);
                    trackedHands = null;
                    foreach (var candidate in handSubsystems)
                        if (candidate.running) { trackedHands = candidate; break; }
                }

                if (trackedHands != null && trackedHands.running)
                {
                    ReadHandJoints(trackedHands.leftHand, isLeft: true, ref s);
                    ReadHandJoints(trackedHands.rightHand, isLeft: false, ref s);
                }
            }

            // Fallback wrist/forearm proxy when left controller is tracked without articulated XRHandSubsystem joints.
            if (!s.HasLeftWrist && s.LeftTracked)
            {
                Transform lt = lh.Root.transform;
                s.HasLeftWrist = true;
                s.LeftWristPos = lt.position;
                s.LeftWristRot = lt.rotation;
                s.LeftForearmProximalDir = -lt.forward;
                s.HasLeftPalm = true;
                s.LeftPalmPos = lt.position;
                s.LeftPalmNormal = lt.up;
            }

            // Active XR pointer ray (prefer right hand, fallback to left hand).
            if (s.RightTracked)
            {
                s.HasPointerRay = true;
                s.PointerOrigin = rh.Root.transform.position;
                s.PointerDir = rh.Root.transform.forward;
                s.PointerSelectJustPressed = s.RightTriggerJustPressed || (s.RightPinching && !prevRightPinch);
            }
            else if (s.LeftTracked)
            {
                s.HasPointerRay = true;
                s.PointerOrigin = lh.Root.transform.position;
                s.PointerDir = lh.Root.transform.forward;
                s.PointerSelectJustPressed = s.LeftTriggerJustPressed || (s.LeftPinching && !prevLeftPinch);
            }
            prevLeftPinch = s.LeftPinching;
            prevRightPinch = s.RightPinching;

            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float y = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
                float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                s.DesktopMove = new Vector3(x, y, z);
                s.DesktopSprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                s.DesktopTabJustPressed = kb.tabKey.wasPressedThisFrame;
                s.DesktopRKeyHeld = kb.rKey.isPressed;
                s.DesktopSpeedDownJustPressed = kb.leftBracketKey.wasPressedThisFrame;
                s.DesktopSpeedUpJustPressed = kb.rightBracketKey.wasPressedThisFrame;
                s.DesktopLocaleToggleJustPressed = kb.lKey.wasPressedThisFrame;
                s.DesktopHomeJustPressed = kb.homeKey.wasPressedThisFrame;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool middleHeld = mouse.middleButton.isPressed;
                bool altLook = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed) && mouse.leftButton.isPressed;
                s.DesktopRightMouseHeld = mouse.rightButton.isPressed;
                s.DesktopLookActive = middleHeld || altLook || Cursor.lockState == CursorLockMode.Locked;
                if (s.DesktopLookActive) s.DesktopLookDelta = mouse.delta.ReadValue();
                s.DesktopScrollY = mouse.scroll.ReadValue().y;

                if (!s.HasPointerRay && DesktopCamera)
                {
                    var ray = DesktopCamera.ScreenPointToRay(mouse.position.ReadValue());
                    s.HasPointerRay = true;
                    s.PointerOrigin = ray.origin;
                    s.PointerDir = ray.direction;
                    s.PointerSelectJustPressed = mouse.leftButton.wasPressedThisFrame;
                }
            }

            cosmosSnapshot = s;
            snapshot = s;
            return true;
        }

        void ReadHandJoints(XRHand hand, bool isLeft, ref CosmosInputSnapshot s)
        {
            if (!hand.isTracked) return;
            bool hasThumb = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose);
            bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose);
            if (hasThumb && hasIndex)
            {
                Vector3 thumbW = TrackingSpace.TransformPoint(thumbPose.position);
                Vector3 indexW = TrackingSpace.TransformPoint(indexPose.position);
                float dist = Vector3.Distance(thumbW, indexW);
                bool pinching = dist < 0.032f && float.IsFinite(dist);
                Vector3 midW = (thumbW + indexW) * 0.5f;
                if (isLeft)
                {
                    s.LeftPinching = pinching || s.LeftPinching;
                    if (pinching) s.LeftPinchPos = midW;
                }
                else
                {
                    s.RightPinching = pinching || s.RightPinching;
                    if (pinching) s.RightPinchPos = midW;
                    s.HasRightFingerTip = true;
                    s.RightFingerTipPos = indexW;
                }
            }
            else if (!isLeft && hasIndex)
            {
                s.HasRightFingerTip = true;
                s.RightFingerTipPos = TrackingSpace.TransformPoint(indexPose.position);
            }

            if (isLeft)
            {
                bool hasWrist = hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose);
                bool hasPalm = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose);
                if (hasWrist)
                {
                    s.HasLeftWrist = true;
                    s.LeftWristPos = TrackingSpace.TransformPoint(wristPose.position);
                    s.LeftWristRot = TrackingSpace.rotation * wristPose.rotation;
                    if (hasPalm)
                    {
                        s.HasLeftPalm = true;
                        s.LeftPalmPos = TrackingSpace.TransformPoint(palmPose.position);
                        // In OpenXR XRHand left palm, palm rotation's -up points out of palm (+Y points back of hand).
                        Vector3 palmUp = TrackingSpace.rotation * (palmPose.rotation * Vector3.down);
                        s.LeftPalmNormal = palmUp.sqrMagnitude > 1e-6f ? palmUp.normalized : Vector3.up;
                        Vector3 prox = s.LeftWristPos - s.LeftPalmPos;
                        s.LeftForearmProximalDir = prox.sqrMagnitude > 1e-6f ? prox.normalized : -(s.LeftWristRot * Vector3.forward);
                    }
                    else
                    {
                        s.LeftForearmProximalDir = -(s.LeftWristRot * Vector3.forward);
                    }
                }
            }
        }

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
            for (int i = 0; i < hands.Length; i++) { Editor?.Liquids?.SetContactInput(i,null,false); Router?.Cancel(i); Drawing?.Cancel(i);Editor?.GetComponent<SpatialSculpting>()?.Cancel(i,hands[i].Interactor); hands[i].Cancel(); }
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
