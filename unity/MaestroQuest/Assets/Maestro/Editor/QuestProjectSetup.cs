// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Maestro.Quest.Editor
{
    public static class QuestProjectSetup
    {
        public const string ScenePath = "Assets/Maestro/Scenes/MaestroRoom.unity";

        [MenuItem("Maestro/Configure development project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Maestro/Scenes");
            Directory.CreateDirectory("Assets/XR/Settings");
            Directory.CreateDirectory("Assets/Plugins/Android");
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "Maestro";
            PlayerSettings.productName = "Maestro Quest";
            PlayerSettings.bundleVersion = "1.0.0";
            // Development identity only. Store identity is a separate release input.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maestro.quest.development");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            // Meta's OpenXR build requires linear lighting. The browser shader
            // decodes its raw sRGB pixels before Unity's final display conversion.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            var playerObject = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
            if (playerObject)
            {
                var player = new SerializedObject(playerObject);
                var handler = player.FindProperty("activeInputHandler");
                if (handler != null) { handler.intValue = 1; player.ApplyModifiedPropertiesWithoutUndo(); }
            }

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget xr))
            {
                xr = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(xr, "Assets/XR/Settings/XRGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, xr, true);
            }
            if (!xr.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) xr.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var manager = xr.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android)) throw new InvalidOperationException("Could not configure the Android OpenXR loader.");
            xr.SettingsForBuildTarget(BuildTargetGroup.Android).InitManagerOnStart = true;
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (!settings) throw new InvalidOperationException("OpenXR settings were not created.");
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            // The old "handtracking" ID now selects Microsoft's interaction profile.
            var legacyHands = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, "com.unity.openxr.feature.input.handtracking");
            if (legacyHands) { legacyHands.enabled = false; EditorUtility.SetDirty(legacyHands); }
            foreach (var id in new[] {
                "com.unity.openxr.feature.metaquest",
                "com.meta.openxr.feature.metaxr",
                "com.unity.openxr.feature.compositionlayers",
                "com.unity.openxr.feature.input.metaquestplus",
                "com.unity.openxr.feature.input.oculustouch",
                "com.unity.openxr.feature.input.handtrackingsubsystem",
                "com.unity.openxr.feature.input.metahandtrackingaim",
                "com.unity.openxr.feature.arfoundation-meta-session",
                "com.unity.openxr.feature.arfoundation-meta-camera"
            })
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (!feature) throw new InvalidOperationException("Missing required XR feature: " + id);
                feature.enabled = true;
                if (id == "com.unity.openxr.feature.metaquest")
                {
                    // Browser texture sharing uses GLES. This Meta optimization is Vulkan-only.
                    var serialized = new SerializedObject(feature);
                    var discard = serialized.FindProperty("m_optimizeBufferDiscards");
                    if (discard != null) discard.boolValue = false;
                    // The pinned OpenXR package uses "eureka" internally for Quest 3.
                    // Do not inherit new device targets when an SDK adds them by default.
                    var devices = serialized.FindProperty("targetDevices");
                    if (devices == null || !devices.isArray) throw new InvalidOperationException("Pinned OpenXR target-device schema changed.");
                    int quest3 = 0;
                    for (int i = 0; i < devices.arraySize; i++)
                    {
                        var device = devices.GetArrayElementAtIndex(i);
                        bool selected = device.FindPropertyRelative("manifestName").stringValue == "eureka";
                        device.FindPropertyRelative("enabled").boolValue = selected;
                        if (selected) quest3++;
                    }
                    if (quest3 != 1) throw new InvalidOperationException("Pinned OpenXR Quest 3 target was not found exactly once.");
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(xr);
            var metaConfig = OVRProjectConfig.CachedProjectConfig;
            // Quest 3 is the current verified hardware. Expand only with device QA.
            // Meta 207 otherwise adds VR Glasses ("stanley"), rejected by its uploader.
            metaConfig.targetDeviceTypes.Clear();
            metaConfig.targetDeviceTypes.Add(OVRProjectConfig.DeviceType.Quest3);
            metaConfig.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
            metaConfig.anchorSupport = OVRProjectConfig.AnchorSupport.Enabled;
            metaConfig.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            metaConfig.systemLoadingScreenBackground = OVRProjectConfig.SystemLoadingScreenBackground.ContextualPassthrough;
            PlayerSettings.SplashScreen.show = false;
            metaConfig.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            OVRProjectConfig.CommitProjectConfig(metaConfig);
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = "RoomObstacles";
            layers.GetArrayElementAtIndex(9).stringValue = "TrackedPushers";
            layers.GetArrayElementAtIndex(10).stringValue = "LooseItems";
            layers.GetArrayElementAtIndex(11).stringValue = "ScannedEnvironment";
            tags.ApplyModifiedPropertiesWithoutUndo();
            ConfigureAnimations();
            // A Resources material keeps the dynamically used shader in player builds.
            const string pigmentPath = "Assets/Maestro/Resources/PencilPalette.mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(pigmentPath)) AssetDatabase.CreateAsset(IllustratedMaterials.Create(IllustratedMaterials.Paper), pigmentPath);
            // Runtime glTF decoding uses these before applying the shared pencil material.
            foreach (var shaderName in new[] { "Standard", "UniGLTF/UniUnlit" })
            {
                var shader = Shader.Find(shaderName); if (!shader) throw new InvalidOperationException("Missing model import shader: " + shaderName);
                string path = "Assets/Maestro/Resources/Import-" + shaderName.Replace('/', '-') + ".mat";
                if (!AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.CreateAsset(new Material(shader), path);
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Maestro room", typeof(MaestroRoom));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            QuestIncludedAvatar.Validate();
            QuestBehaviourCatalog.Export();
            Debug.Log("MAESTRO_PROJECT_CONFIGURED");
        }

        static void ConfigureAnimations()
        {
            const string modelPath = "Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (!importer.clipAnimations.Any(clip => clip.name == "Walk"))
            {
                var walk = importer.defaultClipAnimations.First(clip => clip.takeName.Split('|').Last() == "Walk");
                walk.name = "Walk"; walk.loopTime = true;
                importer.clipAnimations = importer.clipAnimations.Append(walk).ToArray(); importer.SaveAndReimport();
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            const string path = "Assets/Maestro/Resources/Avatars/MaestroAnimations.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var name in new[] { "Idle", "Listening", "Speaking", "Greeting", "Pointing", "Walk" })
            {
                var clip = clips.FirstOrDefault(value => value.name.Split('|').Last() == name);
                if (!clip) throw new InvalidOperationException("Missing included animation: " + name + ". Found: " + string.Join(", ", clips.Select(value => value.name)));
                var state = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == name) ?? machine.AddState(name);
                state.motion = clip;
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
        }
    }
}
