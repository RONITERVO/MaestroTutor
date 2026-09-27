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
            var sources=new[] { "Runtime/Programs/BehaviourCatalog.cs", "Runtime/Programs/CapabilityArguments.cs", "Runtime/Programs/BehaviourProgram.cs", "Runtime/Rules/RuleDocument.cs", "Runtime/Creation/RoomRecipe.cs", "Runtime/Creation/RecipeTemplates.cs", "Runtime/Programs/LegacyCapabilityAdapters.cs" }
                .Concat(Directory.GetFiles("Assets/Maestro/Runtime/Capabilities","*.cs").Select(path=>path.Replace("\\","/").Substring("Assets/Maestro/".Length)).OrderBy(path=>path,StringComparer.Ordinal)).ToArray();
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
