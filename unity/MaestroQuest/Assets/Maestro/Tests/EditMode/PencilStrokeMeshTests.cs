// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public class PencilStrokeMeshTests
    {
        [Test]
        public void TubeSidesFaceOutwardAndBothEndsAreClosed()
        {
            var mesh = PencilStrokeMesh.Build(new[] { new[] { Vector3.zero, Vector3.forward } }, .002f);
            try
            {
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = vertices[indices[i]];
                    var b = vertices[indices[i + 1]];
                    var c = vertices[indices[i + 2]];
                    var center = (a + b + c) / 3;
                    var outward = i < 18 ? new Vector3(center.x, center.y, 0) : i == 18 ? Vector3.back : Vector3.forward;
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), outward), 0);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DrawingsAreSolidAndDeterministicWithoutChangingUnityRandom()
        {
            var state = UnityEngine.Random.state;
            var path = new[] { new[] { Vector3.zero, Vector3.up * .2f, new Vector3(.1f,.4f,.2f) } };
            var first = PencilStrokeMesh.Build(path, .002f);
            var second = PencilStrokeMesh.Build(path, .002f);
            try
            {
                CollectionAssert.AreEqual(first.vertices, second.vertices);
                Assert.AreEqual(state, UnityEngine.Random.state);
                Assert.Greater(first.bounds.size.x, .1f);
                Assert.Greater(first.bounds.size.z, .2f);
                Assert.AreEqual(9, first.vertexCount);
                Assert.AreEqual(42, first.triangles.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
        }

        [Test]
        public void DegenerateAndUnboundedInputsCannotProduceInvalidGeometry()
        {
            var mesh = PencilStrokeMesh.Build(new[] { new[] { Vector3.zero, Vector3.zero, Vector3.up } }, .001f);
            try { Assert.AreEqual(6, mesh.vertexCount); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            Assert.Throws<ArgumentException>(() => PencilStrokeMesh.Build(new[] { new[] { Vector3.zero, new Vector3(float.NaN,0,0) } }, .001f));
            Assert.Throws<ArgumentException>(() => PencilStrokeMesh.Build(new[] { new Vector3[PencilStrokeMesh.MaximumPoints + 1] }, .001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PencilStrokeMesh.Build(Array.Empty<Vector3[]>(), 0));
        }
    }
}
