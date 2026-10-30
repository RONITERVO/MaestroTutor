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
            public readonly bool Faulted, Paused, InputComplete, Closed;
            // Source completion excludes any renderer-owned spatial/filter tail.
            public bool SourceDrained => InputComplete && !Closed && !Faulted && Played == Submitted;
            public Cursor(long sequence, long submitted, long played, bool faulted,
                bool paused, bool inputComplete, bool closed)
            { Sequence = sequence; Submitted = submitted; Played = played; Faulted = faulted;
                Paused = paused; InputComplete = inputComplete; Closed = closed; }
        }
        readonly object sync = new();
        object outputOwner;
        // Permanent ownership for this stream. Claiming an alias must never
        // close or consume another renderer's input, including across adapters.
        internal bool TryClaimOutput(object owner)
        {
            if(owner==null)throw new ArgumentNullException(nameof(owner));
            lock(sync){if(outputOwner!=null)return false;outputOwner=owner;return true;}
        }
        internal bool OutputOwnedBy(object owner){lock(sync)return owner!=null&&ReferenceEquals(outputOwner,owner);}

        readonly float[] samples = new float[Capacity];
        readonly Receipt[] receipts = new Receipt[ReceiptCapacity];
        readonly double startAt, tail;
        long sequence, written, consumed, played;
        double phase, renderedUntil = double.NegativeInfinity;
        int receiptHead, receiptCount;
        bool closed, faulted, paused, inputComplete;
        public SpeechPcmStream(double startAt, double tail)
        {
            if (!double.IsFinite(startAt) || !double.IsFinite(tail) || tail < 0 || tail > 2)
                throw new ArgumentException("Invalid speech timing.");
            this.startAt = startAt; this.tail = tail;
        }
        public bool TryWrite(long nextSequence, short[] pcm, double now, out string error)
            => TryWrite(nextSequence, pcm, pcm?.Length ?? 0, now, out error);
        // The caller may reuse a fixed producer buffer after this synchronous copy.
        public bool TryWrite(long nextSequence, short[] pcm, int count, double now, out string error)
        {
            lock (sync)
            {
                error = null;
                if (closed || faulted) { error = "Speech output was stopped"; return false; }
                if (inputComplete) { error = "Speech input was completed"; return false; }
                if (nextSequence != sequence + 1) { error = "Speech chunk is duplicate or out of order"; return false; }
                if (pcm == null || count < 1 || count > pcm.Length || count > MaxChunk) { error = "Speech chunk is invalid"; return false; }
                AdvancePlayed(now - tail);
                if (written - played + count > Capacity) { error = "Speech output buffer is full"; return false; }
                for (int i = 0; i < count; i++) samples[(int)((written + i) % Capacity)] = pcm[i] / 32768f;
                written += count; sequence = nextSequence;
                return true;
            }
        }
        // Explicit end of input, never inferred from a temporary network underrun.
        // The exact accepted sequence fences a premature/stale producer completion.
        // Completing input does not discard queued PCM or terminate native tails.
        public bool TryComplete(long lastSequence, out string error)
        {
            lock (sync)
            {
                error = null;
                if (closed || faulted) { error = "Speech output was stopped"; return false; }
                if (lastSequence != sequence) { error = "Speech completion sequence does not match"; return false; }
                inputComplete = true;
                return true;
            }
        }
        // Called by OnAudioFilterRead. Also used by deterministic tests of the
        // exact render path; advancing a test clock alone cannot play samples.
        // True admits this block (including an underrun tail); false means paused,
        // stopped, invalid or duplicate. A later Pause cannot retract consumed PCM.
        public bool Render(float[] data, int channels, int outputRate, double dspTime)
        {
            Array.Clear(data, 0, data.Length);
            lock (sync)
            {
                if (closed || faulted || paused) return false;
                if (channels < 1 || channels > 8 || data.Length % channels != 0
                    || outputRate < Rate || outputRate > 192000 || !double.IsFinite(dspTime))
                { faulted = true; return false; }
                int frames = data.Length / channels;
                if (frames == 0 || dspTime < renderedUntil - 1e-7) return false;
                renderedUntil = dspTime + (double)frames / outputRate;
                AdvancePlayed(dspTime - tail);
                if (written == consumed || renderedUntil <= startAt) return true;
                if (receiptCount == ReceiptCapacity) { faulted = true; return false; }
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
                return true;
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
            lock (sync) { AdvancePlayed(now - tail); return new Cursor(sequence, written, played, faulted, paused, inputComplete, closed); }
        }
        // Procedural filters can still be called while their AudioSource is paused.
        // Fence consumption on the audio thread, retaining queued frames and phase.
        public bool IsPaused { get { lock(sync) { return paused; } } }
        public void SetPaused(bool value) { lock(sync) { if (!closed && !faulted) paused=value; } }
        public void Close()
        {
            lock (sync) { closed = true; paused = false; sequence = written = consumed = played = 0; phase = 0; receiptHead = receiptCount = 0; }
        }
    }
}
