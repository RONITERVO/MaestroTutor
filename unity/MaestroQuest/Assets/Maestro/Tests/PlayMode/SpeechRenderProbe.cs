// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    // Test-only filter after the real speech filter. Capture before the muted
    // listener; do not substitute the renderer or publish microphone audio.
    public sealed class SpeechRenderProbe : MonoBehaviour
    {
        readonly object sync = new();
        int blocks, channelCount; float peak, step, previous;
        void OnAudioFilterRead(float[] data,int channels)
        {
            lock(sync)
            {
                blocks++; channelCount=channels;
                for(int i=0;i<data.Length;i+=channels)
                {
                    peak=Math.Max(peak,Math.Abs(data[i])); step=Math.Max(step,Math.Abs(data[i]-previous)); previous=data[i];
                }
            }
        }
        internal (int Blocks,int Channels,float Peak,float Step) Read()
        { lock(sync) return (blocks,channelCount,peak,step); }
    }
}
