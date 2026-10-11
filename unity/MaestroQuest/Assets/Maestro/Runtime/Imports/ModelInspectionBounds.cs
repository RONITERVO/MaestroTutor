// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    public sealed partial class ModelInspection
    {
        sealed partial class Inspector
        {
            ModelAssetBounds InspectBounds()
            {
                // Rest pose is not a motion envelope. VRM constraints, skins and
                // blend shapes need their own verified deformation bounds.
                if(motionOnly||info.IsAvatar||info.Clips>0||info.MorphVertices>0||Array(root,"skins",16).Count>0)
                    return ModelAssetBounds.Unknown("Animated, skinned or deformable model bounds are not available yet.");
                var scene=root["scenes"][Integer(root,"scene",0,((JArray)root["scenes"]).Count-1,0)];
                var extent=RoomSpatialExtent.Empty;
                var meshBounds=new Dictionary<int,Bounds>();var visited=new HashSet<int>();
                string issue=null;
                Vector3 Vector(JToken value,Vector3 fallback)=>value is JArray a?new((float)a[0],(float)a[1],(float)a[2]):fallback;
                Bounds MeshBounds(int index)
                {
                    if(meshBounds.TryGetValue(index,out var cached))return cached;
                    bool first=true;Bounds bounds=default;
                    foreach(var primitive in (JArray)meshes[index]["primitives"]){
                        var accessor=accessors[(int)primitive["attributes"]["POSITION"]];
                        var view=views[(int)accessor["bufferView"]];
                        int start=binary+((int?)view["byteOffset"]??0)+((int?)accessor["byteOffset"]??0);
                        int stride=(int?)view["byteStride"]??12;
                        // Already validated dense float VEC3 storage. Do not trust
                        // exporter min/max; Unity recalculates from these vertices.
                        for(int i=0;i<(int)accessor["count"];i++){
                            int at=start+i*stride;
                            var point=new Vector3(BitConverter.ToSingle(bytes,at),BitConverter.ToSingle(bytes,at+4),-BitConverter.ToSingle(bytes,at+8));
                            if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                        }
                    }
                    meshBounds.Add(index,bounds);return bounds;
                }
                void Visit(int index,Matrix4x4 parent)
                {
                    if(issue!=null||!visited.Add(index))return;
                    var node=nodes[index];
                    // UniGLTF decomposes matrices into Transform TRS. Shear and
                    // reflection decomposition must be covered before claiming parity.
                    if(node["matrix"]!=null||node["extensions"]!=null){issue="Matrix or extended node transforms are not supported by the bounds inspector yet.";return;}
                    var position=Vector(node["translation"],Vector3.zero);position.z=-position.z;
                    var scale=Vector(node["scale"],Vector3.one);
                    var values=node["rotation"] as JArray;
                    var rotation=values==null?Quaternion.identity:new Quaternion(-(float)values[0],-(float)values[1],(float)values[2],(float)values[3]);
                    float length=Quaternion.Dot(rotation,rotation);
                    if(!float.IsFinite(length)||length<1e-12f){issue="The model has a degenerate rotation.";return;}
                    rotation=Quaternion.Normalize(rotation);
                    var mapping=parent*Matrix4x4.TRS(position,rotation,scale);
                    // Extreme source transforms can cancel to a small fitted model
                    // while losing useful float precision. Keep those candidates
                    // unknown instead of publishing a deceptively tight envelope.
                    for(int i=0;i<16;i++)if(!float.IsFinite(mapping[i])||Mathf.Abs(mapping[i])>10000){issue="The model transform exceeds the bounds inspector's precision range.";return;}
                    if(node["mesh"]!=null)extent.Include(MeshBounds((int)node["mesh"]),mapping);
                    foreach(var child in Array(node,"children",512))Visit((int)child,mapping);
                }
                foreach(var node in Array(scene,"nodes",512))Visit((int)node,Matrix4x4.identity);
                if(issue!=null)return ModelAssetBounds.Unknown(issue);
                if(!extent.Known)return ModelAssetBounds.Unknown("The model bounds overflow the supported coordinate range.");
                if(extent.HasBounds){
                    if(!ModelGeometryLayout.ValidSource(extent.Bounds))return ModelAssetBounds.Unknown("The model has invalid source dimensions.");
                    var centre=extent.Bounds.center;
                    if(Mathf.Max(Mathf.Abs(centre.x),Mathf.Abs(centre.y),Mathf.Abs(centre.z))>10000)return ModelAssetBounds.Unknown("The model origin exceeds the bounds inspector's precision range.");
                }
                return new ModelAssetBounds(true,extent.HasBounds,extent.Bounds,"");
            }
        }
    }
}
