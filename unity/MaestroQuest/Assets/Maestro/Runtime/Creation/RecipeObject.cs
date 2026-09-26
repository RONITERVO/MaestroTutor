// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    /// <summary>Native evaluation of saved recipes, shared by manual and agent edits.</summary>
    public sealed class RecipeObject : MonoBehaviour
    {
        readonly Dictionary<string,Transform> nodes = new();
        readonly Dictionary<string,Quaternion> rest = new();
        readonly List<Material> materials = new();
        readonly List<Color> colors = new();
        GameObject geometry;
        RoomRecipe recipe;
        string encoded;
        float time;
        bool interrupted;
        public Bounds LocalBounds { get; private set; }
        public Transform Part(string id) => nodes.TryGetValue(id,out var node) ? node : null;
        public bool Apply(RoomRecipe value)
        {
            if (value == null || !value.Validate(out _)) return false;
            string json = JsonUtility.ToJson(value); if (encoded == json) return false;
            encoded = json; recipe = value.Copy(); time = 0; interrupted = false;
            if (geometry) { geometry.SetActive(false); ArtResources.Release(geometry); }
            foreach (var material in materials) ArtResources.Release(material);
            nodes.Clear(); rest.Clear(); materials.Clear(); colors.Clear();
            geometry = new GameObject("Recipe geometry"); geometry.transform.SetParent(transform,false);
            Bounds bounds = default; bool first = true;
            foreach (var part in recipe.parts)
            {
                var node = new GameObject(part.id).transform;
                node.SetParent(string.IsNullOrEmpty(part.parent) ? geometry.transform : nodes[part.parent],false);
                node.SetLocalPositionAndRotation(part.position,part.rotation); nodes.Add(part.id,node); rest.Add(part.id,part.rotation);
                var shape = GameObject.CreatePrimitive(part.shape == "sphere" ? PrimitiveType.Sphere : part.shape == "cylinder" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                shape.transform.SetParent(node,false); shape.transform.localScale = Vector3.Scale(part.size,part.shape == "cylinder" ? new Vector3(1,.5f,1) : Vector3.one);
                // One stable proxy collider belongs to the complete grabbable assembly.
                var collider = shape.GetComponent<Collider>(); collider.enabled = false; ArtResources.Release(collider);
                var material = IllustratedMaterials.Create(part.color); materials.Add(material); colors.Add(part.color); shape.GetComponent<Renderer>().sharedMaterial = material;
                for (int i=0;i<8;i++)
                {
                    var corner = new Vector3((i&1)==0 ? -.5f : .5f,(i&2)==0 ? -.5f : .5f,(i&4)==0 ? -.5f : .5f);
                    var point = transform.InverseTransformPoint(node.TransformPoint(Vector3.Scale(part.size,corner)));
                    if (first) { bounds = new Bounds(point,Vector3.zero); first=false; } else bounds.Encapsulate(point);
                }
            }
            LocalBounds=bounds; return true;
        }
        public void Tint(Color tint) { for(int i=0;i<materials.Count;i++) materials[i].color=colors[i]*tint; }
        void Update()
        {
            if (recipe == null || !recipe.playing || interrupted) return;
            time += Mathf.Min(Time.deltaTime,.05f);
            foreach (var track in recipe.tracks) nodes[track.part].localRotation=rest[track.part]*recipe.Sample(track,time);
        }
        void OnApplicationPause(bool paused) { if (paused) interrupted=true; }
        void OnApplicationFocus(bool focused) { if (!focused) interrupted=true; }
        void OnDestroy() { foreach (var material in materials) ArtResources.Release(material); }
    }
}
