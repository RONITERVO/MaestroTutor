// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Tests
{
    // Original synthetic geometry/animation, with no external asset licensing dependency.
    public static class ModelFixture
    {
        public static byte[] Create(Action<JObject> edit = null, bool avatar = false, bool skinAllBones = false)
        {
            var views = new JArray(); var accessors = new JArray(); using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
            int Floats(string type, int components, params float[] values)
            {
                int offset = (int)data.Length; foreach (float value in values) writer.Write(value);
                views.Add(new JObject { ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = values.Length * 4 });
                accessors.Add(new JObject { ["bufferView"] = views.Count - 1, ["componentType"] = 5126, ["count"] = values.Length / components, ["type"] = type }); return accessors.Count - 1;
            }
            int positions = Floats("VEC3", 3, -.5f,0,0, .5f,0,0, 0,1,0);
            accessors[positions]["min"] = new JArray(-.5, 0, 0); accessors[positions]["max"] = new JArray(.5, 1, 0);
            int normals = Floats("VEC3", 3, 0,0,1, 0,0,1, 0,0,1);
            int times = Floats("SCALAR", 1, 0, 1);
            int rotations = Floats("VEC4", 4, 0,0,0,1, 0,0,.70710678f,.70710678f);
            var nodes = new JArray(new JObject { ["name"] = "Animated triangle", ["mesh"] = 0 });
            var sceneNodes = new JArray(0);
            var root = new JObject {
                ["asset"] = new JObject { ["version"] = "2.0", ["copyright"] = "Original Maestro test fixture; Apache-2.0" },
                ["scene"] = 0, ["scenes"] = new JArray(new JObject { ["nodes"] = sceneNodes }), ["nodes"] = nodes,
                ["meshes"] = new JArray(new JObject { ["primitives"] = new JArray(new JObject { ["attributes"] = new JObject { ["POSITION"] = positions, ["NORMAL"] = normals }, ["material"] = 0 }) }),
                ["materials"] = new JArray(new JObject { ["pbrMetallicRoughness"] = new JObject { ["baseColorFactor"] = new JArray(.25,.5,.75,1), ["metallicFactor"] = 0 } }),
                ["animations"] = new JArray(new JObject { ["name"] = "Wave triangle", ["samplers"] = new JArray(new JObject { ["input"] = times, ["output"] = rotations }), ["channels"] = new JArray(new JObject { ["sampler"] = 0, ["target"] = new JObject { ["node"] = 0, ["path"] = "rotation" } }) }),
                ["bufferViews"] = views, ["accessors"] = accessors
            };
            if (avatar)
            {
                string[] names = { "hips", "spine", "chest", "neck", "head", "leftUpperArm", "leftLowerArm", "leftHand", "rightUpperArm", "rightLowerArm", "rightHand", "leftUpperLeg", "leftLowerLeg", "leftFoot", "rightUpperLeg", "rightLowerLeg", "rightFoot" };
                int[] parents = { -1,0,1,2,3,2,5,6,2,8,9,0,11,12,0,14,15 };
                float[][] translations = { new[] {0f,1f,0f},new[] {0f,.15f,0f},new[] {0f,.15f,0f},new[] {0f,.2f,0f},new[] {0f,.1f,0f},new[] {.15f,.15f,0f},new[] {.25f,0f,0f},new[] {.25f,0f,0f},new[] {-.15f,.15f,0f},new[] {-.25f,0f,0f},new[] {-.25f,0f,0f},new[] {.1f,-.1f,0f},new[] {0f,-.4f,0f},new[] {0f,-.4f,.05f},new[] {-.1f,-.1f,0f},new[] {0f,-.4f,0f},new[] {0f,-.4f,.05f} };
                var human = new JObject();
                for (int i = 0; i < names.Length; i++) { nodes.Add(new JObject { ["name"] = names[i], ["translation"] = new JArray(translations[i]) }); human[names[i]] = new JObject { ["node"] = i+1 }; }
                for (int i = 0; i < names.Length; i++) if (parents[i] >= 0) { var parent = nodes[parents[i]+1]; if (parent["children"] == null) parent["children"] = new JArray(); ((JArray)parent["children"]).Add(i+1); }
                sceneNodes.Add(1);
                // Bind the actual triangle to hips/head so avatar tests verify
                // deformed geometry as well as Transform-only skeleton motion.
                int jointOffset = (int)data.Length;
                foreach (ushort joint in new ushort[] { 0,0,0,0, 0,0,0,0, (ushort)(skinAllBones ? 4 : 1),0,0,0 }) writer.Write(joint);
                views.Add(new JObject { ["buffer"] = 0, ["byteOffset"] = jointOffset, ["byteLength"] = 24 });
                accessors.Add(new JObject { ["bufferView"] = views.Count-1, ["componentType"] = 5123, ["count"] = 3, ["type"] = "VEC4" });
                int joints = accessors.Count-1;
                int weights = Floats("VEC4",4, 1,0,0,0, 1,0,0,0, 1,0,0,0);
                var jointIndices = skinAllBones ? Enumerable.Range(0,names.Length).ToArray() : new[] { 0,4 };
                var matrices = new List<float>();
                foreach (int joint in jointIndices)
                {
                    float x = 0,y = 0,z = 0;
                    for (int ancestor = joint; ancestor >= 0; ancestor = parents[ancestor]) { x += translations[ancestor][0]; y += translations[ancestor][1]; z += translations[ancestor][2]; }
                    matrices.AddRange(new float[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, -x,-y,-z,1 });
                }
                int bind = Floats("MAT4",16,matrices.ToArray());
                root["meshes"][0]["primitives"][0]["attributes"]["JOINTS_0"] = joints;
                root["meshes"][0]["primitives"][0]["attributes"]["WEIGHTS_0"] = weights;
                nodes[0]["skin"] = 0;
                root["skins"] = new JArray(new JObject { ["joints"] = new JArray(jointIndices.Select(joint => joint+1)), ["inverseBindMatrices"] = bind });
                root["extensionsUsed"] = new JArray("VRMC_vrm");
                root["extensions"] = new JObject {
                    ["VRMC_vrm"] = new JObject {
                        ["specVersion"] = "1.0",
                        ["meta"] = new JObject { ["name"] = "Original test skeleton", ["version"] = "1", ["authors"] = new JArray("Maestro tests"), ["licenseUrl"] = "https://vrm.dev/licenses/1.0/", ["avatarPermission"] = "everyone", ["commercialUsage"] = "corporation", ["creditNotation"] = "required", ["modification"] = "allowModificationRedistribution" },
                        ["humanoid"] = new JObject { ["humanBones"] = human }
                    }
                };
            }
            root["buffers"] = new JArray(new JObject { ["byteLength"] = data.Length }); edit?.Invoke(root);
            return Pack(root, data.ToArray());
        }
        public static byte[] Mixamo(Action<JObject> edit = null) => Create(root => {
            root.Remove("extensions"); root.Remove("extensionsUsed");
            string[] names = { "Hips","Spine","Spine1","Neck","Head","LeftArm","LeftForeArm","LeftHand","RightArm","RightForeArm","RightHand","LeftUpLeg","LeftLeg","LeftFoot","RightUpLeg","RightLeg","RightFoot" };
            for (int i = 0; i < names.Length; i++) root["nodes"][i+1]["name"] = "mixamorig:"+names[i];
            edit?.Invoke(root);
        },avatar:true,skinAllBones:true);
        public static byte[] HipTravel(float end=1)
        {
            var original=Mixamo();int jsonLength=BitConverter.ToInt32(original,12);var root=JObject.Parse(Encoding.UTF8.GetString(original,20,jsonLength));
            using var binary=new MemoryStream();using var writer=new BinaryWriter(binary);writer.Write(original,28+jsonLength,(int)root["buffers"][0]["byteLength"]);int offset=(int)binary.Position;
            foreach(float value in new[]{0f,1f,0f,end,1f,0f})writer.Write(value);
            var views=(JArray)root["bufferViews"];views.Add(new JObject {["buffer"]=0,["byteOffset"]=offset,["byteLength"]=24});
            var accessors=(JArray)root["accessors"];accessors.Add(new JObject {["bufferView"]=views.Count-1,["componentType"]=5126,["count"]=2,["type"]="VEC3"});
            root["animations"]=new JArray(new JObject {["name"]="Authored travel",["samplers"]=new JArray(new JObject {["input"]=2,["output"]=accessors.Count-1}),["channels"]=new JArray(new JObject {["sampler"]=0,["target"]=new JObject {["node"]=1,["path"]="translation"}})});
            root["buffers"][0]["byteLength"]=(int)binary.Length;return Pack(root,binary.ToArray());
        }
        public static byte[] TranslationMotion(string interpolation, float end = 1)
        {
            var original = Create(); int jsonLength = BitConverter.ToInt32(original,12);
            var root = JObject.Parse(Encoding.UTF8.GetString(original,20,jsonLength));
            using var binary = new MemoryStream(); using var writer = new BinaryWriter(binary);
            int binaryLength = (int)root["buffers"][0]["byteLength"]; writer.Write(original,28+jsonLength,binaryLength);
            int offset = (int)binary.Position;
            float[] values = interpolation == "CUBICSPLINE" ? new float[] { 0,0,0, 0,0,0, 0,0,2, 0,0,0, 0,0,end, 0,0,0 } : new float[] { 0,0,0, 0,0,end };
            foreach (float value in values) writer.Write(value);
            var views = (JArray)root["bufferViews"]; views.Add(new JObject { ["buffer"] = 0,["byteOffset"] = offset,["byteLength"] = values.Length*4 });
            var accessors = (JArray)root["accessors"]; accessors.Add(new JObject { ["bufferView"] = views.Count-1,["componentType"] = 5126,["count"] = values.Length/3,["type"] = "VEC3" });
            root["animations"][0]["samplers"][0]["output"] = accessors.Count-1; root["animations"][0]["samplers"][0]["interpolation"] = interpolation;
            root["animations"][0]["channels"][0]["target"]["path"] = "translation";
            root["buffers"][0]["byteLength"] = binary.Length; return Pack(root,binary.ToArray());
        }
        public static byte[] CubicRotationMotion()
        {
            var original = Create(); int jsonLength = BitConverter.ToInt32(original,12);
            var root = JObject.Parse(Encoding.UTF8.GetString(original,20,jsonLength));
            using var binary = new MemoryStream(); using var writer = new BinaryWriter(binary);
            writer.Write(original,28+jsonLength,(int)root["buffers"][0]["byteLength"]); int offset = (int)binary.Position;
            float[] values = { 0,0,0,0, 0,0,0,1, 0,2,0,0, 0,0,0,0, 0,.70710678f,0,.70710678f, 0,0,0,0 };
            foreach (float value in values) writer.Write(value);
            var views = (JArray)root["bufferViews"]; views.Add(new JObject { ["buffer"] = 0,["byteOffset"] = offset,["byteLength"] = values.Length*4 });
            var accessors = (JArray)root["accessors"]; accessors.Add(new JObject { ["bufferView"] = views.Count-1,["componentType"] = 5126,["count"] = values.Length/4,["type"] = "VEC4" });
            root["animations"][0]["samplers"][0]["output"] = accessors.Count-1; root["animations"][0]["samplers"][0]["interpolation"] = "CUBICSPLINE";
            root["buffers"][0]["byteLength"] = binary.Length; return Pack(root,binary.ToArray());
        }
        public static byte[] MorphMotion()
        {
            var original = Create(); int jsonLength = BitConverter.ToInt32(original,12);
            var root = JObject.Parse(Encoding.UTF8.GetString(original,20,jsonLength));
            using var binary = new MemoryStream(); using var writer = new BinaryWriter(binary);
            writer.Write(original,28+jsonLength,(int)root["buffers"][0]["byteLength"]);
            int Add(string type,int components,float[] values)
            {
                int offset = (int)binary.Position; foreach (float value in values) writer.Write(value);
                var views = (JArray)root["bufferViews"]; views.Add(new JObject { ["buffer"] = 0,["byteOffset"] = offset,["byteLength"] = values.Length*4 });
                var accessors = (JArray)root["accessors"]; accessors.Add(new JObject { ["bufferView"] = views.Count-1,["componentType"] = 5126,["count"] = values.Length/components,["type"] = type }); return accessors.Count-1;
            }
            int positions = Add("VEC3",3,new float[] { 0,0,0, 0,0,0, 0,0,1 });
            int values = Add("SCALAR",1,new float[] { 0,1 });
            root["meshes"][0]["primitives"][0]["targets"] = new JArray(new JObject { ["POSITION"] = positions });
            root["meshes"][0]["extras"] = new JObject { ["targetNames"] = new JArray("Smile") };
            root["meshes"][0]["weights"] = new JArray(0);
            root["animations"][0]["samplers"][0]["output"] = values;
            root["animations"][0]["channels"][0]["target"]["path"] = "weights";
            root["buffers"][0]["byteLength"] = binary.Length; return Pack(root,binary.ToArray());
        }
        public static byte[] Pack(JObject root, byte[] binary)
        {
            byte[] json = Encoding.UTF8.GetBytes(root.ToString(Formatting.None)); int jsonLength = (json.Length+3)/4*4, binaryLength = (binary.Length+3)/4*4;
            using var file = new MemoryStream(); using var writer = new BinaryWriter(file);
            writer.Write(0x46546c67u); writer.Write(2u); writer.Write(28+jsonLength+binaryLength); writer.Write(jsonLength); writer.Write(0x4e4f534au); writer.Write(json);
            for (int i = json.Length; i < jsonLength; i++) writer.Write((byte)32);
            writer.Write(binaryLength); writer.Write(0x004e4942u); writer.Write(binary); for (int i = binary.Length; i < binaryLength; i++) writer.Write((byte)0); return file.ToArray();
        }
    }
}
