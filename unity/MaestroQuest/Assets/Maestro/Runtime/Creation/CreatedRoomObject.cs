// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Owned geometry and paint for one user-created object.</summary>
    public sealed class CreatedRoomObject : MonoBehaviour
    {
        Material pigment;
        PencilMarks drawing;
        GameObject selection;
        public RoomItem Build(RoomObjectData data)
        {
            Bounds bounds;
            Collider collider;
            if (data.kind == RoomObjectKind.Drawing)
            {
                drawing = gameObject.AddComponent<PencilMarks>(); drawing.SetPaths(new[] { data.points }, data.radius);
                bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
                var box = gameObject.AddComponent<BoxCollider>(); box.center = bounds.center;
                box.size = bounds.size + Vector3.one * .018f; collider = box;
            }
            else
            {
                var primitive = data.kind == RoomObjectKind.Ball ? PrimitiveType.Sphere : data.kind == RoomObjectKind.Cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                var shape = GameObject.CreatePrimitive(primitive); shape.name = data.kind.ToString(); shape.transform.SetParent(transform, false);
                shape.transform.localScale = data.kind == RoomObjectKind.Cylinder ? new Vector3(.13f,.065f,.13f) : Vector3.one * .13f;
                pigment = IllustratedMaterials.Create(data.color); shape.GetComponent<Renderer>().sharedMaterial = pigment;
                collider = shape.GetComponent<Collider>(); bounds = new Bounds(Vector3.zero, Vector3.one * .13f);
            }
            ApplyColor(data.color);
            BuildSelection(bounds);
            var item = gameObject.AddComponent<RoomItem>(); var limits = RoomDocument.ScaleLimits(data.kind);
            item.Configure(new[] { collider }, limits.minimum, limits.maximum); return item;
        }

        public void ApplyColor(Color color) { if (pigment) pigment.color = color; if (drawing) drawing.SetColor(color); }
        public void SetSelected(bool value) { if (selection) selection.SetActive(value); }

        void BuildSelection(Bounds bounds)
        {
            selection = new GameObject("Selected object outline", typeof(PencilMarks)); selection.transform.SetParent(transform, false);
            var low = bounds.min - Vector3.one * .012f; var high = bounds.max + Vector3.one * .012f;
            var paths = new List<Vector3[]>();
            foreach (float z in new[] { low.z, high.z }) paths.Add(new[] { new Vector3(low.x,low.y,z), new Vector3(high.x,low.y,z), new Vector3(high.x,high.y,z), new Vector3(low.x,high.y,z), new Vector3(low.x,low.y,z) });
            foreach (float x in new[] { low.x,high.x }) foreach (float y in new[] { low.y,high.y }) paths.Add(new[] { new Vector3(x,y,low.z), new Vector3(x,y,high.z) });
            var outline = selection.GetComponent<PencilMarks>(); outline.SetPaths(paths,.001f); outline.SetColor(IllustratedMaterials.Ribbon);
            selection.SetActive(false);
        }
        void OnDestroy() => ArtResources.Release(pigment);
    }
}
