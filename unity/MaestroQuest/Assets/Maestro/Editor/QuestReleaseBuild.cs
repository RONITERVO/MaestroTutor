// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
namespace Maestro.Quest.Editor
{
    public sealed class QuestReleaseGuard:IPreprocessBuildWithReport
    {
        public int callbackOrder=>1100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.Android||(report.summary.options&BuildOptions.Development)!=0)return;
            ValidateCurrent();
        }
        public static void ValidateCurrent()
        {
            if(!File.Exists(".maestro-build-mirror.json"))throw new BuildFailedException("Quest releases must be prepared in an owned build mirror.");
            var path=Environment.GetEnvironmentVariable(QuestReleaseInputs.ProfileVariable);
            var profile=QuestReleaseInputs.ValidateProfile(QuestReleaseInputs.Read(path));
            QuestReleaseInputs.VerifyWeb(path,"Assets/StreamingAssets/maestro-web");
            var native=JsonUtility.FromJson<QuestPlatformConfiguration>(File.ReadAllText("Assets/Maestro/Resources/QuestPlatform.json"));
            if(native==null||native.version!=1||!native.enabled||native.appId!=(string)profile["metaAppId"])
                throw new BuildFailedException("Quest release Meta identity does not match the public profile.");
            if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=(string)profile["package"]||PlayerSettings.bundleVersion!=(string)profile["versionName"]||PlayerSettings.Android.bundleVersionCode!=(int)profile["versionCode"])
                throw new BuildFailedException("Quest release package/version does not match the public profile.");
            if(EditorUserBuildSettings.development||EditorUserBuildSettings.allowDebugging||EditorUserBuildSettings.connectProfiler||PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Contains("MAESTRO_QUEST_DEVELOPMENT"))
                throw new BuildFailedException("Quest release still has development switches enabled.");
            if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)!=ScriptingImplementation.IL2CPP||PlayerSettings.Android.targetArchitectures!=AndroidArchitecture.ARM64||(int)PlayerSettings.Android.minSdkVersion!=32||(int)PlayerSettings.Android.targetSdkVersion!=34)
                throw new BuildFailedException("Quest release toolchain settings changed; review the pinned release policy.");
            if(PlayerSettings.Android.useCustomKeystore)throw new BuildFailedException("Release keys must stay outside Unity; the packaging tool signs the verified intermediate afterwards.");
            if(!File.Exists("Assets/Plugins/Android/MaestroBookBrowser.aar"))throw new BuildFailedException("Missing native book browser.");
        }
    }
    public static class QuestReleaseBuild
    {
#if UNITY_ANDROID
        public static void Build()
        {
            var profilePath=Environment.GetEnvironmentVariable(QuestReleaseInputs.ProfileVariable);
            var profile=QuestReleaseInputs.ValidateProfile(QuestReleaseInputs.Read(profilePath));
            QuestReleaseInputs.VerifyWeb(profilePath,"Assets/StreamingAssets/maestro-web");
            if(!File.Exists(".maestro-build-mirror.json"))throw new BuildFailedException("Use an owned build mirror.");
            const string platformPath="Assets/Maestro/Resources/QuestPlatform.json";
            byte[] original=File.ReadAllBytes(platformPath);
            try {
                QuestProjectSetup.Configure();
                AndroidExternalToolsSettings.sdkRootPath=Tools("MAESTRO_ANDROID_SDK");
                AndroidExternalToolsSettings.ndkRootPath=Tools("MAESTRO_ANDROID_NDK");
                AndroidExternalToolsSettings.jdkRootPath=Tools("MAESTRO_ANDROID_JDK");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,(string)profile["package"]);
                PlayerSettings.bundleVersion=(string)profile["versionName"];PlayerSettings.Android.bundleVersionCode=(int)profile["versionCode"];
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android,string.Empty);
                PlayerSettings.Android.useCustomKeystore=false;
                PlayerSettings.Android.keystorePass=string.Empty;PlayerSettings.Android.keyaliasPass=string.Empty;
                EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
                EditorUserBuildSettings.development=false;EditorUserBuildSettings.allowDebugging=false;EditorUserBuildSettings.connectProfiler=false;
                File.WriteAllText(platformPath,new JObject { ["version"]=1,["enabled"]=true,["appId"]=profile["metaAppId"].DeepClone() }.ToString());
                AssetDatabase.ImportAsset(platformPath,ImportAssetOptions.ForceUpdate);
                QuestReleaseGuard.ValidateCurrent();
                var output=Environment.GetEnvironmentVariable("MAESTRO_QUEST_APK");
                if(string.IsNullOrEmpty(output)||!Path.IsPathFullyQualified(output)||!output.EndsWith(".apk",StringComparison.OrdinalIgnoreCase))throw new BuildFailedException("Set the intermediate APK output path.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{QuestProjectSetup.ScenePath},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.None });
                if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Quest release intermediate build failed.");
                File.WriteAllText(Path.ChangeExtension(output,".build.json"),new JObject { ["result"]="Succeeded",["developmentOnly"]=false,["releaseSigned"]=false,["profileSha256"]=QuestReleaseInputs.Hash(File.ReadAllBytes(profilePath)),["package"]=profile["package"].DeepClone(),["versionCode"]=profile["versionCode"].DeepClone() }.ToString());
                Debug.Log("MAESTRO_RELEASE_INTERMEDIATE_APK "+output);
            } finally {
                File.WriteAllBytes(platformPath,original);AssetDatabase.ImportAsset(platformPath,ImportAssetOptions.ForceUpdate);
                PlayerSettings.Android.keystorePass=string.Empty;PlayerSettings.Android.keyaliasPass=string.Empty;
            }
        }
        static string Tools(string name) { var value=Environment.GetEnvironmentVariable(name);if(!Directory.Exists(value))throw new BuildFailedException("Missing toolchain setting: "+name);return Path.GetFullPath(value); }
#endif
    }
}
