// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

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
            originObject.transform.SetParent(transform, false);
            var offset = new GameObject("Camera height");
            offset.transform.SetParent(originObject.transform, false);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.92f,.89f,.83f,0);
            var origin = originObject.AddComponent<XROrigin>();
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 1.55f;
#if UNITY_ANDROID && !UNITY_EDITOR
            var pose = cameraObject.AddComponent<TrackedPoseDriver>();
            headPosition = new InputAction("Head position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            headRotation = new InputAction("Head rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            headTracking = new InputAction("Head tracking", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer");
            pose.positionInput = new InputActionProperty(headPosition);
            pose.rotationInput = new InputActionProperty(headRotation);
            pose.trackingStateInput = new InputActionProperty(headTracking);
            headPosition.Enable(); headRotation.Enable(); headTracking.Enable();
            new GameObject("Mixed reality session", typeof(ARSession));
            cameraObject.AddComponent<ARCameraManager>();
#endif
            var bookObject = new GameObject("Maestro book");
            bookObject.transform.SetParent(transform, false);
            bookObject.transform.localPosition = new Vector3(0, 1.16f, .65f);
            bookObject.transform.localRotation = Quaternion.Euler(24,0,0);
            book = bookObject.AddComponent<IllustratedBook>(); book.Build();
            browser = bookObject.AddComponent<NativeBookBrowser>();
            browser.SnapshotChanged += UpdateBook;
            bookObject.AddComponent<PhysicalBookControls>().Build(browser, book);
            var router = gameObject.AddComponent<BookPointerRouter>(); router.Browser = browser;
            var input = gameObject.AddComponent<BookControllerInput>();
            input.Router = router; input.TrackingSpace = offset.transform; input.DesktopCamera = camera;
            var avatar = new GameObject("Full body Maestro");
            avatar.transform.SetParent(transform, false);
            avatar.transform.localPosition = new Vector3(-.78f,0,1.4f);
            avatar.transform.localRotation = Quaternion.Euler(0,160,0);
            avatar.AddComponent<MaestroAvatar>().Browser = browser;
        }

        void Update()
        {
            if (browser.Surface == currentSurface) return;
            currentSurface = browser.Surface;
            if (currentSurface) book.SetSurface(currentSurface);
        }

        void UpdateBook(BookSnapshot state) => book.SetBookmark(!string.IsNullOrEmpty(state.bookmarkMessageId), PageSide.Left);

        void OnDestroy()
        {
            if (browser) browser.SnapshotChanged -= UpdateBook;
            headPosition?.Dispose(); headRotation?.Dispose(); headTracking?.Dispose();
        }
    }
}
