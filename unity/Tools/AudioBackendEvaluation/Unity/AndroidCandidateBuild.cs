// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maestro.AudioEvaluation.Editor
{
    public static class AndroidCandidateBuild
    {
        [Serializable] sealed class Evidence
        {
            public string result, unity, package, backend, architecture, stripping;
            public ulong bytes;
            public double seconds;
            public int errors, warnings, minimumSdk, targetSdk;
            public bool developmentOnly=true, deviceRun=false, releaseKeyUsed=false;
        }
        static string DirectorySetting(string name)
        {
            var path=Environment.GetEnvironmentVariable(name);
            if(string.IsNullOrEmpty(path)||!Directory.Exists(path)) throw new InvalidOperationException("Missing toolchain directory: "+name);
            return Path.GetFullPath(path);
        }
        static void Plugin(string path,bool android)
        {
            var plugin=AssetImporter.GetAtPath(path) as PluginImporter;
            if(!plugin) throw new InvalidOperationException("Missing reviewed native plugin: "+path);
            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithEditor(!android);
            plugin.SetCompatibleWithPlatform(BuildTarget.Android,android);
            plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64,!android);
            if(android) plugin.SetPlatformData(BuildTarget.Android,"CPU","ARM64");
            else { plugin.SetEditorData("CPU","x86_64"); plugin.SetEditorData("OS","Windows"); }
            plugin.SaveAndReimport();
        }
        public static void Build()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string output=Environment.GetEnvironmentVariable("MAESTRO_AUDIO_PROBE_APK");
            if(!project.Replace('\\','/').Contains("/.quest-evidence/audio-unity/")||string.IsNullOrEmpty(output)||
               !Path.IsPathFullyQualified(output)||Path.GetDirectoryName(Path.GetFullPath(output))!=Path.GetDirectoryName(project))
                throw new InvalidOperationException("Only an isolated audio evidence project may build this probe");
            string oldSdk=AndroidExternalToolsSettings.sdkRootPath, oldNdk=AndroidExternalToolsSettings.ndkRootPath,
                oldJdk=AndroidExternalToolsSettings.jdkRootPath;
            int exit=1;
            try
            {
                AndroidExternalToolsSettings.sdkRootPath=DirectorySetting("MAESTRO_ANDROID_SDK");
                AndroidExternalToolsSettings.ndkRootPath=DirectorySetting("MAESTRO_ANDROID_NDK");
                AndroidExternalToolsSettings.jdkRootPath=DirectorySetting("MAESTRO_ANDROID_JDK");
                PlayerSettings.companyName="Maestro"; PlayerSettings.productName="Native audio evaluation";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.maestro.audioprobe");
                PlayerSettings.Android.bundleVersionCode=1; PlayerSettings.bundleVersion="0.0.1";
                PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
                PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.GameActivity;
                PlayerSettings.Android.useCustomKeystore=false;
                PlayerSettings.Android.forceInternetPermission=false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android,ApiCompatibilityLevel.NET_Standard);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.High);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[] {GraphicsDeviceType.Vulkan});
                EditorUserBuildSettings.buildAppBundle=false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
                EditorUserBuildSettings.development=true;
                EditorUserBuildSettings.allowDebugging=false; EditorUserBuildSettings.connectProfiler=false;
                Plugin("Assets/Plugins/SteamAudio/Binaries/Windows/x86_64/phonon.dll",false);
                Plugin("Assets/Plugins/SteamAudio/Binaries/Android/arm64/libphonon.so",true);
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("Probe camera").AddComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
                EditorSceneManager.SaveScene(scene,"Assets/Evaluation/Probe.unity"); AssetDatabase.SaveAssets();
                const BuildOptions options=BuildOptions.Development|BuildOptions.CleanBuildCache;
                if((options&(BuildOptions.AutoRunPlayer|BuildOptions.ConnectToHost))!=0) throw new InvalidOperationException("Build only");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Evaluation/Probe.unity"},
                    target=BuildTarget.Android,locationPathName=output,options=options});
                var result=report.summary;
                File.WriteAllText(Path.ChangeExtension(output,".build.json"),JsonUtility.ToJson(new Evidence {
                    result=result.result.ToString(),unity=Application.unityVersion,package="com.maestro.audioprobe",
                    backend="IL2CPP",architecture="ARM64",stripping="High",minimumSdk=32,targetSdk=34,
                    bytes=result.totalSize,seconds=result.totalTime.TotalSeconds,errors=result.totalErrors,warnings=result.totalWarnings
                },true));
                if(result.result!=BuildResult.Succeeded) throw new InvalidOperationException("Android audio probe build failed");
                exit=0;
            }
            catch(Exception error) { Debug.LogException(error); }
            finally
            {
                AndroidExternalToolsSettings.sdkRootPath=oldSdk; AndroidExternalToolsSettings.ndkRootPath=oldNdk;
                AndroidExternalToolsSettings.jdkRootPath=oldJdk;
                if(Application.isBatchMode) EditorApplication.Exit(exit);
            }
        }
    }
}
#endif
