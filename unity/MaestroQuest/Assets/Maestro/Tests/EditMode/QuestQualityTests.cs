// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class QuestQualityTests
    {
        [Test]
        public void ConfigurationRepairsInactiveProfilesAndPreservesTheSelectedLevel()
        {
            int previous = QualitySettings.GetQualityLevel();
            var saved = new (int aa, int sync, ShadowQuality shadows, ShadowResolution resolution, float distance, int lights)[QualitySettings.names.Length];
            QualitySettings.ForEach((i, _) => saved[i] = (QualitySettings.antiAliasing, QualitySettings.vSyncCount, QualitySettings.shadows, QualitySettings.shadowResolution, QualitySettings.shadowDistance, QualitySettings.pixelLightCount));
            try {
                QualitySettings.ForEach(() => {
                    QualitySettings.antiAliasing = 0; QualitySettings.vSyncCount = 1;
                    QualitySettings.shadows = ShadowQuality.Disable; QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 1; QualitySettings.pixelLightCount = 0;
                });
                QualitySettings.SetQualityLevel(0, false);
                Editor.QuestProjectSetup.ConfigureQuality();
                Assert.AreEqual(0, QualitySettings.GetQualityLevel());
                QualitySettings.ForEach((_, name) => {
                    Assert.AreEqual(4, QualitySettings.antiAliasing, name);
                    Assert.AreEqual(0, QualitySettings.vSyncCount, name);
                    Assert.AreEqual(ShadowQuality.HardOnly, QualitySettings.shadows, name);
                    Assert.AreEqual(ShadowResolution.Medium, QualitySettings.shadowResolution, name);
                    Assert.AreEqual(20, QualitySettings.shadowDistance, name);
                    Assert.AreEqual(4, QualitySettings.pixelLightCount, name);
                });
            } finally {
                QualitySettings.ForEach((i, _) => {
                    var value = saved[i]; QualitySettings.antiAliasing = value.aa; QualitySettings.vSyncCount = value.sync;
                    QualitySettings.shadows = value.shadows; QualitySettings.shadowResolution = value.resolution;
                    QualitySettings.shadowDistance = value.distance; QualitySettings.pixelLightCount = value.lights;
                });
                QualitySettings.SetQualityLevel(previous, false);
            }
        }
    }
}
