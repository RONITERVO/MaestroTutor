// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    // Imported meshes are immutable after the pencil material/mesh conversion.
    // Build a small per-joint envelope once; observations read transforms and
    // weights only, never vertices, BakeMesh, physics or another authored world.
    [DefaultExecutionOrder(32000)]
    internal sealed class ImportedSkinBounds:MonoBehaviour
    {
        SkinnedMeshRenderer skin;Mesh mesh;Transform model;
        Transform[] bones;Matrix4x4[] bind;RoomSpatialExtent[] regions;
        Vector3[] morphReach;RoomSpatialExtent rest;
        bool prepared,overridden;
        internal void Initialize(SkinnedMeshRenderer renderer,Transform modelRoot)
        {
            skin=renderer;model=modelRoot;mesh=skin?skin.sharedMesh:null;
            prepared=Prepare();
            if(prepared)rest=Read(model.worldToLocalMatrix);
            RefreshRendererBounds();
        }
        static Vector3 Abs(Vector3 value)=>new(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));
        static bool Affine(Matrix4x4 value)
        {
            for(int i=0;i<16;i++)if(!float.IsFinite(value[i]))return false;
            return value.m30==0&&value.m31==0&&value.m32==0&&value.m33==1;
        }
        bool Prepare()
        {
            if(!skin||!model||!mesh||!mesh.isReadable||skin.quality!=SkinQuality.Bone4)return false;
            bones=skin.bones;bind=mesh.bindposes;
            if(bones.Length==0||bones.Length!=bind.Length)return false;
            for(int i=0;i<bones.Length;i++)if(!bones[i]||!Affine(bind[i]))return false;
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            if(vertices.Length==0||weights.Length!=vertices.Length)return false;
            using(var counts=mesh.GetBonesPerVertex()){
                if(counts.Length!=vertices.Length)return false;
                foreach(var count in counts)if(count==0||count>4)return false;
            }
            regions=new RoomSpatialExtent[bones.Length];for(int i=0;i<regions.Length;i++)regions[i]=RoomSpatialExtent.Empty;
            bool Include(int joint,float weight,Vector3 point)
            {
                if(!float.IsFinite(weight)||weight<0||weight>1.00001f)return false;
                if(weight==0)return true;
                if(joint<0||joint>=regions.Length)return false;
                ref var region=ref regions[joint];
                if(region.HasBounds)region.Bounds.Encapsulate(point);else region.Bounds=new Bounds(point,Vector3.zero);
                region.HasBounds=true;return true;
            }
            for(int i=0;i<vertices.Length;i++){
                var v=vertices[i];var w=weights[i];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                if(!RoomRecipe.Finite(v)||!float.IsFinite(sum)||Mathf.Abs(sum-1)>.00001f||
                   !Include(w.boneIndex0,w.weight0,v)||!Include(w.boneIndex1,w.weight1,v)||
                   !Include(w.boneIndex2,w.weight2,v)||!Include(w.boneIndex3,w.weight3,v))return false;
            }
            // UniGLTF imports each morph target as one delta frame at 100.
            // Arbitrary multi-frame Unity meshes need a separate interpolation
            // proof. A missing proof stays unknown, never a rest-pose guess.
            morphReach=new Vector3[mesh.blendShapeCount];var delta=new Vector3[vertices.Length];
            for(int shape=0;shape<morphReach.Length;shape++){
                if(mesh.GetBlendShapeFrameCount(shape)!=1||mesh.GetBlendShapeFrameWeight(shape,0)!=100)return false;
                mesh.GetBlendShapeFrameVertices(shape,0,delta,null,null);
                foreach(var value in delta){if(!RoomRecipe.Finite(value))return false;morphReach[shape]=Vector3.Max(morphReach[shape],Abs(value));}
            }
            return true;
        }
        internal RoomSpatialExtent Read(Matrix4x4 inverse,bool includeRest=false)
        {
            if(!prepared||!skin||!model||skin.sharedMesh!=mesh||skin.quality!=SkinQuality.Bone4||!Affine(inverse))return default;
            var reach=Vector3.zero;
            for(int shape=0;shape<morphReach.Length;shape++){
                float weight=skin.GetBlendShapeWeight(shape);if(!float.IsFinite(weight))return default;
                reach+=morphReach[shape]*(Mathf.Abs(weight)*.01f);
            }
            if(!RoomRecipe.Finite(reach))return default;
            var result=RoomSpatialExtent.Empty;
            for(int i=0;i<regions.Length;i++){
                if(!regions[i].HasBounds)continue;if(!bones[i])return default;
                var bounds=regions[i].Bounds;bounds.extents+=reach;
                var mapping=inverse*bones[i].localToWorldMatrix*bind[i];
                if(!Affine(mapping))return default;
                result.Include(bounds,mapping);
            }
            if(includeRest){
                var initial=rest.Transform(inverse*model.localToWorldMatrix);
                if(!initial.Known)return default;
                if(initial.HasBounds)result.Include(initial.Bounds,Matrix4x4.identity);
            }
            if(result.Known&&result.HasBounds){
                // Imported weights are normalized but float sums and matrix
                // products are not exact. Cover their small arithmetic drift.
                float padding=Mathf.Max(.00001f,(result.Bounds.center.magnitude+result.Bounds.extents.magnitude)*.0001f);
                var bounds=result.Bounds;bounds.Expand(padding*2);result.Include(bounds,Matrix4x4.identity);
            }
            return result;
        }
        internal void RefreshRendererBounds()
        {
            if(!skin)return;
            var value=Read(Matrix4x4.identity);
            if(value.Known&&value.HasBounds){skin.updateWhenOffscreen=false;skin.bounds=value.Bounds;overridden=true;}
            else {if(overridden){skin.ResetBounds();overridden=false;}skin.updateWhenOffscreen=true;}
        }
        void LateUpdate()=>RefreshRendererBounds();
        void OnDisable(){if(overridden&&skin){skin.ResetBounds();overridden=false;}}
    }
}
