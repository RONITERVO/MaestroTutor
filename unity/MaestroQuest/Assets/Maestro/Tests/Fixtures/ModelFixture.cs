// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Tests
{
    // Original synthetic geometry/animation, with no external asset licensing dependency.
    public static class ModelFixture
    {
        public static byte[] Create(Action<JObject> edit = null, bool avatar = false)
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
