// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Book;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    internal readonly struct ScannedAcousticMesh
    {
        internal readonly GameObject Object;
        internal readonly Mesh Mesh;
        internal ScannedAcousticMesh(GameObject value, Mesh mesh) { Object = value; Mesh = mesh; }
    }
    internal interface IScannedAcousticSource
    {
        bool Tracked { get; }
        bool TryRead(out ScannedAcousticMesh[] meshes);
    }
    public sealed partial class ScannedRoom
    {
        readonly List<AcousticSurface> acousticSurfaces = new();
        IScannedAcousticSource acousticSource;
        IScannedAcousticSource AcousticSource => acousticSource ??= new DeviceAcousticSource(this);
        internal void SetAcousticSourceForTests(IScannedAcousticSource value) { ClearAcousticScan(); acousticSource = value; }
        // Acoustic observation intentionally does not use ReadLayout: that
        // placement API requires enabled physics and mixed-reality mode.
        bool AcousticScanAvailable => SetupActive && geometryAccepted && !Busy && AcousticSource.Tracked;
        void RefreshAcousticAvailability()
        {
            bool available = AcousticScanAvailable;
            foreach (var surface in acousticSurfaces) if (surface) surface.SetAvailable(available);
        }
        void ClearAcousticScan()
        {
            foreach (var surface in acousticSurfaces) if (surface) surface.SetAvailable(false);
            acousticSurfaces.Clear();
        }
        internal void SynchronizeAcousticScan()
        {
            if (!AcousticScanAvailable) { RefreshAcousticAvailability(); return; }
            if (!AcousticSource.TryRead(out var meshes) || meshes == null || meshes.Length > RoomAcoustics.MaximumScannedSurfaces) { ClearAcousticScan(); return; }
            var seen = new HashSet<AcousticSurface>();
            foreach (var effect in meshes)
            {
                if (!effect.Object || !effect.Mesh) continue;
                var surface = effect.Object.GetComponent<AcousticSurface>();
                if (!surface) surface = AcousticSurface.Attach(effect.Object, effect.Mesh, true);
                else if (surface.Mesh != effect.Mesh || !acousticSurfaces.Contains(surface)) surface.Bind(effect.Mesh, true);
                surface.SetAvailable(true); seen.Add(surface);
            }
            foreach (var old in acousticSurfaces) if (old && !seen.Contains(old)) old.SetAvailable(false);
            acousticSurfaces.Clear(); acousticSurfaces.AddRange(seen);
        }
        sealed class DeviceAcousticSource : IScannedAcousticSource
        {
            readonly ScannedRoom owner;
            internal DeviceAcousticSource(ScannedRoom value) { owner = value; }
            public bool Tracked => owner.mruk && (owner.virtualView || owner.mruk.IsWorldLockActive) &&
                (owner.tracked == null || owner.tracked.IsPressed());
            public bool TryRead(out ScannedAcousticMesh[] meshes)
            {
                meshes = null;
                if (!owner.current || !owner.surfaces || owner.current.Anchors.Count > 256) return false;
                var result = new List<ScannedAcousticMesh>();
                foreach (var anchor in owner.current.Anchors)
                {
                    if (!anchor || !owner.surfaces.EffectMeshObjects.TryGetValue(anchor, out var effect) || !effect.effectMeshGO || !effect.mesh) continue;
                    if (result.Count == RoomAcoustics.MaximumScannedSurfaces) return false;
                    result.Add(new ScannedAcousticMesh(effect.effectMeshGO, effect.mesh));
                }
                meshes = result.ToArray(); return true;
            }
        }
    }
}
