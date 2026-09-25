// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Owned geometry and paint for one user-created object.</summary>
    public sealed class CreatedRoomObject : MonoBehaviour
    {
        Material pigment;
        PencilMarks drawing;
        GameObject selection;
        Color tint;
        public ImportedModel Model { get; private set; }
        public string ModelStatus { get; private set; }
        public RoomItem Build(RoomObjectData data, ModelLibrary library = null)
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
            item.Configure(new[] { collider }, limits.minimum, limits.maximum);
            if (data.kind == RoomObjectKind.ImportedModel && library != null) LoadModel(data.modelHash, library, collider);
            return item;
        }

        async void LoadModel(string hash, ModelLibrary library, Collider collider)
        {
            ModelStatus = "Loading local model…";
            try
            {
                var asset = await library.ReadAsync(hash); if (!this) return;
                var root = new GameObject("Imported geometry"); root.transform.SetParent(transform, false);
                Model = root.AddComponent<ImportedModel>(); await Model.LoadAsync(asset); if (!this) return;
                var box = (BoxCollider)collider; box.transform.localScale = Vector3.one; box.center = Model.LocalBounds.center; box.size = Model.LocalBounds.size + Vector3.one * .02f;
                box.GetComponent<Renderer>().enabled = false;
                bool selected = selection && selection.activeSelf; if (selection) { selection.SetActive(false); Destroy(selection); }
                BuildSelection(Model.LocalBounds); SetSelected(selected); ApplyColor(tint);
                ModelStatus = asset.Inspection.IsAvatar ? "VRM imported as room object" : "Model ready";
            }
            catch (System.Exception error) { if (this) ModelStatus = error is ModelImportException ? error.Message : "This model could not be loaded. Import a compatible GLB or VRM again."; }
        }
        public void ApplyColor(Color color) { tint = color; if (pigment) pigment.color = color; if (drawing) drawing.SetColor(color); if (Model && Model.Ready) Model.Instance.GetComponent<PencilModelStyle>()?.Tint(color); }
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
