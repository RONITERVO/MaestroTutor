// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    /// <summary>Explicit accepted collision geometry that navigation may consume.
    /// Visual previews never publish here. Other props do not become ground merely
    /// because they share a physics layer.</summary>
    public sealed class RoomWalkableSurface : MonoBehaviour
    {
        internal Collider Collision { get; private set; }
        internal uint Revision { get; private set; }
        internal void Publish(Collider collision) { Collision=collision; Revision++; }
        internal bool Available => isActiveAndEnabled && Collision && Collision.enabled &&
            !Collision.isTrigger && Collision.gameObject.activeInHierarchy &&
            (!Collision.attachedRigidbody || Collision.attachedRigidbody.isKinematic) &&
            GetComponentInParent<RoomItem>()?.Grab?.isSelected != true;
    }
}
