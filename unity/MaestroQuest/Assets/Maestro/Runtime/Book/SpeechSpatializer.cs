// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Book
{
    internal static class SpeechSpatializer
    {
        internal const string PluginName = "Meta XR Audio";

        internal static void Configure(AudioSource source)
        {
            if (AudioSettings.GetSpatializerPluginName() != PluginName)
                throw new InvalidOperationException("Maestro's spatial audio renderer is not configured.");
            // A configured name alone does not prove the native library loaded.
            int result = MetaXRAcousticNativeInterface.UnityNativeInterface.ovrAudio_GetPluginContext(out var context);
            if (result != 0 || context == IntPtr.Zero)
                throw new InvalidOperationException("Maestro's spatial audio renderer could not start.");
            source.spatializePostEffects = true;
            var spatializer = source.gameObject.AddComponent<MetaXRAudioSource>();
            spatializer.EnableSpatialization = true;
            // Room acoustics require owned geometry/materials and measured echo
            // tails. Do not apply a synthetic default room to the user's room.
            spatializer.EnableAcoustics = false;
            spatializer.GainBoostDb = 0;
            spatializer.UpdateParameters();
        }
    }
}
