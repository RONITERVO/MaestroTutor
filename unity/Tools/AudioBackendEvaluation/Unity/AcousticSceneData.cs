// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Maestro.Quest.Book;
using SteamAudio;
using UMatrix = UnityEngine.Matrix4x4;
using UVector = UnityEngine.Vector3;

namespace Maestro.AudioEvaluation
{
    // Derived immutable renderer inputs, not a new saved world or a mesh loader.
    // Capture runs on the control thread through the app's geometry contract.
    // The simulation worker receives values only; it never reads Unity objects.
    internal sealed class AcousticMeshData
    {
        readonly SteamAudio.Vector3[] vertices;
        readonly int[] triangles;
        readonly float[] material;
        internal int VertexCount => vertices.Length;
        internal int TriangleCount => triangles.Length / 3;
        internal AcousticMeshData(float[] points, int[] indices)
        {
            if (points == null || points.Length < 9 || points.Length % 3 != 0 || points.Length > 16384 * 3 ||
                indices == null || indices.Length < 3 || indices.Length % 3 != 0 || indices.Length > 8192 * 3)
                throw new ArgumentException("Invalid or over-budget acoustic mesh");
            vertices = new SteamAudio.Vector3[points.Length / 3];
            for (int i = 0; i < points.Length; i++)
                if (!float.IsFinite(points[i]) || Math.Abs(points[i]) > 10000)
                    throw new ArgumentException("Invalid acoustic vertex");
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new SteamAudio.Vector3 { x = points[i * 3], y = points[i * 3 + 1], z = -points[i * 3 + 2] };
            triangles = (int[])indices.Clone();
            foreach (int index in triangles)
                if (index < 0 || index >= vertices.Length) throw new ArgumentException("Invalid acoustic index");
            UVector Point(int i) => new UVector(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]);
            for (int i = 0; i < triangles.Length; i += 3)
                if (UVector.Cross(Point(triangles[i + 1]) - Point(triangles[i]),
                                  Point(triangles[i + 2]) - Point(triangles[i])).sqrMagnitude < 1e-16f)
                    throw new ArgumentException("Degenerate acoustic triangle");
            // Existing app neutral hard-surface approximation. Visual colour or
            // transparency must not infer an acoustic material or delete a wall.
            material = new[] { .2f, .2f, .2f, .5f, 0f, 0f, 0f };
        }
        internal static AcousticMeshData Capture(IAcousticGeometry input)
        {
            if (input == null || !input.Active || !input.HasGeometry)
                throw new ArgumentException("Acoustic geometry is unavailable");
            if (!input.Read(out var points, out var indices, out var issue))
                throw new ArgumentException("Acoustic geometry could not be read: " + issue);
            return new AcousticMeshData(points, indices);
        }
        internal IntPtr Upload(IntPtr scene)
        {
            var materials = new int[TriangleCount];
            GCHandle a = default, b = default, c = default, d = default;
            try
            {
                a = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                b = GCHandle.Alloc(triangles, GCHandleType.Pinned);
                c = GCHandle.Alloc(material, GCHandleType.Pinned);
                d = GCHandle.Alloc(materials, GCHandleType.Pinned);
                var settings = new StaticMeshSettings { numVertices = VertexCount, numTriangles = TriangleCount,
                    numMaterials = 1, vertices = a.AddrOfPinnedObject(), triangles = b.AddrOfPinnedObject(),
                    materials = c.AddrOfPinnedObject(), materialIndices = d.AddrOfPinnedObject() };
                var error = API.iplStaticMeshCreate(scene, ref settings, out var mesh);
                if (error != Error.Success)
                {
                    if (mesh != IntPtr.Zero) API.iplStaticMeshRelease(ref mesh);
                    throw new InvalidOperationException("Native mesh creation: " + error);
                }
                return mesh;
            }
            finally
            {
                if (a.IsAllocated) a.Free(); if (b.IsAllocated) b.Free();
                if (c.IsAllocated) c.Free(); if (d.IsAllocated) d.Free();
            }
        }
    }
    internal sealed class AcousticMeshPose
    {
        internal readonly string Id;
        internal readonly AcousticMeshData Mesh;
        internal readonly SteamAudio.Matrix4x4 NativePose;
        internal readonly bool Scanned;
        internal static AcousticMeshPose Capture(string id, IAcousticGeometry input)
        {
            var data = AcousticMeshData.Capture(input);
            return new AcousticMeshPose(id, data, input.Pose, input.Scanned);
        }
        internal AcousticMeshPose(string id, AcousticMeshData mesh, UMatrix pose, bool scanned = false)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || mesh == null)
                throw new ArgumentException("Acoustic identity and mesh are required");
            for (int i = 0; i < 16; i++)
                if (!float.IsFinite(pose[i]) || Math.Abs(pose[i]) > 10000)
                    throw new ArgumentException("Invalid acoustic transform");
            if (pose.determinant <= 1e-10f || pose.m30 != 0 || pose.m31 != 0 || pose.m32 != 0 || pose.m33 != 1)
                throw new ArgumentException("Acoustic transform must be affine and orientation preserving");
            Id = id; Mesh = mesh; Scanned = scanned;
            // Same handedness change as the pinned vendor Unity integration.
            // Local vertices have already flipped Z. Placement is F * M * F.
            var flip = UMatrix.Scale(new UVector(1, 1, -1));
            var m = flip * pose * flip;
            NativePose = new SteamAudio.Matrix4x4 {
                m00=m.m00, m01=m.m01, m02=m.m02, m03=m.m03,
                m10=m.m10, m11=m.m11, m12=m.m12, m13=m.m13,
                m20=m.m20, m21=m.m21, m22=m.m22, m23=m.m23,
                m30=m.m30, m31=m.m31, m32=m.m32, m33=m.m33 };
        }
    }
    internal sealed class AcousticSceneData
    {
        readonly AcousticMeshPose[] meshes;
        internal int Count => meshes.Length;
        internal AcousticMeshPose At(int i) => meshes[i];
        internal AcousticSceneData(params AcousticMeshPose[] inputs)
        {
            if (inputs == null || inputs.Length > 512) throw new ArgumentException("Acoustic scene budget");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int scans=0, virtuals=0, scanVertices=0, virtualVertices=0, scanTriangles=0, virtualTriangles=0;
            foreach (var item in inputs)
            {
                if (item == null || !ids.Add(item.Id)) throw new ArgumentException("Duplicate or missing acoustic identity");
                if (item.Scanned) { scans++; scanVertices+=item.Mesh.VertexCount; scanTriangles+=item.Mesh.TriangleCount; }
                else { virtuals++; virtualVertices+=item.Mesh.VertexCount; virtualTriangles+=item.Mesh.TriangleCount; }
            }
            // Preserve the app's separate scan/virtual reservations. Reusing one
            // mesh does not bypass the limits on placed surface complexity.
            if (scans>128 || virtuals>384 || scanVertices>32768 || virtualVertices>98304 ||
                scanTriangles>16384 || virtualTriangles>49152) throw new ArgumentException("Acoustic scene budget");
            meshes = (AcousticMeshPose[])inputs.Clone();
        }
        internal static readonly AcousticSceneData Empty = new AcousticSceneData();
        internal static readonly AcousticSceneData ProbeRoom = new AcousticSceneData(new AcousticMeshPose(
            "probe-room", new AcousticMeshData(new float[] { -3,-3,3, 3,-3,3, 3,3,3, -3,3,3,
                -3,-3,-3, 3,-3,-3, 3,3,-3, -3,3,-3 }, new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4,
                3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5 }), UMatrix.identity));
    }
}
