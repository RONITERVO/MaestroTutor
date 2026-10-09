// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Maestro.Quest.Editor
{
    public static class QuestBehaviourCatalog
    {
        [MenuItem("Maestro/Export behaviour catalog")]
        public static void Export()
        {
            var manifest=BehaviourCatalog.Manifest();
            // Discover native implementation inputs so a new helper cannot be
            // omitted from drift checks. Tests and .meta files are not runtime
            // behavior. Keep this scope identical to verify-behaviour-catalog.
            var extensions=new[]{".cs",".shader",".cginc",".hlsl",".asmdef",".asmref"};
            var sources=Directory.GetFiles("Assets/Maestro/Runtime","*",SearchOption.AllDirectories)
                .Where(path=>extensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))
                .Concat(new[]{"Assets/Maestro/Resources/MaestroRoomAudio.mixer","Assets/Maestro/Resources/RoomGuides.json"})
                .Concat(Directory.GetFiles("Assets/Maestro/Resources/Creation/Templates","*",SearchOption.AllDirectories).Where(path=>string.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase)))
                .Concat(Directory.GetFiles("Assets/Maestro/Resources/Programs/Modules","*",SearchOption.AllDirectories).Where(path=>string.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase)))
                .Select(path=>path.Replace("\\","/").Substring("Assets/Maestro/".Length))
                .Distinct(StringComparer.Ordinal).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            using var sha=SHA256.Create();
            manifest["sources"]=new JArray(sources.Select(path=>new JObject {
                ["path"]="unity/MaestroQuest/Assets/Maestro/"+path,
                ["sha256"]=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText("Assets/Maestro/"+path).Replace("\r\n","\n")))).Replace("-","").ToLowerInvariant(),
            }));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/behaviour-catalog.json",manifest.ToString(Formatting.Indented)+"\n",new System.Text.UTF8Encoding(false));
        }
    }
}
