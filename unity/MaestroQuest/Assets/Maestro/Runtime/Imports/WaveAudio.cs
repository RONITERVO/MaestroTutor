// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
namespace Maestro.Quest.Imports {
    /// <summary>Bounded RIFF/WAVE adapter. Original bytes remain the asset identity;
    /// playback converts on a worker to the shared 24 kHz mono emitter transport.</summary>
    internal static class WaveAudio {
        internal const int MaximumBytes=32*1024*1024, OutputRate=24000;
        internal const double MaximumSeconds=30;
        internal readonly struct Info {
            internal readonly int Rate,Channels,Bits,Frames,Offset;internal readonly bool Floating;
            internal Info(int rate,int channels,int bits,int frames,int offset,bool floating){Rate=rate;Channels=channels;Bits=bits;Frames=frames;Offset=offset;Floating=floating;}
            internal double Seconds=>(double)Frames/Rate;
        }
        static InvalidDataException Invalid(string message)=>new("This sound cannot be imported: "+message);
        static uint U32(byte[] b,int at)=>(uint)(b[at]|b[at+1]<<8|b[at+2]<<16|b[at+3]<<24);
        static int U16(byte[] b,int at)=>b[at]|b[at+1]<<8;
        static bool Tag(byte[] b,int at,string tag)=>b[at]==tag[0]&&b[at+1]==tag[1]&&b[at+2]==tag[2]&&b[at+3]==tag[3];
        internal static Info Inspect(byte[] bytes,CancellationToken cancel=default){
            if(bytes==null||bytes.Length<44||bytes.Length>MaximumBytes)throw Invalid("choose a complete WAV file of 32 MB or smaller.");
            if(!Tag(bytes,0,"RIFF")||!Tag(bytes,8,"WAVE")||U32(bytes,4)!=(uint)bytes.Length-8)throw Invalid("the RIFF/WAVE length or header is invalid.");
            int format=-1,formatBytes=0,data=-1,dataBytes=0,chunks=0;
            for(int at=12;at<bytes.Length;){
                cancel.ThrowIfCancellationRequested();if(++chunks>1024||bytes.Length-at<8)throw Invalid("the chunk table is incomplete or too large.");
                uint size=U32(bytes,at+4);long end=(long)at+8+size,next=end+(size&1);
                if(end>bytes.Length||next>bytes.Length)throw Invalid("a chunk extends beyond the file.");
                if(Tag(bytes,at,"fmt ")){if(format>=0||data>=0)throw Invalid("the format chunk is duplicated or follows the samples.");format=at+8;formatBytes=(int)size;}
                if(Tag(bytes,at,"data")){if(data>=0||format<0)throw Invalid("there must be one sample chunk after the format.");data=at+8;dataBytes=(int)size;}
                at=(int)next;
            }
            if(format<0||data<0||formatBytes!=16&&formatBytes!=18||formatBytes==18&&U16(bytes,format+16)!=0)throw Invalid("use an ordinary PCM or float WAV format.");
            int encoding=U16(bytes,format),channels=U16(bytes,format+2),bits=U16(bytes,format+14),block=U16(bytes,format+12);
            uint rate=U32(bytes,format+4);bool floating=encoding==3;
            if(encoding!=1&&!floating||channels is not (1 or 2)||rate<8000||rate>96000||floating&&bits!=32||!floating&&bits is not (8 or 16 or 24 or 32))throw Invalid("use mono or stereo PCM (8/16/24/32 bit) or 32-bit float, 8–96 kHz.");
            if(block!=channels*(bits/8)||U32(bytes,format+8)!=rate*block||dataBytes==0||dataBytes%block!=0)throw Invalid("the sample alignment or byte rate is invalid.");
            int frames=dataBytes/block;var info=new Info((int)rate,channels,bits,frames,data,floating);
            if(info.Seconds<.03||info.Seconds>MaximumSeconds)throw Invalid("choose a sound between 0.03 and 30 seconds; longer and streamed sources need another adapter.");
            if(floating)for(int i=0;i<frames*channels;i++){if((i&4095)==0)cancel.ThrowIfCancellationRequested();if(!float.IsFinite(BitConverter.Int32BitsToSingle((int)U32(bytes,data+i*4))))throw Invalid("samples contain non-finite values.");}
            return info;
        }
        static double Sample(byte[] bytes,in Info info,int frame){
            frame=Math.Clamp(frame,0,info.Frames-1);int offset=info.Offset+frame*info.Channels*(info.Bits/8);double sum=0;
            for(int c=0;c<info.Channels;c++,offset+=info.Bits/8){
                double value;
                if(info.Floating)value=BitConverter.Int32BitsToSingle((int)U32(bytes,offset));
                else if(info.Bits==8)value=(bytes[offset]-128)/128.0;
                else if(info.Bits==16)value=(short)U16(bytes,offset)/32768.0;
                else if(info.Bits==24){int v=bytes[offset]|bytes[offset+1]<<8|bytes[offset+2]<<16;value=(v<<8>>8)/8388608.0;}
                else value=(int)U32(bytes,offset)/2147483648.0;
                sum+=Math.Clamp(value,-1,1);
            }
            return sum/info.Channels;
        }
        internal static short[] Decode(byte[] bytes,CancellationToken cancel=default){
            var info=Inspect(bytes,cancel);int count=(int)Math.Round(info.Seconds*OutputRate);var pcm=new short[count];
            // A windowed low-pass resampler prevents high input frequencies folding
            // into an audible false tone when adapting to the spatial mono transport.
            double cutoff=Math.Min(1,(double)OutputRate/info.Rate)*.94;
            int radius=(int)Math.Ceiling(16/cutoff);
            for(int i=0;i<count;i++){
                if((i&255)==0)cancel.ThrowIfCancellationRequested();double time=(double)i*info.Rate/OutputRate,value=0,weight=0;
                if(info.Rate==OutputRate)value=Sample(bytes,in info,i);
                else for(int j=(int)Math.Floor(time)-radius;j<=(int)Math.Floor(time)+radius;j++){
                    double x=j-time;if(Math.Abs(x)>radius)continue;double a=Math.PI*x*cutoff;
                    double w=(Math.Abs(a)<1e-12?1:Math.Sin(a)/a)*(.5+.5*Math.Cos(Math.PI*x/radius));
                    value+=Sample(bytes,in info,j)*w;weight+=w;
                }
                if(info.Rate!=OutputRate)value/=weight;
                pcm[i]=(short)Math.Clamp(Math.Round(Math.Clamp(value,-1,1)*32767),short.MinValue,short.MaxValue);
            }
            return pcm;
        }
    }
}
