// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Maestro.Quest.Imports;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest
{
    /// <summary>One book/browser owner and one tutor avatar in the user's room.</summary>
    public sealed class MaestroRoom : MonoBehaviour
    {
        NativeBookBrowser browser;
        IllustratedBook book;
        Texture currentSurface;
        InputAction headPosition, headRotation, headTracking;

        void Awake()
        {
            var originObject = new GameObject("User origin");
            originObject.SetActive(false);
            originObject.transform.SetParent(transform, false);
            RoomPhysicsLayers.Configure();
            var offset = new GameObject("TrackingSpace");
            offset.transform.SetParent(originObject.transform, false);
            var cameraObject = new GameObject("CenterEyeAnchor", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            var origin = originObject.AddComponent<XROrigin>();
            origin.Origin = originObject;
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.55f;
#if UNITY_ANDROID && !UNITY_EDITOR
            var metaManager = originObject.AddComponent<OVRManager>();
            metaManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            originObject.AddComponent<MetaTrackingRig>();
            var pose = cameraObject.AddComponent<TrackedPoseDriver>();
            headPosition = new InputAction("Head position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            headRotation = new InputAction("Head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            headTracking = new InputAction("Head tracking", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer");
            pose.positionInput = new InputActionProperty(headPosition);
            pose.rotationInput = new InputActionProperty(headRotation);
            pose.trackingStateInput = new InputActionProperty(headTracking);
            headPosition.Enable(); headRotation.Enable(); headTracking.Enable();
            var session = new GameObject("Mixed reality session", typeof(ARSession));
            session.transform.SetParent(transform, false);
            cameraObject.AddComponent<ARCameraManager>();
#endif
            originObject.SetActive(true);
            var physics = gameObject.AddComponent<RoomPhysicsWorld>();
            var navigation = gameObject.AddComponent<RoomNavigation>(); navigation.Initialize(physics);
            var scan = gameObject.AddComponent<ScannedRoom>(); scan.Initialize(physics);
            gameObject.AddComponent<XRInteractionManager>();
            var content = new GameObject("Room content"); content.transform.SetParent(transform, false);
            var room = content.AddComponent<RoomInteraction>(); room.Viewer = camera.transform;
            for (int hand = 0; hand < 2; hand++)
            {
                var recall = new GameObject(hand == 0 ? "Left palm recovery" : "Right palm recovery"); recall.transform.SetParent(transform,false);
                recall.AddComponent<PalmRecoveryButton>().Build(hand,room,physics,offset.transform,camera.transform);
            }
            var bookObject = new GameObject("Maestro book");
            bookObject.transform.SetParent(content.transform, false);
            bookObject.transform.localPosition = new Vector3(0, 1.16f, .65f);
            bookObject.transform.localRotation = Quaternion.Euler(24,0,0);
            book = bookObject.AddComponent<IllustratedBook>(); book.Build();
            browser = bookObject.AddComponent<NativeBookBrowser>();
            browser.SnapshotChanged += UpdateBook;
            bookObject.AddComponent<PhysicalBookControls>().Build(browser, book);
            var coverHandle = bookObject.AddComponent<BoxCollider>();
            bookObject.layer = RoomPhysicsLayers.Environment;
            coverHandle.center = new Vector3(0, 0, .024f);
            coverHandle.size = new Vector3(.648f, .457f, .012f);
            var bookItem = bookObject.AddComponent<RoomItem>(); bookItem.Configure(new Collider[] { coverHandle }, .65f, 1.8f); room.Register(bookItem);
            var router = gameObject.AddComponent<BookPointerRouter>(); router.Browser = browser;
            var input = gameObject.AddComponent<BookControllerInput>();
            input.Router = router; input.TrackingSpace = offset.transform; input.DesktopCamera = camera; input.Room = room;
            input.PhysicsWorld = physics;
            var avatar = new GameObject("Full body Maestro");
            avatar.transform.SetParent(content.transform, false);
            avatar.transform.localPosition = new Vector3(-.78f,0,1.4f);
            avatar.transform.localRotation = Quaternion.Euler(0,160,0);
            avatar.AddComponent<MaestroAvatar>().Browser = browser;
            var avatarHandle = avatar.AddComponent<CapsuleCollider>(); avatarHandle.center = new Vector3(0,.85f,0); avatarHandle.height = 1.7f; avatarHandle.radius = .25f;
            avatar.layer = RoomPhysicsLayers.Environment;
            var avatarItem = avatar.AddComponent<RoomItem>(); avatarItem.Configure(new Collider[] { avatarHandle }, .3f, 1.5f); room.Register(avatarItem);
            var editor = content.AddComponent<RoomEditor>(); editor.Initialize(room,bookItem,avatarItem,physics:physics);
            router.Editor = editor; input.Editor = editor;
            content.AddComponent<RoomAgent>().Initialize(editor,browser);
            var drawing = gameObject.AddComponent<SpatialDrawing>(); drawing.Editor = editor; input.Drawing = drawing;
            var tray = new GameObject("Creation tools"); tray.transform.SetParent(content.transform,false);
            tray.transform.localPosition = new Vector3(.87f,.98f,.9f); tray.transform.localRotation = Quaternion.Euler(24,35,0);
            tray.AddComponent<RoomToolTray>().Build(editor,room);
            var workshop = content.AddComponent<AnimationWorkshop>(); workshop.Initialize(editor);
            var movement = avatar.AddComponent<AvatarSpatialMotion>(); movement.Initialize(editor,workshop,room,navigation,() => headTracking == null || (headTracking.ReadValue<int>() & 3) == 3);
            var animationTools = new GameObject("Animation tools"); animationTools.transform.SetParent(content.transform,false);
            animationTools.transform.localPosition = new Vector3(.87f,.55f,.9f); animationTools.transform.localRotation = Quaternion.Euler(40,35,0);
            animationTools.AddComponent<AnimationTools>().Build(workshop,room);
            var rules = content.AddComponent<RuleWorkshop>(); rules.Initialize(editor);
            content.AddComponent<RoomRules>().Initialize(rules,editor,workshop,browser,room,input);
            var ruleTools = new GameObject("Behaviour rules"); ruleTools.transform.SetParent(content.transform,false);
            ruleTools.transform.localPosition = new Vector3(-.95f,.68f,.75f); ruleTools.transform.localRotation = Quaternion.Euler(28,-35,0);
            ruleTools.AddComponent<RuleTools>().Build(rules,room);
            var imports = content.AddComponent<ImportWorkshop>(); imports.Initialize(editor, content.GetComponent<AnimationWorkshop>());
            content.AddComponent<LibraryBookController>().Initialize(editor,imports,rules,browser);
            var importTools = new GameObject("Model import tools"); importTools.transform.SetParent(content.transform, false);
            importTools.transform.localPosition = new Vector3(1.25f, .80f, 1.65f); importTools.transform.localRotation = Quaternion.Euler(20, 55, 0);
            importTools.AddComponent<ImportTools>().Build(imports, room);
            var physicsTools = new GameObject("Room physics tools"); physicsTools.transform.SetParent(content.transform,false);
            physicsTools.transform.localPosition = new Vector3(-1.2f,1.0f,1.55f); physicsTools.transform.localRotation = Quaternion.Euler(20,-45,0);
            router.Placement = physicsTools.AddComponent<PhysicsTools>(); router.Placement.Build(editor,physics,scan,room);
            var movementTools = new GameObject("Maestro movement tools"); movementTools.transform.SetParent(content.transform,false);
            movementTools.transform.localPosition = new Vector3(.85f,.38f,1.25f); movementTools.transform.localRotation = Quaternion.Euler(40,25,0);
            movementTools.AddComponent<AvatarSpatialTools>().Build(movement,editor,workshop,content.GetComponent<RoomRules>(),room);
            var virtualView=gameObject.AddComponent<VirtualRoomView>(); virtualView.Initialize(originObject.transform,camera,scan,physics);
            var movementControls=gameObject.AddComponent<MovementControls>();
            movementControls.Initialize(room,editor,workshop,movement,content.GetComponent<RoomRules>(),rules,input,virtualView,() => headTracking == null || (headTracking.ReadValue<int>() & 3) == 3);
            var controlTools=new GameObject("Movement and controller bindings"); controlTools.transform.SetParent(content.transform,false);
            controlTools.transform.localPosition=new Vector3(-1.15f,.4f,1.25f); controlTools.transform.localRotation=Quaternion.Euler(40,-30,0);
            controlTools.AddComponent<MovementTools>().Build(movementControls,room);
        }

        void Update()
        {
            if (browser.Surface == currentSurface) return;
            currentSurface = browser.Surface;
            if (currentSurface) book.SetSurface(currentSurface,true);
        }

        void UpdateBook(BookSnapshot state) => book.SetBookmark(!string.IsNullOrEmpty(state.bookmarkMessageId), PageSide.Left);

        void OnDestroy()
        {
            if (browser) browser.SnapshotChanged -= UpdateBook;
            headPosition?.Dispose(); headRotation?.Dispose(); headTracking?.Dispose();
        }
    }
}
