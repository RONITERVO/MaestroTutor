// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Additional DSP declarations from the pinned Steam Audio 4.8.1 phonon.h.
// Evaluation only; no vendor library is distributed by this project.
using System;
using System.Runtime.InteropServices;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    internal static class NativeDsp
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Buffer
        {
            public int channels, samples;
            public IntPtr data;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct ReflectionSettings
        {
            public ReflectionEffectType type;
            public int irSize, channels;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Layout
        {
            public int type, speakers;
            public IntPtr directions;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct DecodeSettings
        {
            public Layout layout;
            public IntPtr hrtf;
            public int order;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct DecodeParams
        {
            public int order;
            public IntPtr hrtf;
            public CoordinateSpace3 orientation;
            public Bool binaural;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BinauralSettings
        {
            public IntPtr hrtf;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BinauralParams
        {
            public SteamAudio.Vector3 direction;
            public int interpolation;
            public float spatialBlend;
            public IntPtr hrtf, peakDelays;
        }
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern Error iplBinauralEffectCreate(IntPtr context, ref SteamAudio.AudioSettings audio,
                                                             ref BinauralSettings settings, out IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplBinauralEffectApply(IntPtr effect, ref BinauralParams parameters,
                                                          ref Buffer input, ref Buffer output);
        internal const int TailComplete = 1;
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplBinauralEffectGetTail(IntPtr effect, ref Buffer output);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplReflectionEffectGetTail(IntPtr effect, ref Buffer output, IntPtr mixer);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplAmbisonicsDecodeEffectGetTail(IntPtr effect, ref Buffer output);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplBinauralEffectReset(IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplBinauralEffectRelease(ref IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplReflectionEffectReset(IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplAmbisonicsDecodeEffectReset(IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern Error iplAudioBufferAllocate(IntPtr context, int channels, int samples,
                                                            ref Buffer buffer);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplAudioBufferFree(IntPtr context, ref Buffer buffer);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern Error iplReflectionEffectCreate(IntPtr context, ref SteamAudio.AudioSettings audio,
                                                               ref ReflectionSettings settings, out IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplReflectionEffectApply(IntPtr effect, ref ReflectionEffectParams parameters,
                                                            ref Buffer input, ref Buffer output, IntPtr mixer);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplReflectionEffectRelease(ref IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern Error iplAmbisonicsDecodeEffectCreate(IntPtr context, ref SteamAudio.AudioSettings audio,
                                                                     ref DecodeSettings settings, out IntPtr effect);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern int iplAmbisonicsDecodeEffectApply(IntPtr effect, ref DecodeParams parameters,
                                                                  ref Buffer input, ref Buffer output);
        [DllImport("phonon", CallingConvention = CallingConvention.Winapi)]
        internal static extern void iplAmbisonicsDecodeEffectRelease(ref IntPtr effect);
    }
}
