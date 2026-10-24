// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>One voice's direct obstruction and sends to the owned room mixer.
    /// A source's Stop must never reset the room's shared reflection state.</summary>
    internal sealed class SpeechOcclusion : MonoBehaviour
    {
        RoomAcoustics room;
        MetaXRAudioSource source;
        internal void Bind(MetaXRAudioSource value)
        {
            source = value; room = GetComponentInParent<RoomAcoustics>();
            var audio = GetComponentInParent<RoomAudioOutput>();
            bool routed = audio && audio.Route(GetComponent<AudioSource>());
            source.ReverbSendDb = routed ? 0 : -60;
            var parameters = gameObject.AddComponent<MetaXRAudioSourceExperimentalFeatures>();
            parameters.EarlyReflectionsSendDb = routed ? 0 : -60;
            // Preserve useful speech intelligibility behind objects. Full
            // obstruction/material controls can share this same geometry later.
            parameters.OcclusionIntensity = .65f;
            parameters.UpdateParameters(); source.UpdateParameters(); Refresh();
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
