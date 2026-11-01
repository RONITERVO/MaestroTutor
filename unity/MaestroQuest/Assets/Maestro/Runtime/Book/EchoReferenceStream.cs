// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;

namespace Maestro.Quest.Book
{
    internal enum EchoReferenceRead { Waiting, Frame, Gap, Closed }
    internal enum EchoReferenceGap { None, Format, Samples, Clock, Overflow, Stale }

    /// <summary>A single-consumer, half-second render reference. This is not an
    /// echo canceller. The capture owner must align it with its microphone clock
    /// and reset cancellation on gaps or closure. PCM is ephemeral, never saved.</summary>
    internal sealed class EchoReferenceStream : IDisposable
    {
        internal readonly struct Frame
        {
            internal readonly long Epoch, Sequence;
            internal readonly double RenderTime, ObservedRenderTime, ObservedAt;
            internal readonly EchoReferenceGap Gap;
            internal Frame(long epoch, long sequence, double render, double observedRender, double observedAt, EchoReferenceGap gap)
            { Epoch=epoch; Sequence=sequence; RenderTime=render; ObservedRenderTime=observedRender; ObservedAt=observedAt; Gap=gap; }
        }
        internal const double FreshSeconds = .5;
        readonly object sync = new();
        readonly float[] samples;
        readonly Func<double> clock;
        internal int Rate { get; }
        internal int Channels { get; }
        internal int FrameSamples { get; }
        internal int CapacitySamples => samples.Length;
        internal bool Closed { get { lock(sync) return closed; } }
        int head, count;
        long epoch=1, sequence;
        double headTime, nextTime, observedTime, observedAt;
        bool closed, observed;
        EchoReferenceGap pendingGap;

        internal EchoReferenceStream(int rate, int channels, Func<double> readClock = null)
        {
            if(rate<8000 || rate>192000 || rate%100!=0 || channels<1 || channels>2)
                throw new ArgumentOutOfRangeException(nameof(rate), "Reference requires mono/stereo and whole 10 ms frames at 8–192 kHz");
            Rate=rate; Channels=channels; FrameSamples=rate/100*channels;
            samples=new float[rate/2*channels]; clock=readClock ?? (()=>AcousticOutputMonitor.Now);
        }
        // Borrow the callback's array only for this call. Neither clip amplitude
        // nor channel order is changed. Device latency is not inferred here.
        internal void Observe(float[] data, int channels, double dspTime, double now)
        {
            lock(sync)
            {
                if(closed) return;
                if(data==null || channels!=Channels || data.Length==0 || data.Length%Channels!=0 || data.Length>samples.Length)
                { Discontinuity(EchoReferenceGap.Format); return; }
                if(!double.IsFinite(dspTime) || dspTime<0 || !double.IsFinite(now) ||
                    observed && (now<observedAt || now-observedAt>FreshSeconds))
                { Discontinuity(EchoReferenceGap.Clock); return; }
                foreach(float sample in data)
                    if(!float.IsFinite(sample)) { Discontinuity(EchoReferenceGap.Samples); return; }
                // Never stitch two unrelated render intervals into a 10 ms frame.
                // Allow floating-point clock error, not a missing sample.
                if(observed && Math.Abs(dspTime-nextTime)>.5/Rate) Discontinuity(EchoReferenceGap.Clock);
                if(count+data.Length>samples.Length) Discontinuity(EchoReferenceGap.Overflow);
                if(count==0) headTime=dspTime;
                int tail=(head+count)%samples.Length, first=Math.Min(data.Length,samples.Length-tail);
                Array.Copy(data,0,samples,tail,first);
                Array.Copy(data,first,samples,0,data.Length-first);
                count+=data.Length; observed=true; observedTime=dspTime; observedAt=now;
                nextTime=dspTime+(double)data.Length/Channels/Rate;
            }
        }
        internal EchoReferenceRead Read(float[] destination, out Frame frame)
        {
            if(destination==null || destination.Length!=FrameSamples)
                throw new ArgumentException("Supply exactly one interleaved 10 ms frame",nameof(destination));
            lock(sync)
            {
                // Sample under the same lock as Observe: a just-arrived callback
                // must not be mistaken for a future monotonic timestamp.
                double now=clock();
                if(!closed && observed && (!double.IsFinite(now) || now<observedAt || now-observedAt>FreshSeconds))
                    Discontinuity(EchoReferenceGap.Stale);
                frame=new Frame(epoch,sequence,headTime,observedTime,observedAt,pendingGap);
                if(closed || pendingGap!=EchoReferenceGap.None || count<FrameSamples)
                {
                    Array.Clear(destination,0,destination.Length);
                    if(closed) return EchoReferenceRead.Closed;
                    if(pendingGap!=EchoReferenceGap.None) { pendingGap=EchoReferenceGap.None; return EchoReferenceRead.Gap; }
                    return EchoReferenceRead.Waiting;
                }
                int first=Math.Min(FrameSamples,samples.Length-head);
                Array.Copy(samples,head,destination,0,first); Array.Copy(samples,0,destination,first,FrameSamples-first);
                Array.Clear(samples,head,first); Array.Clear(samples,0,FrameSamples-first);
                head=(head+FrameSamples)%samples.Length; count-=FrameSamples;
                frame=new Frame(epoch,++sequence,headTime,observedTime,observedAt,EchoReferenceGap.None);
                headTime+=.01;
                return EchoReferenceRead.Frame;
            }
        }
        void Discontinuity(EchoReferenceGap reason)
        {
            Array.Clear(samples,0,samples.Length); head=count=0; observed=false;
            headTime=nextTime=observedTime=observedAt=0; pendingGap=reason; epoch++;
        }
        public void Dispose()
        {
            lock(sync)
            {
                if(closed) return;
                closed=true; Discontinuity(EchoReferenceGap.None);
            }
        }
    }
}
