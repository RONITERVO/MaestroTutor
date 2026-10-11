// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    // Immutable per-instance settings. Gain is independently mutable, like the
    // production lease. Reflections already include their propagation distance.
    internal sealed class PcmSpatialSettings
    {
        internal readonly bool Spatial;
        internal readonly float MinDistance, MaxDistance;
        internal static readonly PcmSpatialSettings Default = new(true, 1, 15);
        internal PcmSpatialSettings(bool spatial, float minDistance, float maxDistance)
        {
            if (!float.IsFinite(minDistance) || !float.IsFinite(maxDistance) || minDistance <= 0 || maxDistance < minDistance)
                throw new ArgumentException("Invalid PCM distance settings");
            Spatial = spatial; MinDistance = minDistance; MaxDistance = maxDistance;
        }
        internal float DirectAttenuation(CoordinateSpace3 listener, CoordinateSpace3 source)
        {
            if (!Spatial) return 1;
            double x = (double)source.origin.x - listener.origin.x;
            double y = (double)source.origin.y - listener.origin.y;
            double z = (double)source.origin.z - listener.origin.z;
            double distance = Math.Sqrt(x * x + y * y + z * z);
            // Unity logarithmic rolloff at the app's Rolloff Scale of one.
            // MaxDistance is retained/validated; its behaviour is reference-tested.
            return (float)(MinDistance / Math.Max(MinDistance, distance));
        }
    }
    internal sealed partial class PreparedGraph
    {
        internal bool MatchesPcm(string id, PcmVoice voice)
        {
            int slot = FindSlot(id);
            return slot >= 0 && !IsRetired && voice?.Open == true && Volatile.Read(ref pcm[slot])?.Voice == voice;
        }
        internal bool SetPcmGain(string id, PcmVoice voice, float value)
        {
            if (!MatchesPcm(id, voice)) return false;
            voice.SetGain(value);
            return true;
        }
    }
    internal sealed partial class OwnedLifecycle
    {
        internal bool SetPcmGain(string id, PcmVoice voice, float value)
            => !closed && Active != null && Active.SetPcmGain(id, voice, value);
        internal bool SetPcmPose(string id, PcmVoice voice, CoordinateSpace3 pose)
        {
            if (closed || Active == null || !Active.MatchesPcm(id, voice)) return false;
            if (!AcousticFrame.SamePose(desiredScene.Source(voice.Slot), pose))
                UpdateScene(desiredScene.WithSource(voice.Slot, pose));
            return true;
        }
    }
}
