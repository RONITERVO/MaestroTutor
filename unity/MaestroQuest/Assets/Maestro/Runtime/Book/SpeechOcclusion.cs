// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Direct sound obstruction only. Reflections have no mixer route
    /// until their completion/cancellation and microphone tails are measured.</summary>
    internal sealed class SpeechOcclusion : MonoBehaviour
    {
        RoomAcoustics room;
        MetaXRAudioSource source;
        internal void Bind(MetaXRAudioSource value)
        {
            source = value; room = GetComponentInParent<RoomAcoustics>();
            source.ReverbSendDb = -60;
            var parameters = gameObject.AddComponent<MetaXRAudioSourceExperimentalFeatures>();
            parameters.EarlyReflectionsSendDb = -60;
            // Preserve useful speech intelligibility behind objects. Full
            // obstruction/material controls can share this same geometry later.
            parameters.OcclusionIntensity = .65f;
            parameters.UpdateParameters(); Refresh();
        }
        internal void Refresh()
        {
            if (!source) return;
            bool enabled = room && room.Ready;
            if (source.EnableAcoustics == enabled) return;
            source.EnableAcoustics = enabled; source.UpdateParameters();
        }
        void LateUpdate() => Refresh();
    }
}
