// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Interaction
{
    public sealed class RoomInteraction : MonoBehaviour
    {
        public Transform Viewer;
        readonly List<RoomItem> items = new();
        readonly List<Material> materials = new();

        public void Register(RoomItem item) => items.Add(item);

        public void BuildStarterItems()
        {
            CreateItem("Drawing block", PrimitiveType.Cube, new Vector3(.52f, 1.02f, .58f), new Vector3(.13f,.13f,.13f), IllustratedMaterials.Cover);
            CreateItem("Drawing ball", PrimitiveType.Sphere, new Vector3(.72f, 1.02f, .72f), Vector3.one * .13f, IllustratedMaterials.Ribbon);
            CreateItem("Drawing cylinder", PrimitiveType.Cylinder, new Vector3(.60f, 1.02f, .91f), new Vector3(.10f,.10f,.10f), IllustratedMaterials.Hex("2B8D88"));
        }

        void CreateItem(string label, PrimitiveType type, Vector3 position, Vector3 size, Color color)
        {
            // The root has unit scale so two-handed resizing stays uniform.
            var root = new GameObject(label); root.transform.SetParent(transform, false); root.transform.localPosition = position;
            var shape = GameObject.CreatePrimitive(type); shape.transform.SetParent(root.transform, false); shape.transform.localScale = size;
            var material = IllustratedMaterials.Create(color); materials.Add(material);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            var item = root.AddComponent<RoomItem>(); item.Configure(new[] { shape.GetComponent<Collider>() }); Register(item);
        }

        public void RestoreInFrontOfViewer()
        {
            if (!Viewer) return;
            var forward = Vector3.ProjectOnPlane(Viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            // Content origin follows the user's current heading, without moving the XR camera origin.
            transform.SetPositionAndRotation(Viewer.position - Vector3.up * 1.55f, Quaternion.LookRotation(forward.normalized, Vector3.up));
            foreach (var item in items) if (item) item.RestoreHome();
        }

        void OnDestroy() { foreach (var material in materials) ArtResources.Release(material); }
    }
}
