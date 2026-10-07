// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>An explicitly selected sound-obstructing mesh. Rendering and
    /// colliders do not control it; overlays and grab proxies are not geometry.</summary>
    public sealed class AcousticSurface : MonoBehaviour
    {
        internal Mesh Mesh { get; private set; }
        internal int Revision { get; private set; }
        internal RoomAcoustics Owner { get; private set; }
        internal bool Scanned { get; private set; }
        internal bool Available { get; private set; } = true;
        internal string Issue { get; set; }
        Creation.CreatedRoomObject creation;
        bool environment;
        internal bool ReflectionBoundary => Scanned || environment || creation && creation.AcousticBoundary;

        internal static AcousticSurface Attach(GameObject target, Mesh mesh, bool scanned = false, bool environment = false)
        {
            var surface = target.GetComponent<AcousticSurface>() ?? target.AddComponent<AcousticSurface>();
            surface.Bind(mesh, scanned, environment);
            return surface;
        }
        internal void Bind(Mesh mesh, bool scanned = false, bool environment = false)
        {
            Mesh = mesh; Scanned = scanned; this.environment = environment; Revision++;
            Connect(); Owner?.Invalidate(this);
        }
        internal void SetAvailable(bool value)
        {
            if (Available == value) return;
            Available = value; Revision++; Owner?.Invalidate(this);
        }
        void Connect()
        {
            creation = GetComponentInParent<Creation.CreatedRoomObject>();
            var next = GetComponentInParent<RoomAcoustics>(true);
            if (Owner == next) return;
            if (Owner) Owner.Unregister(this);
            Owner = next;
            if (Owner && isActiveAndEnabled) Owner.Register(this);
        }
        void OnEnable() { Connect(); if (Owner) Owner.Register(this); }
        void OnTransformParentChanged() => Connect();
        void OnDisable() { if (Owner) Owner.Unregister(this); Owner = null; }
        void OnDestroy() { if (Owner) Owner.Unregister(this); Owner = null; }
    }
}
