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

namespace Maestro.Quest.Interaction
{
    /// <summary>Movable room content; loose creations can opt into RigidRoomItem physics.</summary>
    public sealed class RoomItem : MonoBehaviour, IXRSelectFilter
    {
        public XRGrabInteractable Grab { get; private set; }
        internal bool PoseLocked {get;set;}
        WorkspaceWriteGate writes;
        public bool canProcess=>isActiveAndEnabled;
        public bool Process(IXRSelectInteractor interactor,IXRSelectInteractable interactable)=>!PoseLocked&&writes?.Frozen!=true;
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
            Grab.selectFilters.Add(this);
            Grab.enabled = true;
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
