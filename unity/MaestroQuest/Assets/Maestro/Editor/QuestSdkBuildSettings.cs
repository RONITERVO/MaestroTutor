// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Maestro.Quest.Editor
{
    /// <summary>Keep Meta's optional editor agent disconnected in distributable builds.</summary>
    public sealed class QuestSdkBuildSettings : IPreprocessBuildWithReport
    {
        // Meta 207 injects an editor token at order 1, even when the agent is off.
        public int callbackOrder => 1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/DevAgentSettings.asset");
            if (!asset) throw new BuildFailedException("Meta developer-agent settings were not found; check the pinned SDK build order.");
            var settings = new SerializedObject(asset);
            Required(settings,"enabled").boolValue = false;
            Required(settings,"serverAddress").stringValue = string.Empty;
            Required(settings,"accessToken").stringValue = string.Empty;
            Required(settings,"witClientAccessToken").stringValue = string.Empty;
            Required(settings,"useVoiceSdkForInput").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log("MAESTRO_META_AGENT_DISABLED: editor connection credentials removed from build settings.");
        }

        static SerializedProperty Required(SerializedObject settings, string name) =>
            settings.FindProperty(name) ?? throw new BuildFailedException("Pinned Meta settings schema changed: " + name);
    }
}
