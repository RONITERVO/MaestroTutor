// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Maestro.Quest.Imports
{
    public sealed class MotionPack
    {
        public const int MaximumBytes = 8 * 1024 * 1024, MaximumExtractionBytes = 32 * 1024 * 1024;
        public byte[] Bytes { get; private set; }
        public string Hash { get; private set; }
        public string RigHash { get; private set; }
        public string Name { get; private set; }
        public string SourceHash { get; private set; }
        public string Attribution { get; private set; }
        public int SourceClip { get; private set; }
        public int CurveValues { get; private set; }
        public float Duration { get; private set; }
        public bool Short => Duration < .1f;
        internal JObject Document { get; private set; }
        public bool ReverseX => (string)Document["extras"]?["maestroMotion"]?["axis"] == "X";

        static JObject Json(byte[] bytes) => JObject.Parse(Encoding.UTF8.GetString(bytes,20,BitConverter.ToInt32(bytes,12)));
        static void Require(bool value,string message) { if (!value) throw new ModelImportException(message); }
        static JArray Floats(JToken token,params float[] defaults) => token == null ? new JArray(defaults) : new JArray(((JArray)token).Select(x => (float)x));
        static JObject Node(JToken source)
        {
            Require(source["name"]?.Type == JTokenType.String,"Reusable motions require named nodes.");
            string name = (string)source["name"];
            Require(!string.IsNullOrWhiteSpace(name) && !name.Contains('/') && !name.Contains('\\') && name.All(c => !char.IsControl(c)), "Reusable motions need named nodes without path separators.");
            var result = new JObject { ["name"] = name, ["children"] = source["children"]?.DeepClone() ?? new JArray() };
            if (source["matrix"] != null) result["matrix"] = Floats(source["matrix"]);
            else { result["translation"] = Floats(source["translation"],0,0,0); result["rotation"] = Floats(source["rotation"],0,0,0,1); result["scale"] = Floats(source["scale"],1,1,1); }
            if (source["weights"] != null) result["weights"] = Floats(source["weights"]);
            return result;
        }
        static JArray Nodes(JObject source)
        {
            var result = new JArray(((JArray)source["nodes"]).Select(Node));
            Require(result.Select(x => (string)x["name"]).Distinct().Count() == result.Count,"Reusable motions require unique node names.");
            return result;
        }
        static string Axis(JObject source) => source["extensions"]?["VRMC_vrm"] != null ? "X" : (string)source["extras"]?["maestroMotion"]?["axis"] ?? "Z";
        static byte[] Accessor(JObject root, byte[] bytes,int index)
        {
            var accessor = root["accessors"][index]; var view = root["bufferViews"][(int)accessor["bufferView"]];
            Require((int)accessor["componentType"] == 5126 && !((bool?)accessor["normalized"] ?? false),"Reusable motion curves and inverse binds must use float accessors.");
            int components = (string)accessor["type"] switch { "SCALAR" => 1,"VEC3" => 3,"VEC4" => 4,"MAT4" => 16,_ => 0 };
            Require(components > 0,"This motion accessor type is unsupported.");
            int width = components*4, count = (int)accessor["count"], stride = (int?)view["byteStride"] ?? width;
            int start = 28+BitConverter.ToInt32(bytes,12)+((int?)view["byteOffset"] ?? 0)+((int?)accessor["byteOffset"] ?? 0);
            var result = new byte[checked(count*width)];
            for (int i=0;i<count;i++) Buffer.BlockCopy(bytes,start+i*stride,result,i*width,width);
            return result;
        }
        static string Fingerprint(JObject root,byte[] bytes)
        {
            var skins = new JArray();
            foreach (var skin in (JArray)root["skins"] ?? new JArray()) skins.Add(new JObject {
                ["joints"] = skin["joints"].DeepClone(),
                ["binds"] = skin["inverseBindMatrices"] == null ? null : ModelLibrary.Hash(Accessor(root,bytes,(int)skin["inverseBindMatrices"]))
            });
            var shapes = new JArray();
            foreach (var node in (JArray)root["nodes"])
            {
                var mesh = node["mesh"] == null ? null : root["meshes"]?[(int)node["mesh"]];
                // Morph names are part of compatibility; texture/mesh bytes are not.
                shapes.Add(mesh?["extras"]?["targetNames"]?.DeepClone());
            }
            var identity = new JObject { ["axis"] = Axis(root),["nodes"] = Nodes(root),["skins"] = skins,["shapes"] = shapes };
            return ModelLibrary.Hash(Encoding.UTF8.GetBytes(identity.ToString(Formatting.None)));
        }
        public static string RigIdentity(byte[] source)
        {
            ModelInspection.Inspect(source); var root = Json(source);
            Require(root["extensions"]?["VRM"] == null || root["extensions"]?["VRMC_vrm"] != null,"Export VRM 0.x animations as GLB or VRM 1.0 before making reusable motions.");
            return Fingerprint(root,source);
        }
        public static MotionPack[] Extract(string fileName,byte[] source)
        {
            var info = ModelInspection.Inspect(source); var root = Json(source);
            Require(root["extensions"]?["VRM"] == null || root["extensions"]?["VRMC_vrm"] != null,"Export VRM 0.x animations as GLB or VRM 1.0 before making reusable motions.");
            Require(info.Clips > 0,"This file has no animation clips to add to the motion library.");
            string rig = Fingerprint(root,source), sourceHash = ModelLibrary.Hash(source);
            var result = new List<MotionPack>();
            int clipIndex = 0, extractedBytes = 0;
            foreach (var animation in (JArray)root["animations"])
            {
                using var binary = new MemoryStream();
                var views = new JArray(); var accessors = new JArray(); var copied = new Dictionary<int,int>();
                int Copy(int index)
                {
                    if (copied.TryGetValue(index,out int prior)) return prior;
                    var data = Accessor(root,source,index); int offset = (int)binary.Position; binary.Write(data,0,data.Length);
                    views.Add(new JObject { ["buffer"] = 0,["byteOffset"] = offset,["byteLength"] = data.Length });
                    var original = root["accessors"][index];
                    accessors.Add(new JObject { ["bufferView"] = views.Count-1,["componentType"] = 5126,["count"] = (int)original["count"],["type"] = (string)original["type"] });
                    copied[index] = accessors.Count-1; return accessors.Count-1;
                }
                var nodes = Nodes(root); var meshes = new JArray(); var skins = new JArray();
                for (int i=0;i<nodes.Count;i++)
                {
                    var original = root["nodes"][i]; var mesh = original["mesh"] == null ? null : root["meshes"][(int)original["mesh"]];
                    var names = mesh?["extras"]?["targetNames"];
                    if (names != null)
                    {
                        nodes[i]["mesh"] = meshes.Count;
                        meshes.Add(new JObject { ["primitives"] = new JArray(),["extras"] = new JObject { ["targetNames"] = names.DeepClone() } });
                    }
                }
                foreach (var original in (JArray)root["skins"] ?? new JArray())
                {
                    var skin = new JObject { ["joints"] = original["joints"].DeepClone() };
                    if (original["inverseBindMatrices"] != null) skin["inverseBindMatrices"] = Copy((int)original["inverseBindMatrices"]);
                    skins.Add(skin);
                }
                var clip = (JObject)animation.DeepClone(); clip["name"] = "Motion"; clip.Remove("extras"); clip.Remove("extensions");
                foreach (var sampler in (JArray)clip["samplers"]) { sampler["input"] = Copy((int)sampler["input"]); sampler["output"] = Copy((int)sampler["output"]); }
                var packed = new JObject {
                    ["asset"] = new JObject { ["version"] = "2.0" },["scene"] = (int?)root["scene"] ?? 0,["scenes"] = root["scenes"].DeepClone(),
                    ["nodes"] = nodes,["skins"] = skins,["meshes"] = meshes,["animations"] = new JArray(clip),
                    ["buffers"] = new JArray(new JObject { ["byteLength"] = binary.Length }),["bufferViews"] = views,["accessors"] = accessors,
                    ["extras"] = new JObject { ["maestroMotion"] = new JObject { ["version"] = 1,["axis"] = Axis(root) } }
                };
                var asset = Read(Pack(packed,binary.ToArray()));
                extractedBytes = checked(extractedBytes+asset.Bytes.Length);
                Require(extractedBytes <= MaximumExtractionBytes,"This export expands beyond the 32 MB motion import budget. Export fewer clips together.");
                Require(asset.RigHash == rig,"The motion's rig changed while extracting it.");
                asset.Name = ModelLibrary.SafeName((string)animation["name"] ?? Path.GetFileNameWithoutExtension(fileName));
                asset.SourceHash = sourceHash; asset.SourceClip = clipIndex++; asset.Attribution = info.Attribution;
                result.Add(asset);
            }
            return result.ToArray();
        }
        internal float[] Values(int index)
        {
            var data = Accessor(Document,Bytes,index); var result = new float[data.Length/4]; Buffer.BlockCopy(data,0,result,0,data.Length); return result;
        }
        public static MotionPack Read(byte[] bytes)
        {
            Require(bytes != null && bytes.Length <= MaximumBytes,"A reusable motion must fit within 8 MB.");
            ModelInspection.InspectMotion(bytes); var root = Json(bytes);
            var schema = root["extras"]?["maestroMotion"];
            Require(schema is JObject && schema["version"]?.Type == JTokenType.Integer && (int)schema["version"] == 1 && schema["axis"]?.Type == JTokenType.String && new[] { "X","Z" }.Contains((string)schema["axis"]),"Unsupported motion-pack version or axis convention.");
            var pack = new MotionPack { Bytes = bytes,Hash = ModelLibrary.Hash(bytes),RigHash = Fingerprint(root,bytes),Document = root };
            var animation = root["animations"][0]; var channels = (JArray)animation["channels"];
            Require(channels.Count > 0,"This motion contains no channels."); var targets = new HashSet<string>();
            foreach (var channel in channels)
            {
                int node = (int)channel["target"]["node"]; string path = (string)channel["target"]["path"];
                Require(targets.Add(node+":"+path),"A motion must not write the same channel twice.");
                var sampler = animation["samplers"][(int)channel["sampler"]]; var times = pack.Values((int)sampler["input"]); var values = pack.Values((int)sampler["output"]);
                int components = path == "rotation" ? 4 : 3;
                if (path == "weights")
                {
                    var target = root["nodes"][node]; var names = target["mesh"] == null ? null : root["meshes"][(int)target["mesh"]]["extras"]?["targetNames"] as JArray;
                    Require(names != null && names.Count > 0 && names.Count <= 128 && names.All(x => x.Type == JTokenType.String && !string.IsNullOrEmpty((string)x)) && names.Select(x => (string)x).Distinct().Count() == names.Count,"Morph motions require unique named blend shapes.");
                    components = names.Count;
                }
                int multiple = (string)sampler["interpolation"] == "CUBICSPLINE" ? 3 : 1;
                Require(values.Length == checked(times.Length*components*multiple),"Motion channel dimensions do not match its key times.");
                pack.CurveValues += checked(times.Length*components); pack.Duration = Math.Max(pack.Duration,times[^1]);
                if (path == "rotation") for (int k=0;k<times.Length;k++)
                {
                    int start = (k*multiple+(multiple == 3 ? 1 : 0))*4; double norm = 0;
                    for (int j=0;j<4;j++) norm += (double)values[start+j]*values[start+j];
                    Require(norm > .000001 && norm < 4,"The motion contains an invalid joint rotation.");
                }
            }
            Require(pack.Duration > 0 && pack.CurveValues <= 800000,"This motion is empty or exceeds the playback budget.");
            return pack;
        }
        static byte[] Pack(JObject root,byte[] binary)
        {
            byte[] json = Encoding.UTF8.GetBytes(root.ToString(Formatting.None)); int jsonLength = (json.Length+3)/4*4, binaryLength = (binary.Length+3)/4*4;
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(0x46546c67u); writer.Write(2u); writer.Write(28+jsonLength+binaryLength); writer.Write(jsonLength); writer.Write(0x4e4f534au); writer.Write(json);
            for (int i=json.Length;i<jsonLength;i++) writer.Write((byte)32);
            writer.Write(binaryLength); writer.Write(0x004e4942u); writer.Write(binary); for (int i=binary.Length;i<binaryLength;i++) writer.Write((byte)0);
            return stream.ToArray();
        }
    }
}
