// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Book
{
    // Owned value arrays only: no renderer, material, texture or imported instance.
    internal sealed class AcousticMeshInput
    {
        internal readonly float[] Vertices;
        internal readonly int[] Indices;
        internal readonly Matrix4x4 Local;
        internal AcousticMeshInput(float[] vertices,int[] indices,Matrix4x4 local)
        { Vertices=vertices;Indices=indices;Local=local; }
    }
}
