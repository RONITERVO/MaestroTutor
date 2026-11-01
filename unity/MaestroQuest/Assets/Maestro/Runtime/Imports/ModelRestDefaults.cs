// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    // Detached import instructions indexed by source node, never names. Ordinary
    // UniGLTF does not apply glTF node/mesh default morph weights itself.
    internal sealed class ModelRestDefaults
    {
        readonly Dictionary<int,float[]> weights;
        internal ModelRestDefaults(Dictionary<int,float[]> values){weights=values;}
        internal void Apply(RuntimeGltfInstance instance)
        {
            foreach(var pair in weights){
                if(pair.Key>=instance.Nodes.Count||!instance.Nodes[pair.Key])throw new ModelImportException("The imported model lost a morph node.");
                var skin=instance.Nodes[pair.Key].GetComponent<SkinnedMeshRenderer>();
                if(!skin||!skin.sharedMesh||skin.sharedMesh.blendShapeCount!=pair.Value.Length)
                    throw new ModelImportException("The imported morph layout does not match its default weights.");
                for(int i=0;i<pair.Value.Length;i++)skin.SetBlendShapeWeight(i,pair.Value[i]*100);
            }
        }
    }
}
