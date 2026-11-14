// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    // Capture the real stereo listener mix AFTER the source spatializer, then
    // mute it. No microphone or user/provider audio participates in this test.
    public sealed class SpeechListenerProbe : MonoBehaviour
    {
        readonly object sync = new();
        double left, right;
        int blocks, channelCount;
        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (sync)
            {
                blocks++; channelCount = channels;
                if (channels == 2)
                    for (int i = 0; i < data.Length; i += 2)
                    { left += data[i] * data[i]; right += data[i+1] * data[i+1]; }
            }
            Array.Clear(data, 0, data.Length);
        }
        internal (int Blocks, int Channels, double Left, double Right) Read()
        { lock (sync) return (blocks, channelCount, left, right); }
        internal void Reset() { lock (sync) { blocks = channelCount = 0; left = right = 0; } }
    }
}
