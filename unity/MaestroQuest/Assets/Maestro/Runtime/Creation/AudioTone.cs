// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation
{
    /// <summary>Deterministic bounded PCM recipe; no script evaluation or provider call.</summary>
    internal static class AudioTone
    {
        internal const int SampleRate=24000;
        internal static float[] Render(RoomAudioDefinition source,System.Threading.CancellationToken cancellation=default)
        {
            if(source==null||source.kind!="tone"||!source.Validate(out _))throw new ArgumentException("Invalid tone recipe");
            int length=(int)Math.Ceiling(source.seconds*SampleRate);var samples=new float[length];
            double phase=0;uint random=(uint)source.seed;
            for(int i=0;i<length;i++) {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                double time=i/(double)SampleRate;
                double frequency=source.frequency+(source.endFrequency-source.frequency)*time/source.seconds;
                double value;
                if(source.wave=="noise"){random^=random<<13;random^=random>>17;random^=random<<5;value=random/(double)uint.MaxValue*2-1;}
                else if(source.wave=="triangle")value=2/Math.PI*Math.Asin(Math.Sin(phase));
                else value=Math.Sin(phase);
                // Exact zero endpoints prevent discontinuities when a recipe is replayed.
                double envelope=Math.Min(1,Math.Min(time/source.attack,(length-1-i)/(double)SampleRate/source.release));
                samples[i]=(float)(value*Math.Max(0,envelope)*.5);
                phase=(phase+2*Math.PI*frequency/SampleRate)%(2*Math.PI);
            }
            return samples;
        }
    }
}
