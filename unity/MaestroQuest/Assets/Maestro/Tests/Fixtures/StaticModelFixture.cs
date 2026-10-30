// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public static class StaticModelFixture
    {
        public static readonly Vector3[] Vertices={new(-2,-1,.5f),new(3,2,-1),new(1,.2f,4)};
        public static byte[] Create(Action<JObject> edit=null)
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write(0);writer.Write(0);
            foreach(var p in Vertices){writer.Write(99f);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);writer.Write(-99f);}
            var root=new JObject{
                ["asset"]=new JObject{["version"]="2.0",["copyright"]="Original Maestro spatial fixture; Apache-2.0"},
                ["scene"]=0,["scenes"]=new JArray(new JObject{["nodes"]=new JArray(0)}),["nodes"]=new JArray(new JObject{["mesh"]=0}),
                ["meshes"]=new JArray(new JObject{["primitives"]=new JArray(new JObject{["attributes"]=new JObject{["POSITION"]=0}})}),
                // Deliberately wrong exporter bounds and interleaved binary layout.
                ["accessors"]=new JArray(new JObject{["bufferView"]=0,["byteOffset"]=4,["componentType"]=5126,["count"]=3,["type"]="VEC3",["min"]=new JArray(-.1,-.1,-.1),["max"]=new JArray(.1,.1,.1)}),
                ["bufferViews"]=new JArray(new JObject{["buffer"]=0,["byteOffset"]=8,["byteLength"]=60,["byteStride"]=20}),
                ["buffers"]=new JArray(new JObject{["byteLength"]=stream.Length})};
            edit?.Invoke(root);return ModelFixture.Pack(root,stream.ToArray());
        }
        public static byte[] Hierarchy()=>Create(root=>{
            var q=Quaternion.Euler(17,38,-12);var child=Quaternion.Euler(11,-24,31);
            root["nodes"]=new JArray(
                new JObject{["translation"]=new JArray(2,1,-3),["rotation"]=new JArray(q.x,q.y,q.z,q.w),["scale"]=new JArray(1.2,.7,1.5),["children"]=new JArray(1,2)},
                new JObject{["mesh"]=0,["translation"]=new JArray(.7,-.1,1.1),["rotation"]=new JArray(child.x,child.y,child.z,child.w),["scale"]=new JArray(-1,1.3,.8)},
                new JObject{["mesh"]=0,["translation"]=new JArray(-1,2,.3),["scale"]=new JArray(.4,.5,.6)},
                new JObject{["mesh"]=0,["translation"]=new JArray(500,500,500)});
            root["scenes"]=new JArray(new JObject{["nodes"]=new JArray(0)},new JObject{["nodes"]=new JArray(3)});
        });
    }
}
