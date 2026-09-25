// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Maestro.Quest.Editor
{
    // AARs do not carry their Maven dependencies into a Unity build.
    public sealed class QuestAndroidGradle : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Unity defaults this Activity to software View rendering. The offscreen
            // WebView draws into a hardware Canvas and needs an accelerated window.
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            var manifest = new XmlDocument(); manifest.Load(manifestPath);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (XmlElement activity in manifest.SelectNodes("/manifest/application/activity"))
                if (activity.GetAttribute("name", android) == "com.unity3d.player.UnityPlayerGameActivity")
                    activity.SetAttribute("hardwareAccelerated", android, "true");
            // Meta's optional editor agent bundles screen-capture components.
            // This app uses object-motion recording and does not enable that agent.
            const string tools = "http://schemas.android.com/tools";
            manifest.DocumentElement.SetAttribute("xmlns:tools", tools);
            void RemoveLibraryEntry(XmlNode parent, string element, string name)
            {
                var node = manifest.CreateElement(element);
                node.SetAttribute("name",android,name); node.SetAttribute("node",tools,"remove"); parent.AppendChild(node);
            }
            RemoveLibraryEntry(manifest.DocumentElement,"uses-permission","android.permission.FOREGROUND_SERVICE");
            RemoveLibraryEntry(manifest.DocumentElement,"uses-permission","android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION");
            var app = manifest.SelectSingleNode("/manifest/application");
            RemoveLibraryEntry(app,"activity","com.meta.agenticxr.AgenticMediaProjectionActivity");
            RemoveLibraryEntry(app,"service","com.meta.agenticxr.AgenticMediaProjectionService");
            manifest.Save(manifestPath);
            var gradle = Path.Combine(path, "build.gradle");
            var source = File.ReadAllText(gradle);
            const string marker = "// Maestro book browser dependencies";
            if (!source.Contains(marker))
                File.AppendAllText(gradle, "\n" + marker + "\ndependencies {\n" +
                    "    implementation 'androidx.webkit:webkit:1.14.0'\n" +
                    "    implementation 'androidx.annotation:annotation:1.9.1'\n}\n");
        }
    }
}
#endif
