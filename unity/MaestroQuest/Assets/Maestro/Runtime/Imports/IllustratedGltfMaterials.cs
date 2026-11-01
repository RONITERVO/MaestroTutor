// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Globalization;
using UniGLTF;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    /// <summary>Retain source slot identity and sidedness through the installed
    /// importer; Standard does not expose glTF doubleSided as a shader property.</summary>
    internal sealed class IllustratedGltfMaterials:IMaterialDescriptorGenerator
    {
        readonly BuiltInGltfMaterialDescriptorGenerator original=new();
        public MaterialDescriptor Get(GltfData data,int index) {
            var value=original.Get(data,index);
            if(index<0||index>=data.GLTF.materials.Count)return value;
            bool doubleSided=data.GLTF.materials[index].doubleSided;
            Action<Material> retain=material=>{
                material.SetOverrideTag("MaestroMaterialIndex",index.ToString(CultureInfo.InvariantCulture));
                material.SetOverrideTag("MaestroDoubleSided",doubleSided?"true":"false");
            };
            return new MaterialDescriptor(value.Name,value.Shader,value.RenderQueue,value.TextureSlots,value.FloatValues,value.Colors,value.Vectors,value.Actions.Append(retain).ToArray(),value.AsyncActions);
        }
        public MaterialDescriptor GetGltfDefault(string materialName=null)=>original.GetGltfDefault(materialName);
    }
}
