// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using Maestro.Quest.Book;

namespace Maestro.AudioEvaluation
{
    // A handle belongs to one utterance, not just an emitter or graph generation.
    // The queue, resampler and DSP receipts are the unchanged application source.
    internal sealed class PcmVoice
    {
        readonly SpeechPcmStream stream;
        int stopped;
        internal readonly float DryGain, WetGain;
        internal bool Paused => stream.IsPaused;
        internal bool Open => Volatile.Read(ref stopped) == 0;
        internal PcmVoice(double startAt, double tail, float dryGain, float wetGain)
        {
            if (!float.IsFinite(dryGain) || dryGain < 0 || dryGain > 1 || !float.IsFinite(wetGain) || wetGain < 0 ||
                wetGain > 1)
                throw new ArgumentOutOfRangeException("Invalid voice mix");
            stream = new SpeechPcmStream(startAt, tail);
            DryGain = dryGain;
            WetGain = wetGain;
        }
        internal bool Write(long sequence, short[] pcm, double now,
                            out string error) => stream.TryWrite(sequence, pcm, now, out error);
        internal SpeechPcmStream.Cursor Read(double now) => stream.Read(now);
        internal bool Render(float[] output, int rate, double time) => stream.Render(output, 1, rate, time);
        internal bool Complete(long lastSequence, out string error) => stream.TryComplete(lastSequence, out error);
        internal void SetPaused(bool value) => stream.SetPaused(value);
        internal void Close()
        {
            Interlocked.Exchange(ref stopped, 1);
            stream.Close();
        }
    }
    internal sealed partial class OwnedLifecycle
    {
        internal PcmVoice BeginPcm(string id, long commandGeneration, double startAt, double tail, float dryGain = 1,
                                   float wetGain = 1)
        {
            var graph = Active;
            if (closed || generation != commandGeneration || graph == null)
                return null;
            return graph.BeginPcm(id, startAt, tail, dryGain, wetGain);
        }
        internal bool StopPcm(string id, PcmVoice voice)
        {
            var graph = Active;
            return !closed && graph != null && graph.StopPcm(id, voice);
        }
    }
}
