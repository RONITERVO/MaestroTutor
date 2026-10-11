// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;

namespace Maestro.Quest.Book
{
    public sealed partial class RoomAcoustics
    {
        // Separate bounded CPU ownership survives an audio-device reset, which
        // releases native handles. These are derived mesh inputs, not authored data.
        int retainedSurfaces, retainedVertices, retainedTriangles;
        internal int RetainedGeometryCount => retainedSurfaces;
        internal sealed class RetainedInput : IAcousticGeometry
        {
            internal Transform Frame;
            internal Matrix4x4 Placement, InitialPlacement, InitialFrame, InitialWorld, Local;
            internal float[] Vertices; internal int[] Indices;
            internal bool Current = true, Ended;
            public bool Active => !Ended && Frame && Frame.gameObject.activeInHierarchy;
            public bool HasGeometry => true;
            public bool Scanned => false;
            public bool ReflectionBoundary { get; internal set; }
            public int Revision { get; internal set; }
            public Matrix4x4 Pose => Placement.Equals(InitialPlacement) && Frame.localToWorldMatrix.Equals(InitialFrame)
                ? InitialWorld : Frame.localToWorldMatrix * Placement * Local;
            public string Issue { get; set; }
            public bool Read(out float[] vertices, out int[] indices, out string issue)
            {
                vertices = Vertices; indices = Indices;
                issue = Current ? null : "Acoustic definition changed; native geometry preparation is required";
                return Current && vertices != null && indices != null;
            }
        }
        internal sealed class AcousticResidency : IDisposable
        {
            internal RoomAcoustics Owner;
            internal AcousticSurface[] Surfaces;
            internal RetainedInput[] Inputs;
            internal Entry[] Entries;
            internal bool Committed, Ended;
            internal int Vertices, Triangles;
            internal bool Commit()
            {
                if (Ended || Committed || !Owner) return false;
                for (int i = 0; i < Surfaces.Length; i++)
                    if (!Surfaces[i] || !Owner.entries.TryGetValue(Surfaces[i], out var current)
                        || !ReferenceEquals(current, Entries[i]) || current.Retired
                        || current.Revision != Surfaces[i].Revision || !current.Pose.Equals(Surfaces[i].transform.localToWorldMatrix)) return false;
                // No SDK destroy/upload and no invalidation: the same native
                // input remains owned while its renderer is being destroyed.
                for (int i = 0; i < Surfaces.Length; i++)
                { Owner.entries.Remove(Surfaces[i]); Owner.entries.Add(Inputs[i], Entries[i]); }
                // Do not keep destroyed component wrappers (and their importer
                // graphs) alive through the cache. Rollback resolves the live root.
                Surfaces = null; Committed = true; return true;
            }
            internal void UpdateDefinition(bool current, Matrix4x4 placement)
            {
                if (Ended) return;
                foreach (var input in Inputs)
                {
                    input.Placement = placement;
                    if (input.Current == current) continue;
                    input.Current = current; input.Revision++;
                    if (Committed && Owner) Owner.Invalidate(input);
                }
            }
            internal void Restore(GameObject root)
            {
                if (Ended) return;
                var surfaces = root ? root.GetComponentsInChildren<AcousticSurface>(true)
                    .Where(s => s.Available && s.Mesh).ToArray() : Array.Empty<AcousticSurface>();
                bool same = Committed && Owner && surfaces.Length == Inputs.Length;
                for (int i = 0; same && i < Inputs.Length; i++)
                {
                    var input = Inputs[i]; var surface = surfaces[i];
                    same = input.Current && !Entries[i].Retired && !surface.Scanned
                        && surface.ReflectionBoundary == input.ReflectionBoundary
                        && ReadMesh(surface.Mesh, out var vertices, out var indices, out _)
                        && vertices.SequenceEqual(input.Vertices) && indices.SequenceEqual(input.Indices);
                }
                if (same)
                {
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        // A newly enabled view may already have an empty entry.
                        // Retire it before returning this exact geometry owner.
                        Owner.Unregister(surfaces[i]); Owner.entries.Remove(Inputs[i]);
                        var entry = Entries[i]; entry.Revision = surfaces[i].Revision;
                        if (entry.Handle == IntPtr.Zero) entry.AttemptEpoch = -1;
                        Owner.entries.Add(surfaces[i], entry);
                    }
                }
                Dispose();
            }
            public void Dispose()
            {
                if (Ended) return; Ended = true;
                if (Owner)
                {
                    if (Committed) foreach (var input in Inputs) Owner.Unregister(input);
                    Owner.retainedSurfaces -= Inputs.Length; Owner.retainedVertices -= Vertices; Owner.retainedTriangles -= Triangles;
                }
                foreach (var input in Inputs) { input.Ended = true; input.Vertices = null; input.Indices = null; input.Frame = null; }
                Owner = null; Surfaces = null; Inputs = null; Entries = null;
            }
        }
        internal bool TryPrepareResidency(GameObject root, Transform frame, out AcousticResidency residency, out string error)
        {
            residency = null; error = "The area's acoustic geometry is still preparing or unavailable";
            if (!root || !frame || !ValidPose(frame.localToWorldMatrix)) return false;
            var surfaces = root.GetComponentsInChildren<AcousticSurface>(true)
                .Where(s => s.isActiveAndEnabled && s.Available && s.Mesh).ToArray();
            if (surfaces.Length == 0) { error = null; return true; }
            Synchronize();
            if (!opened) return false;
            var inputs = new RetainedInput[surfaces.Length]; var owned = new Entry[surfaces.Length];
            int vertices = 0, triangles = 0;
            var placement = frame.worldToLocalMatrix * root.transform.localToWorldMatrix;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                // Physical scan input remains with its tracking owner. Only
                // already admitted virtual geometry can cross this boundary.
                if (surface.Scanned || !entries.TryGetValue(surface, out var entry) || entry.Retired
                    || entry.Handle == IntPtr.Zero || entry.VertexData == null || entry.IndexData == null
                    || entry.Revision != surface.Revision || entry.Boundary != surface.ReflectionBoundary
                    || !entry.Pose.Equals(surface.transform.localToWorldMatrix)) return false;
                owned[i] = entry; vertices += entry.Vertices; triangles += entry.Triangles;
                inputs[i] = new RetainedInput { Frame = frame, Placement = placement, InitialPlacement = placement,
                    InitialFrame = frame.localToWorldMatrix, InitialWorld = entry.Pose,
                    Local = root.transform.worldToLocalMatrix * entry.Pose, Vertices = entry.VertexData, Indices = entry.IndexData,
                    ReflectionBoundary = entry.Boundary, Revision = entry.Revision };
            }
            if (retainedSurfaces + inputs.Length > MaximumVirtualSurfaces || retainedVertices + vertices > 98304
                || retainedTriangles + triangles > MaximumVirtualTriangles)
            { error = "Retained acoustic geometry budget reached"; return false; }
            retainedSurfaces += inputs.Length; retainedVertices += vertices; retainedTriangles += triangles;
            residency = new AcousticResidency { Owner = this, Surfaces = surfaces, Inputs = inputs, Entries = owned, Vertices = vertices, Triangles = triangles };
            error = null; return true;
        }
    }
}
