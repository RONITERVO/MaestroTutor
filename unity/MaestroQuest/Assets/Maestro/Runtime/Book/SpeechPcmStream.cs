// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;

namespace Maestro.Quest.Book
{
    /// <summary>One bounded, mono 24 kHz stream. The audio thread performs no
    /// allocations or Unity object calls. Render receipts describe actual DSP
    /// blocks, including resampling phase, rather than assumed clip playback.</summary>
    internal sealed class SpeechPcmStream
    {
        internal const int Rate = 24000, Capacity = Rate * 8, MaxChunk = 4800;
        const int ReceiptCapacity = 2048;
        struct Receipt { public double Start, End, First; public long Last; }
        internal readonly struct Cursor
        {
            public readonly long Sequence, Submitted, Played;
            public readonly bool Faulted;
            public Cursor(long sequence, long submitted, long played, bool faulted)
            { Sequence = sequence; Submitted = submitted; Played = played; Faulted = faulted; }
        }
        readonly object sync = new();
        readonly float[] samples = new float[Capacity];
        readonly Receipt[] receipts = new Receipt[ReceiptCapacity];
        readonly double startAt, tail;
        long sequence, written, consumed, played;
        double phase, renderedUntil = double.NegativeInfinity;
        int receiptHead, receiptCount;
        bool closed, faulted;
        public SpeechPcmStream(double startAt, double tail)
        {
            if (!double.IsFinite(startAt) || !double.IsFinite(tail) || tail < 0 || tail > 2)
                throw new ArgumentException("Invalid speech timing.");
            this.startAt = startAt; this.tail = tail;
        }
        public bool TryWrite(long nextSequence, short[] pcm, double now, out string error)
        {
            lock (sync)
            {
                error = null;
                if (closed || faulted) { error = "Speech output was stopped"; return false; }
                if (nextSequence != sequence + 1) { error = "Speech chunk is duplicate or out of order"; return false; }
                if (pcm == null || pcm.Length < 1 || pcm.Length > MaxChunk) { error = "Speech chunk is invalid"; return false; }
                AdvancePlayed(now - tail);
                if (written - played + pcm.Length > Capacity) { error = "Speech output buffer is full"; return false; }
                for (int i = 0; i < pcm.Length; i++) samples[(int)((written + i) % Capacity)] = pcm[i] / 32768f;
                written += pcm.Length; sequence = nextSequence;
                return true;
            }
        }
        // Called by OnAudioFilterRead. Also used by deterministic tests of the
        // exact render path; advancing a test clock alone cannot play samples.
        public void Render(float[] data, int channels, int outputRate, double dspTime)
        {
            Array.Clear(data, 0, data.Length);
            lock (sync)
            {
                if (closed || faulted) return;
                if (channels < 1 || channels > 8 || data.Length % channels != 0
                    || outputRate < Rate || outputRate > 192000 || !double.IsFinite(dspTime))
                { faulted = true; return; }
                int frames = data.Length / channels;
                if (frames == 0 || dspTime < renderedUntil - 1e-7) return;
                renderedUntil = dspTime + (double)frames / outputRate;
                AdvancePlayed(dspTime - tail);
                if (written == consumed || renderedUntil <= startAt) return;
                if (receiptCount == ReceiptCapacity) { faulted = true; return; }
                int firstFrame = Math.Clamp((int)Math.Ceiling(Math.Max(0, startAt - dspTime) * outputRate), 0, frames);
                double firstPosition = consumed + phase, step = (double)Rate / outputRate;
                int frame = firstFrame;
                for (; frame < frames && consumed < written; frame++)
                {
                    float a = samples[(int)(consumed % Capacity)];
                    float b = consumed + 1 < written ? samples[(int)((consumed + 1) % Capacity)] : a;
                    float value = a + (b - a) * (float)phase;
                    for (int channel = 0; channel < channels; channel++) data[frame * channels + channel] = value;
                    phase += step;
                    int advance = (int)(phase + 1e-10);
                    phase -= advance;
                    consumed += advance;
                    if (consumed >= written) { consumed = written; phase = 0; }
                }
                if (frame > firstFrame)
                {
                    receipts[(receiptHead + receiptCount) % ReceiptCapacity] = new Receipt {
                        Start = dspTime + (double)firstFrame / outputRate,
                        End = dspTime + (double)frame / outputRate, First = firstPosition, Last = consumed
                    };
                    receiptCount++;
                }
            }
        }
        void AdvancePlayed(double audibleTime)
        {
            while (receiptCount > 0)
            {
                var receipt = receipts[receiptHead];
                if (audibleTime < receipt.Start) break;
                played = Math.Max(played, Math.Min(receipt.Last,
                    (long)Math.Floor(receipt.First + Math.Max(0, audibleTime - receipt.Start) * Rate + 1e-7)));
                if (audibleTime < receipt.End) break;
                played = Math.Max(played, receipt.Last);
                receiptHead = (receiptHead + 1) % ReceiptCapacity; receiptCount--;
            }
        }
        public Cursor Read(double now)
        {
            lock (sync) { AdvancePlayed(now - tail); return new Cursor(sequence, written, played, faulted); }
        }
        public void Close()
        {
            lock (sync) { closed = true; sequence = written = consumed = played = 0; phase = 0; receiptHead = receiptCount = 0; }
        }
    }
}
