// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Art
{
    /// <summary>Shared pigment and graphite treatment for supplied and imported models.</summary>
    public sealed class PencilModelStyle : MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner, System.IDisposable
    {
        readonly List<Object> owned = new();
        readonly Dictionary<Material, Color> colors = new();
        readonly Dictionary<int,Material> importedMaterials=new();
        internal Material ImportedMaterial(int index)=>importedMaterials.TryGetValue(index,out var value)?value:null;
        internal Color? ImportedBaseColor(int index)=>importedMaterials.TryGetValue(index,out var material)&&colors.TryGetValue(material,out var color)?color:null;
        bool applied;

        public void Apply()
        {
            if (applied) return;
            applied = true;
            var meshes = new Dictionary<Mesh, Mesh>();
            var materials = new Dictionary<Material, Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<Book.BookPageTarget>() != null) continue;
                Mesh source = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!source || !source.isReadable) continue;
                if (!meshes.TryGetValue(source, out var mesh))
                {
                    mesh = Instantiate(source);
                    mesh.name = source.name + " pencil rest coordinates";
                    var rest = new List<Vector3>(mesh.vertices);
                    // FBX may express meshes in centimeters under a scaled parent.
                    // Keep pigment spacing in the model's original physical meters.
                    for (int point = 0; point < rest.Count; point++) rest[point] = Vector3.Scale(rest[point], renderer.transform.lossyScale);
                    mesh.SetUVs(2, rest);
                    if (mesh.normals.Length != mesh.vertexCount) mesh.RecalculateNormals();
                    mesh.SetUVs(3, new List<Vector3>(mesh.normals));
                    meshes.Add(source, mesh); owned.Add(mesh);
                }
                if (renderer is SkinnedMeshRenderer skinned) skinned.sharedMesh = mesh;
                else renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
                var replacements = renderer.sharedMaterials;
                for (int i = 0; i < replacements.Length; i++)
                {
                    var original = replacements[i];
                    if (!original) continue;
                    if (!materials.TryGetValue(original, out var pigment))
                    {
                        Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : IllustratedMaterials.Paper;
                        var surface=IllustratedSurface.Imported(original,color);
                        color.a = 1;
                        pigment = IllustratedMaterials.Create(color, .15f);
                        pigment.SetFloat("_HasRestCoordinates", 1);
                        // Authored eyes, freckles, and pencil paths already carry their own edges.
                        if (original.name.StartsWith("Detail ")) pigment.SetShaderPassEnabled("PENCIL", false);
                        if (original.HasProperty("_BaseMap")) pigment.mainTexture = original.GetTexture("_BaseMap");
                        else if (original.HasProperty("_MainTex")) pigment.mainTexture = original.GetTexture("_MainTex");
                        pigment.mainTextureScale = original.mainTextureScale; pigment.mainTextureOffset = original.mainTextureOffset;
                        surface.Apply(pigment,pigment.GetShaderPassEnabled("PENCIL"));
                        string slot=original.GetTag("MaestroMaterialIndex",false,"");
                        if(int.TryParse(slot,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int index)&&index>=0) {
                            pigment.SetOverrideTag("MaestroMaterialIndex",slot);
                            pigment.SetOverrideTag("MaestroDoubleSided",surface.DoubleSided?"true":"false");
                            importedMaterials.Add(index,pigment);
                        }
                        materials.Add(original, pigment); owned.Add(pigment);
                        colors.Add(pigment, color);
                    }
                    replacements[i] = pigment;
                }
                renderer.sharedMaterials = replacements;
            }
        }

        public void Tint(Color color) { foreach (var pair in colors) if (pair.Key) pair.Key.color = pair.Value * color; }

        public void Dispose() { foreach (var item in owned) ArtResources.Release(item);owned.Clear();colors.Clear();importedMaterials.Clear(); }
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>Dispose();
        void OnDestroy()=>Dispose();
    }
}
