// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Art
{
    /// <summary>One owned mesh for authored details or a user's spatial drawing.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PencilMarks : MonoBehaviour
    {
        Mesh mesh;
        Material material;

        public void SetPaths(IReadOnlyList<Vector3[]> paths, float radius, uint seed = 71)
        {
            // Validate and construct before replacing the visible drawing.
            var next = PencilStrokeMesh.Build(paths, radius, seed);
            if (!material)
            {
                material = IllustratedMaterials.Create(IllustratedMaterials.Ink, .08f);
                material.SetFloat("_HasRestCoordinates", 1);
                material.SetShaderPassEnabled("PENCIL", false);
                GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            GetComponent<MeshFilter>().sharedMesh = next;
            ArtResources.Release(mesh);
            mesh = next;Creation.RoomAppearanceView.VisualsChanged(this);
        }

        public void SetColor(Color color) { if (material){material.color = color;Creation.RoomAppearanceView.VisualsChanged(this);} }

        void OnDestroy() { ArtResources.Release(mesh); ArtResources.Release(material); }
    }
}
