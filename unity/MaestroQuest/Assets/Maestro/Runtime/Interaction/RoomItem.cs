// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using System;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Maestro.Quest.Persistence;
using Maestro.Quest.Book;

namespace Maestro.Quest.Interaction
{
    /// <summary>Movable room content; loose creations can opt into RigidRoomItem physics.</summary>
    public sealed class RoomItem : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        public XRGrabInteractable Grab { get; private set; }
        internal bool PoseLocked {get;set;}
        internal Creation.RoomWaterTraversal WaterTraversal {get;set;}=new(){mode="ignore"};
        WorkspaceWriteGate writes;
        public bool canProcess=>isActiveAndEnabled;
        internal bool PointerVisible=>!GetComponent<Creation.RoomAppearanceView>()||GetComponent<Creation.RoomAppearanceView>().PointerVisible;
        public bool Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)=>PointerVisible&&!PoseLocked&&writes?.Frozen!=true;
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor,IXRHoverInteractable interactable)=>PointerVisible;
        internal void ConfigureWrites(WorkspaceWriteGate gate){writes=gate;}
        internal void DetachWrites(WorkspaceWriteGate gate){if(ReferenceEquals(writes,gate))writes=null;}
        public event Action<RoomItem> GrabStarted, GrabFinished;
        Vector3 homePosition, homeScale;
        Quaternion homeRotation;

        public void Configure(Collider[] handles, float minimumScale = .5f, float maximumScale = 2f)
        {
            if (Grab) return;
            homePosition = transform.localPosition; homeRotation = transform.localRotation; homeScale = transform.localScale;
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.enabled = false;
            Grab.colliders.Clear(); Grab.colliders.AddRange(handles);
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            Grab.selectMode = InteractableSelectMode.Multiple;
            Grab.useDynamicAttach = true;
            Grab.throwOnDetach = false;
            Grab.addDefaultGrabTransformers = false;
            var transformer = gameObject.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowOneHandedScaling = false;
            transformer.allowTwoHandedScaling = true;
            transformer.clampScaling = true;
            transformer.minimumScaleRatio = minimumScale;
            transformer.maximumScaleRatio = maximumScale;
            transformer.allowTwoHandedRotation = XRGeneralGrabTransformer.TwoHandedRotationMode.TwoHandedAverage;
            Grab.AddSingleGrabTransformer(transformer);
            Grab.AddMultipleGrabTransformer(transformer);
            Grab.firstSelectEntered.AddListener(OnGrabStarted);
            Grab.lastSelectExited.AddListener(OnGrabFinished);
            Grab.selectFilters.Add(this);Grab.hoverFilters.Add(this);
            Grab.enabled = true;
        }

        void Start()
        {
            if (!Grab) return;
            // Book pages and tray buttons are built after Configure. XRI stops at an
            // unregistered collider, so their pointer surfaces must share the owner’s grab.
            // Nested movable items keep their own collider registration.
            bool changed = false;
            bool enabled = Grab.enabled;
            foreach (var surface in GetComponentsInChildren<Collider>(true))
            {
                if (surface.GetComponentInParent<RoomItem>() != this || Grab.colliders.Contains(surface)) continue;
                if (!surface.GetComponentInParent<BookPageTarget>() && !surface.GetComponentInParent<PhysicalAction>()) continue;
                if (!changed) Grab.enabled = false;
                Grab.colliders.Add(surface);
                changed = true;
            }
            if (changed) Grab.enabled = enabled;
        }

        void OnGrabStarted(SelectEnterEventArgs _) => GrabStarted?.Invoke(this);
        void OnGrabFinished(SelectExitEventArgs _) => GrabFinished?.Invoke(this);

        public void SetHome(Vector3 position, Quaternion rotation, Vector3 scale)
        { homePosition = position; homeRotation = rotation; homeScale = scale; }

        public void RestoreHome()
        {
            if(PoseLocked)return;
            // Disabling first lets XRI cancel all hands before changing the pose.
            if (Grab) Grab.enabled = false;
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            transform.localScale = homeScale;
            GetComponent<RigidRoomItem>()?.Teleported();
            if (Grab) Grab.enabled = true;
        }
    }
}
