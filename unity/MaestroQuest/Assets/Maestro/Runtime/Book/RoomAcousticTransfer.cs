// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Book
{
    public sealed partial class RoomAcoustics
    {
        // Preparation owns only bounded CPU arrays and references to still-live
        // views. It neither opens a renderer nor changes native worker metadata.
        internal async Task<AcousticResidency> PrepareResidencyAsync(GameObject root, Transform frame,
            RoomPreparationBudget budget, CancellationToken cancellation)
        {
            if (!root || !frame || budget == null || !ValidPose(frame.localToWorldMatrix))
                throw new InvalidOperationException("The area's acoustic frame is unavailable");
            var surfaces = root.GetComponentsInChildren<AcousticSurface>(true)
                .Where(s => s.isActiveAndEnabled && s.Available && s.Mesh).ToArray();
            if (surfaces.Length > MaximumVirtualSurfaces) throw new InvalidOperationException("Retained acoustic geometry budget reached");
            var framePose = frame.localToWorldMatrix; var rootPose = root.transform.localToWorldMatrix;
            var bindings = new AcousticGeometryBinding[surfaces.Length]; var owned = new Entry[surfaces.Length];
            var meshes = new Mesh[surfaces.Length]; var pointCounts = new int[surfaces.Length]; var faceCounts = new int[surfaces.Length];
            int vertices = 0, triangles = 0;
            var placement = frame.worldToLocalMatrix * rootPose;
            void CheckFrame()
            {
                cancellation.ThrowIfCancellationRequested();
                if (!this || !root || !frame || !root.activeInHierarchy || !framePose.Equals(frame.localToWorldMatrix)
                    || !rootPose.Equals(root.transform.localToWorldMatrix)) throw new OperationCanceledException("Acoustic placement changed during preparation");
            }
            bool Same(int i) => surfaces[i] && surfaces[i].Mesh == meshes[i]
                && entries.TryGetValue(surfaces[i], out var entry) && ReferenceEquals(entry, owned[i])
                && bindings[i].Matches(entry.GeometryKey, surfaces[i]);
            for (int i = 0; i < surfaces.Length; i++)
            {
                await budget.Step(); CheckFrame(); var surface = surfaces[i];
                if (!surface || surface.Scanned || !entries.TryGetValue(surface, out var entry))
                    throw new OperationCanceledException("The area's acoustic owner changed during preparation");
                var binding = new AcousticGeometryBinding(entry.GeometryKey, surface);
                if (!binding.Active || !binding.HasGeometry || !ValidPose(binding.Pose))
                    throw new InvalidOperationException("Invalid retained acoustic transform");
                if (!ReadMeshCost(surface.Mesh, out int points, out int faces, out var issue)) throw new InvalidOperationException(issue);
                bindings[i] = binding; owned[i] = entry; meshes[i] = surface.Mesh; pointCounts[i] = points; faceCounts[i] = faces;
                vertices += points; triangles += faces;
            }
            if (retainedSurfaces + surfaces.Length > MaximumVirtualSurfaces || retainedVertices + vertices > 98304
                || retainedTriangles + triangles > MaximumVirtualTriangles) throw new InvalidOperationException("Retained acoustic geometry budget reached");
            retainedSurfaces += surfaces.Length; retainedVertices += vertices; retainedTriangles += triangles; ResidencyEpoch++;
            var result = new AcousticResidency { Owner = this, Surfaces = surfaces, SourceBindings = bindings,
                SourceRoot = root, SourceFrame = frame, RootPose = rootPose, FramePose = framePose,
                Inputs = new RetainedInput[surfaces.Length], Entries = owned, Vertices = vertices, Triangles = triangles };
            try
            {
                for (int i = 0; i < surfaces.Length; i++)
                {
                    await budget.Step(); CheckFrame();
                    if (!Same(i)) throw new OperationCanceledException("Acoustic geometry changed during preparation");
                    var binding = bindings[i]; var entry = owned[i];
                    if (!ReadMeshCost(meshes[i], out int count, out int faces, out _) || count != pointCounts[i] || faces != faceCounts[i])
                        throw new OperationCanceledException("Acoustic geometry cost changed during preparation");
                    float[] points; int[] indices;
                    if (entry.Handle != IntPtr.Zero && !entry.Retired && entry.Revision == binding.Revision
                        && entry.Boundary == binding.Boundary && entry.Pose.Equals(binding.Pose)
                        && entry.VertexData?.Length == pointCounts[i] * 3 && entry.IndexData?.Length == faceCounts[i] * 3)
                    { points = entry.VertexData; indices = entry.IndexData; }
                    else if (!ReadMesh(meshes[i], out points, out indices, out var issue)) throw new InvalidOperationException(issue);
                    result.Inputs[i] = new RetainedInput { Frame = frame, Placement = placement, InitialPlacement = placement,
                        InitialFrame = framePose, InitialWorld = binding.Pose, Local = rootPose.inverse * binding.Pose,
                        Vertices = points, Indices = indices, ReflectionBoundary = binding.Boundary, Revision = binding.Revision };
                }
                CheckFrame();
                for (int i = 0; i < surfaces.Length; i++) if (!Same(i)) throw new OperationCanceledException("Acoustic geometry changed during preparation");
                var prepared = result; result = null; return prepared;
            }
            finally { result?.Dispose(); }
        }
    }
}
