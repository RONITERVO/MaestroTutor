// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Runtime.CompilerServices;
using Maestro.Quest.Book;

namespace Maestro.AudioEvaluation
{
    // A handle belongs to one utterance, not just an emitter or graph generation.
    // The queue, resampler and DSP receipts are the unchanged application source.
    internal sealed class PcmVoice
    {
        static readonly ConditionalWeakTable<SpeechPcmStream, object> transferred = new();
        readonly SpeechPcmStream stream;
        internal int Slot { get; set; } = -1;
        int stopped;
        readonly double outputDelay;
        double rendererEnd = double.PositiveInfinity;
        internal readonly float DryGain, WetGain;
        internal readonly PcmSpatialSettings Settings;
        float gain = 1;
        internal float Gain => Volatile.Read(ref gain);
        internal void SetGain(float value)
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref gain, value);
        }
        internal bool Paused => stream.IsPaused;
        internal bool Open => Volatile.Read(ref stopped) == 0;
        // Transfer is permanent for this stream. Reject an alias without closing
        // its real owner; failed admission of a fresh stream always closes it.
        internal PcmVoice(SpeechPcmStream stream, double tail, float dryGain, float wetGain, PcmSpatialSettings settings, object ownership = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            lock (transferred)
            {
                if (transferred.TryGetValue(stream, out _)) throw new InvalidOperationException("PCM stream already has a renderer owner");
                if (ownership == null ? !stream.TryClaimOutput(new object()) : !stream.OutputOwnedBy(ownership))
                    throw new InvalidOperationException("PCM stream already has a renderer owner or its transfer is invalid");
                transferred.Add(stream, new object());
            }
            this.stream = stream;
            try
            {
                if (!double.IsFinite(tail) || tail < 0 || tail > 2 || !float.IsFinite(dryGain) || dryGain < 0 || dryGain > 1 ||
                    !float.IsFinite(wetGain) || wetGain < 0 || wetGain > 1)
                    throw new ArgumentOutOfRangeException("Invalid voice mix or output delay");
                var cursor = stream.Read(double.MinValue); // Ownership inspection must not advance playback.
                if (cursor.Closed || cursor.Faulted) throw new InvalidOperationException("PCM stream is unavailable");
                outputDelay = tail;
                DryGain = dryGain;
                WetGain = wetGain;
                Settings = settings ?? PcmSpatialSettings.Default;
            }
            catch { stream.Close(); throw; }
        }
        internal bool Write(long sequence, short[] pcm, double now,
                            out string error) => stream.TryWrite(sequence, pcm, now, out error);
        internal SpeechPcmStream.Cursor Read(double now) => stream.Read(now);
        internal bool Render(float[] output, int rate, double time) => stream.Render(output, 1, rate, time);
        internal bool Complete(long lastSequence, out string error) => stream.TryComplete(lastSequence, out error);
        internal void SetPaused(bool value) => stream.SetPaused(value);
        // Source receipts and downstream DSP completion have different clocks.
        // Publication occurs only after the final block passed the submission gate.
        internal bool RendererDrained(double now) => Open &&
                                                     double.IsFinite(now) && now >= Volatile.Read(ref rendererEnd);
        internal void SubmitRendererEnd(double blockEnd) => Interlocked.CompareExchange(ref rendererEnd,
                                                                                        blockEnd + outputDelay,
                                                                                        double.PositiveInfinity);
        internal void Close()
        {
            Interlocked.Exchange(ref stopped, 1);
            stream.Close();
        }
    }
    internal sealed partial class OwnedLifecycle
    {
        internal PcmVoice BeginPcm(string id, long commandGeneration, double startAt, double tail, float dryGain = 1,
                                   float wetGain = 1) => BeginPcm(id, commandGeneration, new SpeechPcmStream(startAt, tail), tail, dryGain, wetGain);
        internal PcmVoice BeginPcm(string id, long commandGeneration, SpeechPcmStream stream, double outputDelay,
                                   float dryGain = 1, float wetGain = 1, PcmSpatialSettings settings = null, object ownership = null)
        {
            var voice = new PcmVoice(stream, outputDelay, dryGain, wetGain, settings, ownership);
            var graph = Active;
            if (closed || generation != commandGeneration || graph == null)
            { voice.Close(); return null; }
            try { return graph.BindPcm(id, voice); }
            catch { voice.Close(); throw; }
        }
        internal bool FinishPcm(string id, PcmVoice voice, double now, out SpeechPcmStream.Cursor receipt)
        {
            receipt = default;
            var graph = Active;
            return !closed && graph != null && graph.FinishPcm(id, voice, now, out receipt);
        }
        internal bool StopPcm(string id, PcmVoice voice)
        {
            var graph = Active;
            return !closed && graph != null && graph.StopPcm(id, voice);
        }
    }
}
