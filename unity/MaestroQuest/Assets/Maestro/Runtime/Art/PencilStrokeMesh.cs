// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Art
{
    /// <summary>Pressure-varied graphite tubes in real space, stable in both XR eyes.</summary>
    public static class PencilStrokeMesh
    {
        public const int MaximumPoints = 10000;

        public static Mesh Build(IReadOnlyList<Vector3[]> paths, float radius, uint seed = 71)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (!float.IsFinite(radius) || radius <= 0 || radius > .1f) throw new ArgumentOutOfRangeException(nameof(radius));
            int total = 0;
            foreach (var path in paths)
            {
                if (path == null || path.Length > MaximumPoints - total) throw new ArgumentException("Drawing exceeds its point budget.", nameof(paths));
                total += path.Length;
                foreach (var point in path)
                    if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) throw new ArgumentException("Drawing coordinates must be finite.", nameof(paths));
            }
            var vertices = new List<Vector3>(total * 3);
            var colors = new List<Color>(total * 3);
            var indices = new List<int>(total * 18);
            foreach (var source in paths)
            {
                var points = new List<Vector3>(source.Length);
                foreach (var point in source)
                    if (points.Count == 0 || (point - points[points.Count - 1]).sqrMagnitude > 1e-12f) points.Add(point);
                if (points.Count < 2) continue;
                int start = vertices.Count;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < points.Count; i++)
                {
                    var tangent = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(i - 1, 0)];
                    if (tangent.sqrMagnitude < 1e-12f) tangent = points[i] - points[Math.Max(0, i - 1)];
                    tangent.Normalize();
                    // Transport the frame along the line, avoiding twists at the up axis.
                    normal = Vector3.ProjectOnPlane(normal, tangent);
                    if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.right);
                    normal.Normalize();
                    var side = Vector3.Cross(tangent, normal).normalized;
                    float pressure = .78f + .2f * Next(ref seed);
                    if (i == 0 || i == points.Count - 1) pressure *= .48f;
                    float value = .78f + .22f * Next(ref seed);
                    for (int ring = 0; ring < 3; ring++)
                    {
                        float angle = ring * Mathf.PI * 2 / 3;
                        vertices.Add(points[i] + (normal * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * (radius * pressure));
                        colors.Add(new Color(value, value, value, 1));
                    }
                    if (i == 0) continue;
                    for (int ring = 0; ring < 3; ring++)
                    {
                        int a = start + (i - 1) * 3 + ring, b = start + i * 3 + ring;
                        int c = start + i * 3 + (ring + 1) % 3, d = start + (i - 1) * 3 + (ring + 1) % 3;
                        indices.Add(a); indices.Add(c); indices.Add(b);
                        indices.Add(a); indices.Add(d); indices.Add(c);
                    }
                }
                indices.Add(start + 2); indices.Add(start + 1); indices.Add(start);
                int end = vertices.Count - 3;
                indices.Add(end); indices.Add(end + 1); indices.Add(end + 2);
            }
            var mesh = new Mesh { name = "Physical pencil strokes" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.SetColors(colors);
            mesh.SetUVs(2, vertices); // Rest-space pigment coordinates survive skeletal animation.
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh.SetUVs(3, new List<Vector3>(mesh.normals));
            return mesh;
        }

        static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) / 16777216f;
        }
    }
}
