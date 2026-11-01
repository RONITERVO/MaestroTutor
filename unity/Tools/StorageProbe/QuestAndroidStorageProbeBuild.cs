// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
namespace Maestro.Quest.Editor
{
    // Template copied only into a receipt-owned mirror by the diagnostic build tool.
    public sealed class QuestAndroidStorageProbeBuild : IPostGenerateGradleAndroidProject
    {
        const string Package="com.maestro.quest.storageprobe";
        public int callbackOrder=>100000;
        public static void Build()
        {
            if(Environment.GetEnvironmentVariable("MAESTRO_ANDROID_STORAGE_PROBE")!="1")throw new InvalidOperationException("Diagnostic build only.");
            AndroidExternalToolsSettings.sdkRootPath=Environment.GetEnvironmentVariable("MAESTRO_ANDROID_SDK");
            AndroidExternalToolsSettings.ndkRootPath=Environment.GetEnvironmentVariable("MAESTRO_ANDROID_NDK");
            AndroidExternalToolsSettings.jdkRootPath=Environment.GetEnvironmentVariable("MAESTRO_ANDROID_JDK");
            var config=Resources.Load<TextAsset>("QuestPlatform");
            if(!config||JsonUtility.FromJson<Maestro.Quest.Book.QuestPlatformConfiguration>(config.text).enabled)throw new InvalidOperationException("Platform access must be disabled.");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,Package);
            PlayerSettings.productName="Maestro Storage Probe (diagnostic)";
            PlayerSettings.Android.bundleVersionCode=1;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.Activity;
            PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android,"MAESTRO_QUEST_DEVELOPMENT");
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            PlayerSettings.runInBackground=false;
            if(EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out XRGeneralSettingsPerBuildTarget xr)){
                var settings=xr.SettingsForBuildTarget(BuildTargetGroup.Android);
                if(settings){settings.InitManagerOnStart=false;EditorUtility.SetDirty(settings);}
                var manager=xr.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                if(manager){foreach(var loader in manager.activeLoaders.ToArray())manager.TryRemoveLoader(loader);EditorUtility.SetDirty(manager);}
                EditorUtility.SetDirty(xr);
            }
            var openXr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if(openXr){foreach(var feature in openXr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())if(feature)feature.enabled=false;EditorUtility.SetDirty(openXr);}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Diagnostic camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            const string scenePath="Assets/Maestro/StorageProbeScene.unity";EditorSceneManager.SaveScene(scene,scenePath);
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
            EditorUserBuildSettings.development=true;EditorUserBuildSettings.allowDebugging=false;EditorUserBuildSettings.connectProfiler=false;
            string output=Environment.GetEnvironmentVariable("MAESTRO_QUEST_APK");
            if(string.IsNullOrEmpty(output)||!Path.IsPathFullyQualified(output)||!output.EndsWith(".apk"))throw new InvalidOperationException("Supply diagnostic APK output.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.Development|BuildOptions.CleanBuildCache});
            File.WriteAllText(output+".build.json",JsonUtility.ToJson(new Evidence{package=Package,result=report.summary.result.ToString(),unity=Application.unityVersion,errors=report.summary.totalErrors,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Storage diagnostic build failed.");
            Debug.Log("MAESTRO_STORAGE_PROBE_APK "+output);EditorApplication.Exit(0);
        }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if(Environment.GetEnvironmentVariable("MAESTRO_ANDROID_STORAGE_PROBE")!="1")return;
            if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=Package)throw new BuildFailedException("Wrong diagnostic identity.");
            string file=Path.Combine(path,"src/main/AndroidManifest.xml");var manifest=new XmlDocument();manifest.Load(file);
            const string android="http://schemas.android.com/apk/res/android",tools="http://schemas.android.com/tools";
            manifest.DocumentElement.SetAttribute("xmlns:tools",tools);
            foreach(XmlElement node in manifest.SelectNodes("//category|//meta-data|/manifest/uses-feature").Cast<XmlElement>().ToArray()){
                string name=node.GetAttribute("name",android);
                if(name.StartsWith("com.oculus.")||name.StartsWith("com.meta.")||name.StartsWith("oculus.")||name=="com.samsung.android.vr.application.mode"||name=="android.hardware.vr.headtracking")node.ParentNode.RemoveChild(node);
            }
            foreach(string permission in new[]{"android.permission.INTERNET","android.permission.RECORD_AUDIO","android.permission.CAMERA","com.oculus.permission.USE_SCENE","com.oculus.permission.USE_ANCHOR_API","com.oculus.permission.HAND_TRACKING"}){
                foreach(XmlElement node in manifest.SelectNodes("/manifest/uses-permission").Cast<XmlElement>().Where(x=>x.GetAttribute("name",android)==permission).ToArray())node.ParentNode.RemoveChild(node);
                var entry=manifest.CreateElement("uses-permission");entry.SetAttribute("name",android,permission);entry.SetAttribute("node",tools,"remove");manifest.DocumentElement.AppendChild(entry);
            }
            ((XmlElement)manifest.SelectSingleNode("/manifest/application")).SetAttribute("extractNativeLibs",android,"true");
            manifest.Save(file);
            // These files exist only in the diagnostic Gradle project.
            string library=Environment.GetEnvironmentVariable("MAESTRO_STORAGE_FAULT_LIBRARY");
            string wrapper=Environment.GetEnvironmentVariable("MAESTRO_STORAGE_FAULT_WRAPPER");
            if(!File.Exists(library)||!File.Exists(wrapper))throw new BuildFailedException("Missing diagnostic native hook.");
            string jni=Path.Combine(path,"src/main/jniLibs/arm64-v8a"),resources=Path.Combine(path,"src/main/resources/lib/arm64-v8a");
            Directory.CreateDirectory(jni);Directory.CreateDirectory(resources);
            File.Copy(library,Path.Combine(jni,"libmaestro_storage_fault.so"),true);
            File.Copy(wrapper,Path.Combine(resources,"wrap.sh"),true);
            string launcher=Path.Combine(Directory.GetParent(path).FullName,"launcher/build.gradle");
            File.AppendAllText(launcher,"\nandroid { packaging { jniLibs { useLegacyPackaging true } } }\n");
        }
        [Serializable]sealed class Evidence{public string package,result,unity;public int errors;public ulong bytes;public double seconds;}
    }
}
#endif
