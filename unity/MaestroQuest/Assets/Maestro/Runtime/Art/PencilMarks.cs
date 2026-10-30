// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    /// <summary>One owned mesh for system artwork or admitted authored ink.</summary>
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class PencilMarks:MonoBehaviour,INativeResourceOwner
    {
        Mesh mesh;Material material;InkResources.Lease authored;
        internal bool GeometryReady=>mesh&&material;
        internal Material ReusablePigment=>authored!=null&&material?material:null;
        internal void TransferPigment(Material expected,InkResources.Lease destination){if(material!=expected||authored==null)throw new System.InvalidOperationException("Ink pigment owner changed");authored.TransferMaterial(expected,destination);}
        public void SetPaths(IReadOnlyList<Vector3[]> paths,float radius,uint seed=71){
            if(authored!=null){using var candidate=PencilGeometry.Prepare(paths,radius,authored.Owner,authored.Source,seed,this);SetPrepared(candidate);return;}
            var next=PencilStrokeMesh.Build(paths,radius,seed);Material created=null;
            try{
                if(!material){created=IllustratedMaterials.Create(IllustratedMaterials.Ink,.08f);created.SetFloat("_HasRestCoordinates",1);created.SetShaderPassEnabled(IllustratedSurface.PencilPass,false);}
                if(created){material=created;GetComponent<MeshRenderer>().sharedMaterial=material;}
                GetComponent<MeshFilter>().sharedMesh=next;ArtResources.Release(mesh);mesh=next;Creation.RoomAppearanceView.VisualsChanged(this);
            }catch{ArtResources.Release(next);if(created)ArtResources.Release(created);throw;}
        }
        internal void SetPrepared(PencilGeometry candidate){
            var lease=candidate.Take(this,out var next,out var pigment);ReleaseNativeResources();
            mesh=next;material=pigment;authored=lease;GetComponent<MeshFilter>().sharedMesh=mesh;GetComponent<MeshRenderer>().sharedMaterial=material;Creation.RoomAppearanceView.VisualsChanged(this);
        }
        public void SetColor(Color color){if(material){material.color=color;Creation.RoomAppearanceView.VisualsChanged(this);}}
        void OnDestroy()=>ReleaseNativeResources();
        void INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(authored!=null)authored.Dispose();else{ArtResources.Release(mesh);ArtResources.Release(material);}authored=null;mesh=null;material=null;}
    }
}
