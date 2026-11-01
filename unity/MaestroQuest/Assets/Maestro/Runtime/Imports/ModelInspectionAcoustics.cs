// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    internal sealed class ModelAcousticGeometry
    {
        internal readonly AcousticMeshInput[] Meshes;
        internal readonly ModelAssetBounds Resting;
        internal readonly string Issue;
        internal bool Known=>Issue==null;
        internal ModelAcousticGeometry(AcousticMeshInput[] meshes,ModelAssetBounds resting,string issue=null)
        {Meshes=meshes;Resting=resting;Issue=issue;}
    }
    public sealed partial class ModelInspection
    {
        internal ModelAcousticGeometry Acoustics {get;private set;}
        internal static ModelInspection InspectAcoustics(byte[] bytes)=>Inspect(bytes,false,true);
        sealed partial class Inspector
        {
            ModelAcousticGeometry InspectAcousticGeometry()
            {
                try {
                    RestRequire(!info.IsAvatar,"VRM constraints require native acoustic preparation.");
                    var result=new List<AcousticMeshInput>();var visited=new HashSet<int>();
                    int totalVertices=0,totalTriangles=0;
                    int IndexValue(JToken accessor,int i){
                        var view=views[(int)accessor["bufferView"]];int type=(int)accessor["componentType"],size=Size(type);
                        int at=binary+((int?)view["byteOffset"]??0)+((int?)accessor["byteOffset"]??0)+i*((int?)view["byteStride"]??size);
                        return type==5125?checked((int)BitConverter.ToUInt32(bytes,at)):type==5123?BitConverter.ToUInt16(bytes,at):bytes[at];
                    }
                    bool Cutout(JToken primitive){
                        if(primitive["material"]==null)return false;
                        var material=root["materials"][(int)primitive["material"]];
                        return (string)material["alphaMode"]=="MASK"&&((float?)material["alphaCutoff"]??.5f)>0;
                    }
                    void Visit(int n,Matrix4x4 parent){
                        if(!visited.Add(n))return;var node=nodes[n];
                        RestRequire(node["matrix"]==null&&node["extensions"]==null,"Matrix or extended nodes require native acoustic preparation.");
                        Vector3 Vector(JToken value,Vector3 fallback)=>value is JArray a?new((float)a[0],(float)a[1],(float)a[2]):fallback;
                        var position=Vector(node["translation"],Vector3.zero);position.z=-position.z;
                        var q=node["rotation"] as JArray;var rotation=q==null?Quaternion.identity:new Quaternion(-(float)q[0],-(float)q[1],(float)q[2],(float)q[3]);
                        RestRequire(float.IsFinite(Quaternion.Dot(rotation,rotation))&&Quaternion.Dot(rotation,rotation)>1e-12f,"Invalid acoustic node rotation.");
                        var mapping=parent*Matrix4x4.TRS(position,rotation.normalized,Vector(node["scale"],Vector3.one));
                        RestRequire(RoomAcoustics.ValidPose(mapping),"Unsupported acoustic node transform.");
                        if(node["mesh"]!=null&&node["skin"]==null){
                            var parts=(JArray)meshes[(int)node["mesh"]]["primitives"];
                            // Native import makes morphs into a SkinnedMeshRenderer.
                            // Its rigid acoustic selection also excludes an entire
                            // renderer if any material uses a nonzero alpha cutoff.
                            if(!parts.Any(p=>Array(p,"targets",128).Count>0||Cutout(p))){
                                var vertices=new List<float>();var indices=new List<int>();
                                foreach(var part in parts){
                                    var positions=accessors[(int)part["attributes"]["POSITION"]];int count=(int)positions["count"],offset=vertices.Count/3;
                                    RestRequire(offset+count<=RoomAcoustics.MaximumMeshVertices,"Imported acoustic mesh exceeds its vertex budget.");
                                    for(int i=0;i<count;i++){var point=RestVector(positions,i);vertices.Add(point.x);vertices.Add(point.y);vertices.Add(point.z);}
                                    var source=part["indices"]==null?null:accessors[(int)part["indices"]];int indexCount=source==null?count:(int)source["count"];
                                    RestRequire(indices.Count+indexCount<=RoomAcoustics.MaximumMeshTriangles*3,"Imported acoustic mesh exceeds its triangle budget.");
                                    for(int i=0;i<indexCount;i+=3){
                                        int At(int j)=>offset+(source==null?j:IndexValue(source,j));
                                        indices.Add(At(i));indices.Add(At(i+2));indices.Add(At(i+1));
                                    }
                                }
                                var v=vertices.ToArray();var t=indices.ToArray();
                                RestRequire(RoomAcoustics.ValidateMeshData(v,t,out var issue),issue);
                                totalVertices+=v.Length/3;totalTriangles+=t.Length/3;
                                RestRequire(result.Count<RoomAcoustics.MaximumVirtualSurfaces&&totalVertices<=98304&&totalTriangles<=49152,"Imported acoustic geometry exceeds its room budget.");
                                result.Add(new AcousticMeshInput(v,t,mapping));
                            }
                        }
                        foreach(var child in Array(node,"children",512))Visit((int)child,mapping);
                    }
                    var scene=root["scenes"][Integer(root,"scene",0,((JArray)root["scenes"]).Count-1,0)];
                    foreach(var node in Array(scene,"nodes",512))Visit((int)node,Matrix4x4.identity);
                    RestRequire(result.Count==0||info.RestBounds.Known&&info.RestBounds.HasBounds,"Imported model fitting requires a verified resting envelope.");
                    return new ModelAcousticGeometry(result.ToArray(),info.RestBounds);
                }catch(RestBoundsUnavailable error){return new ModelAcousticGeometry(System.Array.Empty<AcousticMeshInput>(),info.RestBounds,error.Message);}
            }
        }
    }
}
