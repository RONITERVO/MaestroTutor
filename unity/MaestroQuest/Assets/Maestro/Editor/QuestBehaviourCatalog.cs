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
            var sources=new[] { "Runtime/Programs/BehaviourCatalog.cs", "Runtime/Programs/CapabilityArguments.cs", "Runtime/Programs/BehaviourProgram.cs","Runtime/Programs/ProgramMachine.cs", "Runtime/Programs/ProgramSubscriptions.cs", "Runtime/Programs/PhysicsMotionSubscription.cs", "Runtime/Programs/NativeObjectFacts.cs", "Runtime/Programs/ProgramData.cs", "Runtime/Programs/ProgramModules.cs", "Runtime/Programs/ProgramModuleLibrary.cs", "Runtime/Rules/RuleDocument.cs", "Runtime/Creation/RoomRecipe.cs", "Runtime/Creation/RoomMotion.cs", "Runtime/Creation/RoomMotionEdits.cs", "Runtime/Creation/AnimationRecording.cs", "Runtime/Creation/AnimationWorkshop.cs", "Runtime/Creation/AnimationPoseRetention.cs", "Runtime/Creation/AnimationPosing.cs", "Runtime/Creation/RoomEditor.cs", "Runtime/Avatar/AvatarPoseRig.cs", "Runtime/Imports/ImportWorkshop.cs","Runtime/Imports/ImportSelection.cs","Runtime/Imports/ImportObservation.cs","Runtime/Interaction/ControllerPreferences.cs","Runtime/Interaction/MovementControls.cs","Runtime/Interaction/MovementConfiguration.cs","Runtime/Interaction/MovementModes.cs","Runtime/Interaction/VirtualRoomView.cs","Runtime/Imports/ImportAcceptance.cs","Runtime/Imports/ImportBatchWorkshop.cs","Runtime/Imports/ImportBatchSession.cs","Runtime/Imports/MotionBatch.cs","Runtime/Imports/AndroidMotionBatchSource.cs","Runtime/Creation/AvatarModelSelection.cs", "Runtime/Creation/RecipeTemplates.cs", "Runtime/Programs/LegacyCapabilityAdapters.cs" }
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
