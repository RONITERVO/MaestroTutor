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
        internal const double MaximumSeconds=600, ResidentSeconds=30;
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
            if(bytes==null)throw Invalid("choose a complete WAV file of 32 MB or smaller.");
            using var input=new MemoryStream(bytes,false);return Inspect(input,cancel);
        }
        static byte[] Read(Stream input,int at,int count,CancellationToken cancel){
            var bytes=new byte[count];input.Position=at;int read=0;
            while(read<count){cancel.ThrowIfCancellationRequested();int n=input.Read(bytes,read,Math.Min(65536,count-read));if(n==0)throw Invalid("the sound file changed or ended early.");read+=n;}
            return bytes;
        }
        internal static Info Inspect(Stream input,CancellationToken cancel=default){
            cancel.ThrowIfCancellationRequested();
            if(input==null||!input.CanRead||!input.CanSeek||input.Length<44||input.Length>MaximumBytes)throw Invalid("choose a complete WAV file of 32 MB or smaller.");
            int length=(int)input.Length;var header=Read(input,0,12,cancel);
            if(!Tag(header,0,"RIFF")||!Tag(header,8,"WAVE")||U32(header,4)!=(uint)length-8)throw Invalid("the RIFF/WAVE length or header is invalid.");
            int format=-1,formatBytes=0,data=-1,dataBytes=0,chunks=0;
            for(int at=12;at<length;){
                cancel.ThrowIfCancellationRequested();if(++chunks>1024||length-at<8)throw Invalid("the chunk table is incomplete or too large.");
                var chunk=Read(input,at,8,cancel);uint size=U32(chunk,4);long end=(long)at+8+size,next=end+(size&1);
                if(end>length||next>length)throw Invalid("a chunk extends beyond the file.");
                if(Tag(chunk,0,"fmt ")){if(format>=0||data>=0)throw Invalid("the format chunk is duplicated or follows the samples.");format=at+8;formatBytes=(int)size;}
                if(Tag(chunk,0,"data")){if(data>=0||format<0)throw Invalid("there must be one sample chunk after the format.");data=at+8;dataBytes=(int)size;}
                at=(int)next;
            }
            if(format<0||data<0||formatBytes!=16&&formatBytes!=18)throw Invalid("use an ordinary PCM or float WAV format.");
            var fmt=Read(input,format,formatBytes,cancel);
            if(formatBytes==18&&U16(fmt,16)!=0)throw Invalid("use an ordinary PCM or float WAV format.");
            int encoding=U16(fmt,0),channels=U16(fmt,2),bits=U16(fmt,14),block=U16(fmt,12);
            uint rate=U32(fmt,4);bool floating=encoding==3;
            if(encoding!=1&&!floating||channels is not (1 or 2)||rate<8000||rate>96000||floating&&bits!=32||!floating&&bits is not (8 or 16 or 24 or 32))throw Invalid("use mono or stereo PCM (8/16/24/32 bit) or 32-bit float, 8–96 kHz.");
            if(block!=channels*(bits/8)||U32(fmt,8)!=rate*block||dataBytes==0||dataBytes%block!=0)throw Invalid("the sample alignment or byte rate is invalid.");
            int frames=dataBytes/block;var info=new Info((int)rate,channels,bits,frames,data,floating);
            if(info.Seconds<.03||info.Seconds>MaximumSeconds)throw Invalid("choose a sound between 0.03 and 600 seconds.");
            if(floating)for(int at=0;at<dataBytes;at+=65536){
                var values=Read(input,data+at,Math.Min(65536,dataBytes-at),cancel);
                for(int i=0;i<values.Length;i+=4)if(!float.IsFinite(BitConverter.Int32BitsToSingle((int)U32(values,i))))throw Invalid("samples contain non-finite values.");
            }
            if(input.Length!=length)throw Invalid("the sound file changed during inspection.");return info;
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
        internal static int OutputFrames(in Info info)=>(int)Math.Round(info.Seconds*OutputRate);
        internal static short[] Decode(byte[] bytes,CancellationToken cancel=default){
            var info=Inspect(bytes,cancel);
            if(info.Seconds>ResidentSeconds)throw Invalid("long sounds require bounded streaming playback.");
            return Render(bytes,in info,0,0,OutputFrames(in info),cancel);
        }
        // Read only the source frames required by this output block, including the
        // low-pass filter's complete support. Global output indices keep block
        // boundaries bit-identical to decoding a short clip in one pass.
        internal static short[] DecodeBlock(Stream input,in Info info,int first,int count,CancellationToken cancel=default){
            int total=OutputFrames(in info);
            if(first<0||first>=total||count<1||count>OutputRate||count>total-first)throw new ArgumentOutOfRangeException(nameof(count));
            double cutoff=Math.Min(1,(double)OutputRate/info.Rate)*.94;int radius=(int)Math.Ceiling(16/cutoff);
            int begin=Math.Max(0,(int)Math.Floor((double)first*info.Rate/OutputRate)-radius);
            int end=Math.Min(info.Frames-1,(int)Math.Floor((double)(first+count-1)*info.Rate/OutputRate)+radius);
            int stride=info.Channels*(info.Bits/8);var bytes=Read(input,info.Offset+begin*stride,(end-begin+1)*stride,cancel);
            var window=new Info(info.Rate,info.Channels,info.Bits,end-begin+1,0,info.Floating);
            return Render(bytes,in window,begin,first,count,cancel);
        }
        static short[] Render(byte[] bytes,in Info info,int firstInput,int firstOutput,int count,CancellationToken cancel){
            var pcm=new short[count];double cutoff=Math.Min(1,(double)OutputRate/info.Rate)*.94;
            int radius=(int)Math.Ceiling(16/cutoff);
            for(int n=0;n<count;n++){
                if((n&255)==0)cancel.ThrowIfCancellationRequested();int i=firstOutput+n;double time=(double)i*info.Rate/OutputRate,value=0,weight=0;
                if(info.Rate==OutputRate)value=Sample(bytes,in info,i-firstInput);
                else for(int j=(int)Math.Floor(time)-radius;j<=(int)Math.Floor(time)+radius;j++){
                    double x=j-time;if(Math.Abs(x)>radius)continue;double a=Math.PI*x*cutoff;
                    double w=(Math.Abs(a)<1e-12?1:Math.Sin(a)/a)*(.5+.5*Math.Cos(Math.PI*x/radius));
                    value+=Sample(bytes,in info,j-firstInput)*w;weight+=w;
                }
                if(info.Rate!=OutputRate)value/=weight;
                pcm[n]=(short)Math.Clamp(Math.Round(Math.Clamp(value,-1,1)*32767),short.MinValue,short.MaxValue);
            }
            return pcm;
        }
    }
}
