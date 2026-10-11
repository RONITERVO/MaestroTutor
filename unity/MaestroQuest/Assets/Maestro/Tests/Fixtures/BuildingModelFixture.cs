// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public static class BuildingModelFixture
    {
        public static byte[] Create()
        {
            var vertices=new List<Vector3>();
            void Box(Vector3 centre,Vector3 size) {
                var signs=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
                foreach(int i in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2})vertices.Add(centre+Vector3.Scale(signs[i],size*.5f));
            }
            Box(new Vector3(0,-.05f,0),new Vector3(6,.1f,6));Box(new Vector3(0,2.95f,0),new Vector3(6,.1f,6));
            foreach(float x in new[]{-1.8f,1.8f})Box(new Vector3(x,1.1f,0),new Vector3(2.4f,2.2f,.15f));
            Box(new Vector3(0,2.6f,0),new Vector3(6,.8f,.15f));
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            foreach(var v in vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
            var root=new JObject {
                ["asset"]=new JObject{["version"]="2.0",["copyright"]="Original Maestro building fixture; Apache-2.0"},
                ["scene"]=0,["scenes"]=new JArray(new JObject{["nodes"]=new JArray(0)}),["nodes"]=new JArray(new JObject{["name"]="Building",["mesh"]=0}),
                ["meshes"]=new JArray(new JObject{["primitives"]=new JArray(new JObject{["attributes"]=new JObject{["POSITION"]=0}})}),
                ["accessors"]=new JArray(new JObject{["bufferView"]=0,["componentType"]=5126,["count"]=vertices.Count,["type"]="VEC3",["min"]=new JArray(-3,-.1,-3),["max"]=new JArray(3,3,3)}),
                ["bufferViews"]=new JArray(new JObject{["buffer"]=0,["byteOffset"]=0,["byteLength"]=stream.Length}),["buffers"]=new JArray(new JObject{["byteLength"]=stream.Length})};
            return ModelFixture.Pack(root,stream.ToArray());
        }
    }
}
