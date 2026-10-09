// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
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
#if UNITY_EDITOR
        // The batch-only editor probe supplies a fresh directory before Awake.
        // This seam and the file transport are absent from Android player builds.
        internal string ProbeWorkspaceDirectory;
#endif
        NativeBookBrowser browser;
        IllustratedBook book;
        Texture currentSurface;
        InputAction headPosition, headRotation, headTracking;

        void Awake()
        {
            gameObject.AddComponent<Diagnostics.RuntimeDiagnostics>();
            var acoustics = gameObject.AddComponent<RoomAcoustics>();
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
            gameObject.AddComponent<RoomAcousticMapScheduler>().Configure(acoustics, camera.transform,
                () => headTracking == null || (headTracking.ReadValue<int>() & 3) == 3);
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
            bookObject.transform.localPosition = WorkspaceDefaults.BookPosition;
            bookObject.transform.localRotation = WorkspaceDefaults.BookRotation;
            book = bookObject.AddComponent<IllustratedBook>(); book.Build();
            browser = bookObject.AddComponent<NativeBookBrowser>();
            browser.SnapshotChanged += UpdateBook;
            bookObject.AddComponent<PhysicalBookControls>().Build(browser, book);
            var coverHandle = bookObject.AddComponent<BoxCollider>();
            bookObject.layer = RoomPhysicsLayers.Environment;
            coverHandle.center = new Vector3(0, 0, .024f);
            coverHandle.size = new Vector3(.648f, .457f, .012f);
            var bookItem = bookObject.AddComponent<RoomItem>(); bookItem.Configure(new Collider[] { coverHandle }, .65f, 1.8f); room.Register(bookItem,true);
            var router = gameObject.AddComponent<BookPointerRouter>(); router.Browser = browser;
            var input = gameObject.AddComponent<BookControllerInput>();
            input.Router = router; input.TrackingSpace = offset.transform; input.DesktopCamera = camera; input.Room = room;
            input.PhysicsWorld = physics;
            var virtualView=gameObject.AddComponent<VirtualRoomView>(); virtualView.Initialize(content.transform,originObject.transform,camera,scan,physics);
            gameObject.AddComponent<RoomDepthOcclusion>().Initialize(offset.transform,camera.GetComponent<ARCameraManager>(),virtualView);
            var agent=gameObject.AddComponent<RoomAgent>();agent.Initialize(null,browser);
            var workspace=gameObject.AddComponent<Maestro.Quest.Persistence.WorkspaceHost>();
            browser.BindCameraSource(()=>workspace&&workspace.Current?workspace.Current.Editor:null);
            var includedAvatar=Maestro.Quest.Imports.BundledAvatar.FromApplication();
            var includedMotions=Maestro.Quest.Imports.BundledMotions.FromApplication();
            string workspaceDirectory=Application.persistentDataPath;
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(ProbeWorkspaceDirectory))workspaceDirectory=ProbeWorkspaceDirectory;
#endif
            workspace.Initialize(workspaceDirectory,room.transform,(session,directory,receipts,gate)=>session.Build(room,bookItem,browser,router,input,physics,navigation,scan,virtualView,
                () => headTracking == null || (headTracking.ReadValue<int>() & 3) == 3,workspaceDirectory,directory,receipts,gate,includedAvatar,includedMotions),agent,includedAvatar,includedMotions);

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
