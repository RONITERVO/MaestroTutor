// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>A single source filter retained across provider packet boundaries.
    /// Spatialization runs after this filter, so it processes the generated PCM.</summary>
    [RequireComponent(typeof(AudioSource))]
    internal sealed class SpeechPcmFilter : MonoBehaviour
    {
        SpeechPcmStream stream;
        int outputRate;
        internal void Bind(SpeechPcmStream value, int rate) { outputRate = rate; Volatile.Write(ref stream, value); }
        void OnAudioFilterRead(float[] data, int channels)
        {
            var current = Volatile.Read(ref stream);
            if (current == null) { Array.Clear(data, 0, data.Length); return; }
            current.Render(data, channels, outputRate, AudioSettings.dspTime);
        }
        internal void Stop() => Interlocked.Exchange(ref stream, null)?.Close();
        void OnDisable() => Stop();
    }
}
