// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;

namespace Maestro.Quest.Book
{
    /// <summary>Ephemeral final-mix activity, independent of PCM completion.
    /// No samples are retained. The audio callback and main thread share only
    /// timestamps and health, using a monotonic clock that survives DSP pauses.</summary>
    internal sealed class AcousticOutputMonitor
    {
        internal const double QuietSeconds = .15, FreshSeconds = .5, FailureSeconds = 1.5;
        // -80 dBFS peak, below typical speech but above floating-point reverb
        // residue. This is a digital activity threshold, not an acoustic SPL.
        const float ActivityThreshold = .0001f;
        readonly object sync = new();
        bool armed, observed, valid;
        double armedAt, blockAt, activeUntil;
        internal static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        internal void Arm(double now)
        {
            lock (sync)
            {
                if (!double.IsFinite(now)) { armed = true; valid = false; return; }
                if (!armed) { armed = true; armedAt = now; }
                activeUntil = Math.Max(activeUntil, now);
            }
        }
        internal void Observe(float[] data, int channels, int rate, double now)
        {
            bool healthy = data != null && channels > 0 && channels <= 8 && rate >= 8000 && rate <= 192000
                && data.Length > 0 && data.Length % channels == 0 && double.IsFinite(now);
            bool active = false;
            if (healthy)
                foreach (float sample in data)
                {
                    if (!float.IsFinite(sample)) { healthy = false; break; }
                    if (Math.Abs(sample) >= ActivityThreshold) active = true;
                }
            lock (sync)
            {
                if (!healthy || observed && now < blockAt) { valid = false; return; }
                observed = valid = true; blockAt = now;
                if (active) activeUntil = Math.Max(activeUntil, now + (double)data.Length / channels / rate);
            }
        }
        internal (bool Suppressed, bool Failed) Read(double now, double deviceTail)
        {
            lock (sync)
            {
                if (!armed) return (false, false);
                bool clock = double.IsFinite(now) && now >= armedAt && (!observed || now >= blockAt);
                double last = observed ? Math.Max(armedAt, blockAt) : armedAt;
                bool fresh = clock && observed && valid && now - blockAt <= FreshSeconds;
                bool failed = !clock || now - last > FailureSeconds;
                bool tail = !double.IsFinite(deviceTail) || deviceTail < 0 || deviceTail > 2;
                return (!fresh || tail || now < activeUntil + QuietSeconds + deviceTail, failed || tail);
            }
        }
    }
}
