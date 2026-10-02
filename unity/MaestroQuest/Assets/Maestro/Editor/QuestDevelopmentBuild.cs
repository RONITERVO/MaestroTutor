// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    public static class QuestDevelopmentBuild
    {
        public static void Build()
        {
            QuestProjectSetup.Configure();
            AndroidExternalToolsSettings.sdkRootPath = RequiredDirectory("MAESTRO_ANDROID_SDK");
            AndroidExternalToolsSettings.ndkRootPath = RequiredDirectory("MAESTRO_ANDROID_NDK");
            AndroidExternalToolsSettings.jdkRootPath = RequiredDirectory("MAESTRO_ANDROID_JDK");
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android, "MAESTRO_QUEST_DEVELOPMENT");
            if (!File.Exists("Assets/Plugins/Android/MaestroBookBrowser.aar")) throw new InvalidOperationException("Build and stage the native browser AAR first.");
            if (!File.Exists("Assets/StreamingAssets/maestro-web/index.html")) throw new InvalidOperationException("Build and stage the shared Maestro web application first.");
            var output = Environment.GetEnvironmentVariable("MAESTRO_QUEST_APK");
            if (string.IsNullOrEmpty(output) || !Path.IsPathFullyQualified(output) || !output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("MAESTRO_QUEST_APK must be an absolute APK filename.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { QuestProjectSetup.ScenePath }, locationPathName = output,
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            var summary = report.summary;
            File.WriteAllText(Path.ChangeExtension(output, ".build.json"), JsonUtility.ToJson(new Evidence {
                result = summary.result.ToString(), unity = Application.unityVersion,
                package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android),
                size = summary.totalSize, seconds = summary.totalTime.TotalSeconds,
                errors = summary.totalErrors, warnings = summary.totalWarnings,
                developmentOnly = true
            }, true));
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Quest development build failed: " + summary.result);
            Debug.Log("MAESTRO_DEVELOPMENT_APK " + output);
            // Finish this batch explicitly after the successful report is saved.
            // Automatic -quit can stall after a release/development target switch.
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static string RequiredDirectory(string variable)
        {
            var path = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) throw new InvalidOperationException("Set " + variable + " to an installed toolchain directory.");
            return Path.GetFullPath(path);
        }

        [Serializable] sealed class Evidence
        {
            public string result, unity, package;
            public ulong size;
            public double seconds;
            public int errors, warnings;
            public bool developmentOnly;
        }
    }
}
#endif
