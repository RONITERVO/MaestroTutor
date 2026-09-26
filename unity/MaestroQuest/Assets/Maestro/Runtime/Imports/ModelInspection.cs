// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Imports
{
    public sealed class ModelImportException : Exception
    {
        public ModelImportException(string message) : base(message) { }
    }

    public sealed class ModelInspection
    {
        public const int MaximumBytes = 64 * 1024 * 1024;
        public int Vertices, Triangles, TexturePixels, Clips, MorphVertices;
        public bool IsAvatar;
        public string Attribution;

        // Inspect before asking Unity to allocate meshes, textures or animation curves.
        // This deliberately accepts a documented, self-contained subset of glTF 2.0.
        public static ModelInspection Inspect(byte[] bytes) => Inspect(bytes,false);
        internal static ModelInspection InspectMotion(byte[] bytes) => Inspect(bytes,true);
        static ModelInspection Inspect(byte[] bytes, bool motionOnly)
        {
            try { return new Inspector(bytes,motionOnly).Read(); }
            catch (ModelImportException) { throw; }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is OverflowException || error is InvalidCastException || error is InvalidOperationException || error is IndexOutOfRangeException || error is NullReferenceException)
            { throw new ModelImportException("This model contains invalid GLB data. Export it again as a self-contained GLB or VRM."); }
        }

        sealed class Inspector
        {
            readonly byte[] bytes;
            readonly bool motionOnly;
            JObject root;
            JArray views, accessors, nodes, meshes;
            int binary, binaryLength;
            long decoded;
            readonly ModelInspection info = new();
            public Inspector(byte[] value, bool motionOnly) { bytes = value; this.motionOnly = motionOnly; }
            void Require(bool value, string message = "This model contains invalid GLB data.") { if (!value) throw new ModelImportException(message); }
            uint U32(int at) => BitConverter.ToUInt32(bytes, at);
            public ModelInspection Read()
            {
                Require(bytes != null && bytes.Length >= 28 && bytes.Length <= MaximumBytes, "Choose a GLB or VRM file smaller than 64 MB.");
                Require(U32(0) == 0x46546c67 && U32(4) == 2 && U32(8) == bytes.Length, "Only binary glTF 2.0 (.glb or .vrm) models are supported.");
                int jsonLength = checked((int)U32(12));
                Require(U32(16) == 0x4e4f534a && jsonLength > 0 && jsonLength <= 4 * 1024 * 1024 && jsonLength % 4 == 0 && 20L + jsonLength + 8 <= bytes.Length);
                using var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes, 20, jsonLength))) { MaxDepth = 48, DateParseHandling = DateParseHandling.None };
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Ignore });
                Require(!reader.Read());
                binary = 28 + jsonLength; binaryLength = checked((int)U32(20 + jsonLength));
                Require(U32(24 + jsonLength) == 0x004e4942 && binaryLength % 4 == 0 && (long)binary + binaryLength == bytes.Length);
                Require((string)root["asset"]?["version"] == "2.0");
                foreach (var token in root.Descendants().OfType<JValue>())
                {
                    if (token.Type == JTokenType.Float) Require(double.IsFinite((double)token));
                    if (token.Type == JTokenType.String) Require(((string)token).Length <= 8192, "A model text field is too long.");
                }
                var supported = new[] { "VRM", "VRMC_vrm", "VRMC_materials_mtoon", "VRMC_springBone", "VRMC_node_constraint", "KHR_materials_unlit", "KHR_texture_transform" };
                foreach (var required in Array(root, "extensionsRequired", 16)) Require(supported.Contains((string)required), "This model requires an unsupported glTF extension. Export without mesh or texture compression.");
                var buffers = Array(root, "buffers", 1); Require(buffers.Count == 1 && buffers[0]["uri"] == null);
                int usedBytes = Integer(buffers[0], "byteLength", 1, binaryLength); Require(binaryLength - usedBytes <= 3);
                views = Array(root, "bufferViews", 4096); accessors = Array(root, "accessors", 4096);
                nodes = Array(root, "nodes", 512); meshes = Array(root, "meshes", 128);
                Require(nodes.Count > 0 && (motionOnly || meshes.Count > 0), "This file does not contain a visible 3D model.");
                foreach (var view in views)
                {
                    Require(Integer(view, "buffer", 0, 0) == 0 && view["extensions"] == null, "Compressed buffers are not supported yet.");
                    long offset = Integer(view, "byteOffset", 0, usedBytes, 0), length = Integer(view, "byteLength", 1, usedBytes);
                    Require(offset + length <= usedBytes);
                    if (view["byteStride"] != null) Require(Integer(view, "byteStride", 4, 252) % 4 == 0);
                }
                foreach (var accessor in accessors) CheckAccessor(accessor);
                var parents = Enumerable.Repeat(-1, nodes.Count).ToArray();
                for (int n = 0; n < nodes.Count; n++)
                {
                    var node = nodes[n];
                    foreach (var child in Array(node, "children", 512))
                    {
                        int c = Index(child, nodes.Count); Require(c != n && parents[c] == -1, "Model nodes must form a tree."); parents[c] = n;
                    }
                    foreach (string field in new[] { "translation", "rotation", "scale", "matrix" }) if (node[field] != null)
                    {
                        var values = Array(node, field, 16); Require(values.Count == (field == "matrix" ? 16 : field == "rotation" ? 4 : 3));
                        foreach (var value in values) Require(Math.Abs(Number(value)) <= 10000);
                    }
                    Require(node["matrix"] == null || (node["rotation"] == null && node["translation"] == null && node["scale"] == null));
                }
                for (int n = 0; n < nodes.Count; n++) { int depth = 0; for (int p = n; p != -1; p = parents[p]) Require(++depth <= 64, "The model hierarchy is too deep or contains a loop."); }
                var scenes = Array(root, "scenes", 16); Require(scenes.Count > 0); Integer(root, "scene", 0, scenes.Count - 1, 0);
                foreach (var scene in scenes) foreach (var node in Array(scene, "nodes", 512)) Require(parents[Index(node, nodes.Count)] == -1);
                int materials = Array(root, "materials", 32).Count;
                var meshVertices = new int[meshes.Count]; var meshTriangles = new int[meshes.Count];
                int primitives = 0; long morphVertices = 0;
                for (int m = 0; m < meshes.Count; m++) foreach (var primitive in Array(meshes[m], "primitives", 128))
                {
                    Require(++primitives <= 128 && Integer(primitive, "mode", 4, 4, 4) == 4, "Export a triangle mesh with at most 128 material parts.");
                    Require(primitive["extensions"] == null, "Compressed mesh primitives are not supported yet.");
                    var attributes = primitive["attributes"] as JObject; Require(attributes != null && attributes["POSITION"] != null);
                    int count = Count(Index(attributes["POSITION"], accessors.Count));
                    Require((string)accessors[Index(attributes["POSITION"], accessors.Count)]["type"] == "VEC3");
                    foreach (var property in attributes.Properties())
                    {
                        var accessor = accessors[Index(property.Value, accessors.Count)]; Require((int)accessor["count"] == count);
                        string type = (string)accessor["type"];
                        if (property.Name == "POSITION" || property.Name == "NORMAL") Require(type == "VEC3" && (int)accessor["componentType"] == 5126);
                        if (property.Name.StartsWith("JOINTS_") || property.Name.StartsWith("WEIGHTS_") || property.Name == "TANGENT") Require(type == "VEC4");
                        if (property.Name.StartsWith("TEXCOORD_")) Require(type == "VEC2");
                    }
                    int indices = count;
                    if (primitive["indices"] != null)
                    {
                        int a = Index(primitive["indices"], accessors.Count); var accessor = accessors[a]; indices = Count(a);
                        Require((string)accessor["type"] == "SCALAR" && new[] { 5121, 5123, 5125 }.Contains((int)accessor["componentType"]));
                        ForValues(accessor, value => Require(value >= 0 && value < count, "The model has invalid triangle indices."));
                    }
                    Require(indices % 3 == 0); if (primitive["material"] != null) Index(primitive["material"], materials);
                    foreach (var target in Array(primitive, "targets", 128)) foreach (var property in ((JObject)target).Properties())
                    { Require(Count(Index(property.Value, accessors.Count)) == count); morphVertices += count; }
                    Require(morphVertices <= 4000000, "Reduce the number or size of blend shapes before importing.");
                    meshVertices[m] += count; meshTriangles[m] += indices / 3;
                }
                int renderParts = 0;
                info.MorphVertices = (int)morphVertices;
                foreach (var node in nodes) if (node["mesh"] != null)
                {
                    int m = Index(node["mesh"], meshes.Count); info.Vertices += meshVertices[m]; info.Triangles += meshTriangles[m];
                    renderParts += ((JArray)meshes[m]["primitives"]).Count;
                }
                Require((motionOnly || info.Vertices > 0) && info.Vertices <= 250000 && info.Triangles <= 120000 && renderParts <= 128, "Reduce this model to 250,000 vertices, 120,000 triangles and 128 material parts or fewer.");
                var skins = Array(root, "skins", 16);
                foreach (var skin in skins)
                {
                    var joints = Array(skin, "joints", 512); Require(joints.Count > 0 && joints.Select(x => Index(x, nodes.Count)).Distinct().Count() == joints.Count);
                    if (skin["skeleton"] != null) Index(skin["skeleton"], nodes.Count);
                    if (skin["inverseBindMatrices"] != null) { int a = Index(skin["inverseBindMatrices"], accessors.Count); Require(Count(a) == joints.Count && (string)accessors[a]["type"] == "MAT4"); }
                }
                foreach (var node in nodes) if (node["skin"] != null)
                {
                    var skin = skins[Index(node["skin"], skins.Count)]; int jointCount = ((JArray)skin["joints"]).Count;
                    var mesh = meshes[Index(node["mesh"], meshes.Count)];
                    foreach (var primitive in (JArray)mesh["primitives"])
                    {
                        var attributes = (JObject)primitive["attributes"];
                        Require(attributes["JOINTS_0"] != null && attributes["WEIGHTS_0"] != null);
                        foreach (var property in attributes.Properties()) if (property.Name.StartsWith("JOINTS_"))
                        {
                            var accessor = accessors[Index(property.Value, accessors.Count)]; Require(new[] { 5121, 5123 }.Contains((int)accessor["componentType"]));
                            ForValues(accessor, value => Require(value < jointCount, "A skin references a missing joint."));
                        }
                    }
                }
                CheckTextures(); CheckAnimations();
                if (motionOnly) Require(info.Vertices == 0 && info.TexturePixels == 0 && info.Clips == 1, "A motion pack must contain exactly one clip and no rendered geometry or textures.");
                info.IsAvatar = root["extensions"]?["VRMC_vrm"] != null || root["extensions"]?["VRM"] != null;
                var meta = root["extensions"]?["VRMC_vrm"]?["meta"] ?? root["extensions"]?["VRM"]?["meta"];
                info.Attribution = meta?.ToString(Formatting.Indented) ?? (string)root["asset"]?["copyright"] ?? "No author or license information is included in this model.";
                return info;
            }
            void CheckAccessor(JToken accessor)
            {
                Require(accessor["sparse"] == null, "Sparse accessors are not supported yet. Export dense mesh and animation data.");
                int count = Integer(accessor, "count", 1, 750000), components = Components((string)accessor["type"]), size = Size(Integer(accessor, "componentType", 5120, 5126));
                Require(components > 0 && size > 0); decoded += (long)count * components;
                Require(decoded <= 16000000, "The model's decoded mesh and animation data is too large.");
                var view = views[Index(accessor["bufferView"], views.Count)];
                int packed = components * size;
                // Small-component matrices need special column alignment; restrict inverse binds to float matrices.
                Require(components < 9 || size == 4);
                int stride = Integer(view, "byteStride", packed, 252, packed), offset = Integer(accessor, "byteOffset", 0, (int)view["byteLength"], 0);
                Require(offset % size == 0 && stride % size == 0 && (long)offset + (long)(count - 1) * stride + packed <= (int)view["byteLength"]);
                if ((int)accessor["componentType"] == 5126) ForValues(accessor, value => Require(double.IsFinite(value) && Math.Abs(value) <= 1e10, "Model coordinates or animation values are invalid."));
            }
            void ForValues(JToken accessor, Action<double> check)
            {
                var view = views[Index(accessor["bufferView"], views.Count)]; int type = (int)accessor["componentType"], size = Size(type), components = Components((string)accessor["type"]);
                int offset = binary + ((int?)view["byteOffset"] ?? 0) + ((int?)accessor["byteOffset"] ?? 0);
                int stride = (int?)view["byteStride"] ?? components * size, count = (int)accessor["count"];
                for (int i = 0; i < count; i++) for (int c = 0; c < components; c++)
                {
                    int at = offset + i * stride + c * size;
                    double value = type switch { 5120 => (sbyte)bytes[at], 5121 => bytes[at], 5122 => BitConverter.ToInt16(bytes, at), 5123 => BitConverter.ToUInt16(bytes, at), 5125 => U32(at), _ => BitConverter.ToSingle(bytes, at) }; check(value);
                }
            }
            void CheckTextures()
            {
                var images = Array(root, "images", 32); var textures = Array(root, "textures", 64);
                foreach (var texture in textures) { Index(texture["source"], images.Count); if (texture["sampler"] != null) Index(texture["sampler"], Array(root, "samplers", 64).Count); }
                foreach (var image in images)
                {
                    Require(image["uri"] == null, "All model textures must be embedded in the GLB or VRM file.");
                    var view = views[Index(image["bufferView"], views.Count)]; int at = binary + ((int?)view["byteOffset"] ?? 0), end = at + (int)view["byteLength"];
                    int width = 0, height = 0;
                    if ((string)image["mimeType"] == "image/png")
                    {
                        Require(end - at >= 24 && bytes[at] == 137 && bytes[at+1] == 80 && bytes[at+2] == 78 && bytes[at+3] == 71 && bytes[at+12] == 73 && bytes[at+13] == 72 && bytes[at+14] == 68 && bytes[at+15] == 82);
                        width = Big32(at + 16); height = Big32(at + 20);
                    }
                    else if ((string)image["mimeType"] == "image/jpeg")
                    {
                        Require(end - at >= 4 && bytes[at] == 255 && bytes[at+1] == 216); at += 2;
                        while (at + 4 <= end)
                        {
                            Require(bytes[at++] == 255); while (at < end && bytes[at] == 255) at++;
                            Require(at + 3 <= end); int marker = bytes[at++]; Require(marker != 218 && marker != 217);
                            int length = Big16(at); Require(length >= 2 && at + length <= end);
                            if (marker == 192 || marker == 193 || marker == 194) { Require(length >= 8); height = Big16(at+3); width = Big16(at+5); break; }
                            at += length;
                        }
                    }
                    else throw new ModelImportException("Use embedded PNG or JPEG textures.");
                    Require(width > 0 && height > 0 && width <= 4096 && height <= 4096, "Model textures must be at most 4096 pixels on each side.");
                    info.TexturePixels += checked(width * height);
                    Require(info.TexturePixels <= 32 * 1024 * 1024, "Reduce the total texture size to 32 million pixels or fewer.");
                }
            }
            void CheckAnimations()
            {
                var animations = Array(root, "animations", 32); info.Clips = animations.Count; long keys = 0, curveValues = 0;
                foreach (var animation in animations)
                {
                    var samplers = Array(animation, "samplers", 512); var channels = Array(animation, "channels", 512);
                    foreach (var sampler in samplers)
                    {
                        int input = Index(sampler["input"], accessors.Count), output = Index(sampler["output"], accessors.Count);
                        Require((string)accessors[input]["type"] == "SCALAR" && (int)accessors[input]["componentType"] == 5126);
                        string interpolation = (string)sampler["interpolation"] ?? "LINEAR";
                        Require(new[] { "LINEAR", "STEP", "CUBICSPLINE" }.Contains(interpolation));
                        double previous = -1; ForValues(accessors[input], value => { Require(value >= 0 && value > previous && value <= 3600, "Animation key times must increase and fit within one hour."); previous = value; });
                        Require(Count(output) % (Count(input) * (interpolation == "CUBICSPLINE" ? 3 : 1)) == 0);
                        keys += Count(output);
                    }
                    foreach (var channel in channels)
                    {
                        var sampler = samplers[Index(channel["sampler"], samplers.Count)]; Index(channel["target"]?["node"], nodes.Count);
                        Require(new[] { "translation", "rotation", "scale", "weights" }.Contains((string)channel["target"]?["path"]));
                        var output = accessors[Index(sampler["output"], accessors.Count)];
                        curveValues += (long)(int)output["count"] * Components((string)output["type"]);
                        Require(curveValues <= 800000, "Reduce the number of animated channels or keyframes before importing.");
                    }
                }
                Require(keys <= 200000, "Reduce the number of animation keys to 200,000 or fewer.");
            }
            int Big16(int at) => bytes[at] * 256 + bytes[at+1];
            int Big32(int at) => checked((int)((uint)bytes[at] << 24 | (uint)bytes[at+1] << 16 | (uint)bytes[at+2] << 8 | bytes[at+3]));
            int Count(int index) => (int)accessors[index]["count"];
            static int Size(int type) => type switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => 0 };
            static int Components(string type) => type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" or "MAT2" => 4, "MAT3" => 9, "MAT4" => 16, _ => 0 };
            JArray Array(JToken parent, string key, int max)
            {
                var token = parent[key]; if (token == null) return new JArray();
                Require(token is JArray array && array.Count <= max, "The model exceeds the supported " + key + " limit."); return (JArray)token;
            }
            int Integer(JToken parent, string key, int min, int max, int? fallback = null)
            { if (parent[key] == null && fallback.HasValue) return fallback.Value; double value = Number(parent[key]); Require(value >= min && value <= max && value == Math.Truncate(value)); return (int)value; }
            int Index(JToken value, int count) { double number = Number(value); Require(number >= 0 && number < count && number == Math.Truncate(number)); return (int)number; }
            double Number(JToken value) { Require(value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)); double result = (double)value; Require(double.IsFinite(result)); return result; }
        }
    }
}
