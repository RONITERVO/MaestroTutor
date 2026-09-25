// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Maestro.Quest.Interaction
{
    /// <summary>Movable room content stays where released; no gravity or throwing in passthrough.</summary>
    public sealed class RoomItem : MonoBehaviour
    {
        public XRGrabInteractable Grab { get; private set; }
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
            Grab.enabled = true;
        }

        public void RestoreHome()
        {
            // Disabling first lets XRI cancel all hands before changing the pose.
            if (Grab) Grab.enabled = false;
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            transform.localScale = homeScale;
            if (Grab) Grab.enabled = true;
        }
    }
}
