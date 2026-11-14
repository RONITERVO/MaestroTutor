// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace Maestro.Quest.Interaction {
    // Capture synchronously on Unity's main thread, then reserve before cloning.
    // Reads full vertex/index buffers, including unused attributes and padding.
    internal sealed class NavigationBuildInput {
        internal readonly NavMeshBuildSource[] Sources;
        internal readonly NavigationSourceCost Cost;
        internal readonly Bounds Bounds;
        internal readonly float Radius,Height,VoxelSize;
        NavigationBuildInput(NavMeshBuildSource[] sources,NavigationSourceCost cost,Bounds bounds,float radius,float height,float voxel){Sources=sources;Cost=cost;Bounds=bounds;Radius=radius;Height=height;VoxelSize=voxel;}
        internal static bool TryCapture(IReadOnlyList<NavMeshBuildSource> sources,Bounds bounds,float radius,float height,out NavigationBuildInput input,out string reason){
            input=null;reason="The walking sources have an invalid or unsupported layout";
            if(sources==null||sources.Count==0||!float.IsFinite(radius)||!float.IsFinite(height)||radius<=0||height<radius*2||!Finite(bounds.center)||!Finite(bounds.size)||bounds.size.x<0||bounds.size.y<0||bounds.size.z<0)return false;
            bounds.Expand(.2f);float voxel=Mathf.Clamp(radius/4,.025f,.08f);
            if(!TryCells(bounds.size,voxel,out var cells))return false;
            var copied=new NavMeshBuildSource[sources.Count];var layouts=new NavigationSourceLayout[sources.Count];var meshes=new Dictionary<Mesh,NavigationMeshLayout>();
            try{
                for(int i=0;i<sources.Count;i++){
                    var source=sources[i];if(!Finite(source.transform))return false;
                    source.component=null;copied[i]=source;
                    if(source.shape==NavMeshBuildSourceShape.Box){if(!Finite(source.size)||source.size.x<=0||source.size.y<=0||source.size.z<=0)return false;layouts[i]=NavigationSourceLayout.BoxSource;continue;}
                    if(source.shape!=NavMeshBuildSourceShape.Mesh||source.sourceObject is not Mesh mesh||!mesh||!mesh.isReadable||mesh.blendShapeCount!=0)return false;
                    if(!meshes.TryGetValue(mesh,out var layout)){
                        using var read=Mesh.AcquireReadOnlyMeshData(mesh);var data=read[0];long vertexBytes=0,triangles=0;
                        checked{
                            for(int stream=0;stream<data.vertexBufferCount;stream++)vertexBytes+=data.GetVertexData<byte>(stream).Length;
                            for(int sub=0;sub<data.subMeshCount;sub++){var part=data.GetSubMesh(sub);if(part.topology!=MeshTopology.Triangles||part.indexCount<0||part.indexCount%3!=0)return false;triangles+=part.indexCount/3;}
                        }
                        int width=data.indexFormat==IndexFormat.UInt16?2:4;
                        long indexBytes=data.GetIndexData<byte>().Length;
                        // Bind poses/weights can be present on an ordinary MeshRenderer.
                        // Include their backing payload conservatively, even if a
                        // vertex stream also contains a derived skinning attribute.
                        long weights=mesh.GetAllBoneWeights().Length;
                        long auxiliary=checked(64L*mesh.bindposeCount+8L*weights+(weights>0?mesh.GetBonesPerVertex().Length:0));
                        layout=new(mesh.GetInstanceID(),vertexBytes,indexBytes,width,triangles,auxiliary);meshes.Add(mesh,layout);
                    }
                    layouts[i]=new(layout);
                }
                if(!NavigationSourceCost.TryProject(layouts,cells,out var cost,out reason))return false;
                input=new(copied,cost,bounds,radius,height,voxel);reason=null;return true;
            }catch(OverflowException){reason="The walking source cost exceeds the supported numeric range";return false;}
            catch(InvalidOperationException){reason="The walking mesh data is unavailable for a bounded build";return false;}
        }
        internal static bool TryCells(Vector3 size,float voxel,out long cells){
            cells=0;if(!Finite(size)||size.x<=0||size.y<=0||size.z<=0||!float.IsFinite(voxel)||voxel<=0)return false;
            try{checked{
                long x=(long)Math.Ceiling((double)size.x/voxel),y=(long)Math.Ceiling((double)size.y/voxel),z=(long)Math.Ceiling((double)size.z/voxel);
                cells=x*y*z;return cells>0;
            }}catch(OverflowException){return false;}
        }
        internal NavMeshBuildSettings Settings(int agent){
            var settings=NavMesh.GetSettingsByID(agent);settings.agentRadius=Radius+.025f;settings.agentHeight=Height;
            settings.agentClimb=RoomGroundQuery.MaximumStep;settings.agentSlope=RoomGroundQuery.MaximumSlope;
            settings.overrideVoxelSize=true;settings.voxelSize=VoxelSize;settings.minRegionArea=.1f;return settings;
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        static bool Finite(Matrix4x4 m){for(int i=0;i<16;i++)if(!float.IsFinite(m[i]))return false;return Mathf.Abs(m.determinant)>1e-10f;}
    }
}
