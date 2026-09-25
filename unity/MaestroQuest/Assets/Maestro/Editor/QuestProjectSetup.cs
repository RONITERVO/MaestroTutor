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
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Gamma;
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
            foreach (var id in new[] {
                "com.unity.openxr.feature.metaquest",
                "com.unity.openxr.feature.input.metaquestplus",
                "com.unity.openxr.feature.input.oculustouch",
                "com.unity.openxr.feature.input.handtracking",
                "com.unity.openxr.feature.arfoundation-meta-session",
                "com.unity.openxr.feature.arfoundation-meta-camera"
            })
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (!feature) throw new InvalidOperationException("Missing required XR feature: " + id);
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(xr);
            ConfigureAnimations();
            // A Resources material keeps the dynamically used shader in player builds.
            const string pigmentPath = "Assets/Maestro/Resources/PencilPalette.mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(pigmentPath)) AssetDatabase.CreateAsset(IllustratedMaterials.Create(IllustratedMaterials.Paper), pigmentPath);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Maestro room", typeof(MaestroRoom));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("MAESTRO_PROJECT_CONFIGURED");
        }

        static void ConfigureAnimations()
        {
            const string modelPath = "Assets/Maestro/Resources/Avatars/DefaultMaestro.fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
            const string path = "Assets/Maestro/Resources/Avatars/MaestroAnimations.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var name in new[] { "Idle", "Listening", "Speaking", "Greeting", "Pointing" })
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
