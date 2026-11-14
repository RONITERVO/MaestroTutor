// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
namespace Maestro.Quest.Imports {
    // Shared, bounded mono conversion for finite imports and forward-only feeds.
    // A reader is used by one worker. Read never runs on an audio callback.
    internal sealed class AudioSampleReader {
        const int Capacity=16384;
        readonly Func<float[],int,int,int> read;readonly Action check;
        readonly int rate,channels,radius,phases;readonly long maximumFrames;
        readonly float[] ring=new float[Capacity],input=new float[8192];readonly double[][] weights;
        long frames,output;bool ended;
        internal AudioSampleReader(Func<float[],int,int,int> read,int rate,int channels,Action check,long maximumFrames){
            if(read==null||check==null||rate<8000||rate>96000||channels is not (1 or 2)||maximumFrames<1)throw new ArgumentException("Invalid audio conversion source");
            this.read=read;this.rate=rate;this.channels=channels;this.check=check;this.maximumFrames=maximumFrames;
            double cutoff=Math.Min(1,(double)WaveAudio.OutputRate/rate)*.94;radius=(int)Math.Ceiling(16/cutoff);
            weights=rate==WaveAudio.OutputRate?null:Weights(rate,radius,cutoff,check);phases=(weights?.Length??1)-1;
        }
        void Ensure(long needed){
            while(!ended&&frames<=needed){
                check();int count=read(input,0,input.Length);
                if(count<0||count>input.Length||count%channels!=0)throw new InvalidDataException("Invalid decoded frame alignment");
                if(count==0){ended=true;break;}
                if(count/channels>maximumFrames-frames)throw new InvalidDataException("Decoded audio exceeds its admitted duration");
                for(int i=0;i<count;i+=channels){double sum=0;for(int c=0;c<channels;c++){float v=input[i+c];if(!float.IsFinite(v))throw new InvalidDataException("Decoded samples contain non-finite values");sum+=Math.Clamp(v,-1,1);}ring[frames++%Capacity]=(float)(sum/channels);}
            }
        }
        double Sample(long index){index=Math.Clamp(index,0,frames-1);if(index<frames-Capacity)throw new InvalidDataException("The decoder window exceeded its bound");return ring[index%Capacity];}
        internal int Read(short[] destination,int offset,int count){
            if(destination==null||offset<0||count<0||offset>destination.Length-count)throw new ArgumentOutOfRangeException(nameof(count));
            int written=0;
            while(written<count){
                if((output&255)==0)check();
                // Keep integer positions exact for long-lived sources without accumulating clock error.
                long product=checked(output*rate),center=product/WaveAudio.OutputRate;
                Ensure(center+radius);
                if(ended&&output>=(long)Math.Round((double)frames*WaveAudio.OutputRate/rate))break;
                double value=0;
                if(rate==WaveAudio.OutputRate)value=Sample(center);
                else {
                    double phase=(product%WaveAudio.OutputRate)*(double)phases/WaveAudio.OutputRate;
                    int left=(int)phase;double blend=phase-left;var a=weights[left];var b=weights[left+1];
                    for(int j=0;j<a.Length;j++)value+=Sample(center-radius+j)*(a[j]+(b[j]-a[j])*blend);
                }
                destination[offset+written++]=(short)Math.Clamp(Math.Round(Math.Clamp(value,-1,1)*32767),short.MinValue,short.MaxValue);output++;
            }
            return written;
        }
        // Common audio rates use exact rational phases. Unusual Vorbis rates use
        // interpolation between at most 2,049 tables (~2.3 MB), avoiding per-sample
        // trigonometry and any full-resolution decoded PCM allocation.
        static double[][] Weights(int rate,int radius,double cutoff,Action check){
            int a=rate,b=WaveAudio.OutputRate;while(b!=0){int n=a%b;a=b;b=n;}
            int phases=Math.Min(2048,WaveAudio.OutputRate/a);var tables=new double[phases+1][];
            for(int p=0;p<=phases;p++){
                check();var w=tables[p]=new double[radius*2+1];double total=0;
                for(int j=0;j<w.Length;j++){double x=j-radius-(double)p/phases;if(Math.Abs(x)>radius)continue;double v=Math.PI*x*cutoff;w[j]=(Math.Abs(v)<1e-12?1:Math.Sin(v)/v)*(.5+.5*Math.Cos(Math.PI*x/radius));total+=w[j];}
                for(int j=0;j<w.Length;j++)w[j]/=total;
            }return tables;
        }
    }
}
