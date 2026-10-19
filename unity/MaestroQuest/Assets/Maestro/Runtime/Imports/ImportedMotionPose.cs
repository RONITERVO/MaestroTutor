// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports {
    // One imported playback handoff snapshot. It preserves every source node
    // and blend shape, including joints outside the editable humanoid subset.
    internal sealed class ImportedMotionPose {
        readonly ImportedModel model;readonly Transform[] nodes;readonly Vector3[] positions,scales;readonly Quaternion[] rotations;
        readonly SkinnedMeshRenderer[] skins;readonly float[][] weights;
        readonly Vector3 position,scale;readonly Quaternion rotation;
        internal readonly Vector3 BasePosition;
        internal ImportedMotionPose(ImportedModel model,Vector3 basePosition){
            Maestro.Quest.Avatar.HumanoidFootPlacement.RestoreFor(model);
            this.model=model;BasePosition=basePosition;position=model.transform.localPosition;rotation=model.transform.localRotation;scale=model.transform.localScale;
            nodes=model.Instance.Nodes.ToArray();positions=nodes.Select(n=>n.localPosition).ToArray();rotations=nodes.Select(n=>n.localRotation).ToArray();scales=nodes.Select(n=>n.localScale).ToArray();
            skins=model.Instance.SkinnedMeshRenderers.ToArray();weights=skins.Select(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(s.GetBlendShapeWeight).ToArray()).ToArray();
        }
        internal void Restore(){
            Maestro.Quest.Avatar.HumanoidFootPlacement.RestoreFor(model);
            if(!model||!model.Ready||model.Instance.Nodes.Count!=nodes.Length||!model.Instance.Nodes.SequenceEqual(nodes))return;
            model.transform.SetLocalPositionAndRotation(position,rotation);model.transform.localScale=scale;
            for(int i=0;i<nodes.Length;i++)if(nodes[i]){nodes[i].localPosition=positions[i];nodes[i].localRotation=rotations[i];nodes[i].localScale=scales[i];}
            for(int i=0;i<skins.Length;i++)if(skins[i])for(int j=0;j<weights[i].Length;j++)skins[i].SetBlendShapeWeight(j,weights[i][j]);
        }
    }
}
