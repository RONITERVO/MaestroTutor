// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
namespace Maestro.Quest.Imports {
    /// <summary>Import-only normalization. Saved and streamed assets remain ordinary WAV.
    /// Never called on Unity's audio thread; readers share AudioLibrary's worker limit.</summary>
    internal static class CompressedAudio {
        internal static byte[] Normalize(byte[] bytes,CancellationToken token=default) {
            token.ThrowIfCancellationRequested();
            if(bytes==null||bytes.Length<44||bytes.Length>WaveAudio.MaximumBytes)throw Invalid("choose a local file within 32 MB.");
            if(Is(bytes,0,"RIFF")){_=WaveAudio.Inspect(bytes,token);return bytes;}
            using var stream=new ImportStream(bytes,token);
            try {
                if(Is(bytes,0,"OggS")) {
                    ValidateOgg(bytes,stream.Check);
                    using var reader=new NVorbis.VorbisReader(stream,false){ClipSamples=false};
                    return Convert(reader.ReadSamples,reader.SampleRate,reader.Channels,stream.Check);
                }
                ValidateMp3(bytes,stream.Check);
                using var mp3=new NLayer.MpegFile(stream);
                return Convert(mp3.ReadSamples,mp3.SampleRate,mp3.Channels,stream.Check);
            }catch(OperationCanceledException){throw;}
            catch(InvalidDataException){throw;}
            catch(Exception ex) when(ex is ArgumentException||ex is IndexOutOfRangeException||ex is OverflowException||ex is EndOfStreamException||ex is DivideByZeroException) {
                throw Invalid("the compressed sound is damaged or unsupported.");
            }
        }
        static InvalidDataException Invalid(string reason)=>new("Sound import: "+reason);
        static bool Is(byte[] b,int at,string text){if(at<0||at+text.Length>b.Length)return false;for(int i=0;i<text.Length;i++)if(b[at+i]!=text[i])return false;return true;}
        static uint U32(byte[] b,int p)=>(uint)(b[p]|b[p+1]<<8|b[p+2]<<16|b[p+3]<<24);
        // Ogg is a container, not a codec. Admit one complete Vorbis stream only;
        // reject holes, concatenation and oversized packets before the decoder allocates.
        internal static void ValidateOgg(byte[] b,Action check) {
            int at=0,packetBytes=0,packets=0;uint serial=0,sequence=0;bool ended=false;
            using var header=new MemoryStream();
            while(at<b.Length){
                check();if(ended||at>b.Length-27||!Is(b,at,"OggS")||b[at+4]!=0)throw Invalid("the Ogg container is incomplete or chained.");
                int flags=b[at+5],segments=b[at+26],body=at+27+segments;
                if((flags&~7)!=0||body>b.Length||((flags&1)!=0)!=(packetBytes>0))throw Invalid("invalid Ogg page continuation.");
                if(at==0){if(flags!=2)throw Invalid("the Ogg stream has no initial page.");serial=U32(b,at+14);}
                else if((flags&2)!=0)throw Invalid("chained Ogg streams are not supported.");
                if(U32(b,at+14)!=serial||U32(b,at+18)!=sequence++)throw Invalid("the Ogg stream contains missing or mixed pages.");
                int length=0;for(int i=0;i<segments;i++)length+=b[at+27+i];
                if(length>b.Length-body)throw Invalid("the Ogg page is truncated.");
                uint crc=0;for(int i=at;i<body+length;i++){if((i&65535)==0)check();byte value=i>=at+22&&i<at+26?(byte)0:b[i];crc=(crc<<8)^CrcTable[(crc>>24)^value];}
                if(crc!=U32(b,at+22))throw Invalid("the Ogg page checksum is damaged.");
                int offset=body;
                for(int i=0;i<segments;i++){
                    int count=b[at+27+i];packetBytes+=count;
                    if(packetBytes>262144)throw Invalid("an Ogg packet exceeds 256 KB.");
                    if(packets<3)header.Write(b,offset,count);offset+=count;
                    if(count<255){
                        if(packets<3){var h=header.ToArray();if(h.Length<7||h[0]!=1+packets*2||!Is(h,1,"vorbis"))throw Invalid("use Ogg Vorbis; Opus and other Ogg codecs are not supported.");
                            if(packets==0&&(h.Length!=30||U32(h,7)!=0))throw Invalid("invalid Vorbis identification header.");
                            if(packets==1&&h.Length>65536)throw Invalid("Vorbis metadata exceeds 64 KB.");header.SetLength(0);}
                        if(++packets>110000)throw Invalid("the Ogg stream exceeds the packet limit.");packetBytes=0;
                    }
                }
                ended=(flags&4)!=0;at=body+length;
            }
            if(!ended||packetBytes!=0||packets<4)throw Invalid("the Ogg stream is incomplete.");
        }
        static readonly uint[] CrcTable=MakeCrc();
        static uint[] MakeCrc(){var table=new uint[256];for(uint i=0;i<256;i++){uint v=i<<24;for(int j=0;j<8;j++)v=(v<<1)^((v&0x80000000)!=0?0x04c11db7u:0);table[i]=v;}return table;}
        // Strict frame walk prevents a forgiving decoder from accepting a truncated
        // selection or searching arbitrary junk. Free-bitrate MP3 is not admitted.
        internal static void ValidateMp3(byte[] b,Action check) {
            int at=0,end=b.Length;
            if(Is(b,0,"ID3")){
                if(b[3]<2||b[3]>4||b[4]==255||(b[6]|b[7]|b[8]|b[9])>=128)throw Invalid("invalid MP3 metadata.");
                int size=b[6]<<21|b[7]<<14|b[8]<<7|b[9];at=10+size+(b[3]==4&&(b[5]&16)!=0?10:0);
                if(at>end)throw Invalid("the MP3 metadata is truncated.");
            }
            if(end-at>=128&&Is(b,end-128,"TAG"))end-=128;
            int rate=0,channels=0,frames=0;long samples=0;
            int[] rates={44100,48000,32000};int[] high={0,32,40,48,56,64,80,96,112,128,160,192,224,256,320};int[] low={0,8,16,24,32,40,48,56,64,80,96,112,128,144,160};
            while(at<end){
                check();if(at>end-4||b[at]!=255||(b[at+1]&224)!=224)throw Invalid("use a complete MP3, Ogg Vorbis or PCM WAV file.");
                int version=(b[at+1]>>3)&3,layer=(b[at+1]>>1)&3,bitrate=b[at+2]>>4,sampling=(b[at+2]>>2)&3;
                if(version==1||layer!=1||bitrate==0||bitrate==15||sampling==3)throw Invalid("use a supported Layer III MP3 (CBR or VBR, not free bitrate).");
                int hz=rates[sampling]/(version==3?1:version==2?2:4),ch=(b[at+3]>>6)==3?1:2;
                int length=(version==3?144000*high[bitrate]:72000*low[bitrate])/hz+((b[at+2]>>1)&1);
                if(length<4||length>end-at)throw Invalid("the MP3 frame is truncated.");
                if(frames==0){rate=hz;channels=ch;}else if(rate!=hz||channels!=ch)throw Invalid("MP3 channel or sample-rate changes are unsupported.");
                samples+=version==3?1152:576;if(samples>(long)hz*601)throw Invalid("choose a sound no longer than 600 seconds.");
                at+=length;frames++;
            }
            if(frames<2)throw Invalid("the MP3 has too few complete frames.");
        }
        internal static byte[] Convert(Func<float[],int,int,int> read,int rate,int channels,Action check) {
            if(rate<8000||rate>96000||channels is not (1 or 2))throw Invalid("use mono or stereo sound at 8–96 kHz.");
            const int capacity=16384;var ring=new float[capacity];var input=new float[8192];int frames=0,output=0;bool ended=false;
            void Ensure(int needed){while(!ended&&frames<=needed){check();int count=read(input,0,input.Length);
                if(count<0||count>input.Length||count%channels!=0)throw Invalid("invalid decoded frame alignment.");
                if(count==0){ended=true;break;}
                if((long)frames+count/channels>(long)rate*WaveAudio.MaximumSeconds)throw Invalid("choose a sound no longer than 600 seconds.");
                for(int i=0;i<count;i+=channels){double sum=0;for(int c=0;c<channels;c++){float v=input[i+c];if(!float.IsFinite(v))throw Invalid("decoded samples contain non-finite values.");sum+=Math.Clamp(v,-1,1);}ring[frames++%capacity]=(float)(sum/channels);}
            }}
            double Sample(int index){index=Math.Clamp(index,0,frames-1);if(index<frames-capacity)throw Invalid("the decoder window exceeded its bound.");return ring[index%capacity];}
            using var result=new MemoryStream(262144);
            using var writer=new BinaryWriter(result,Encoding.ASCII,true);writer.Write(new byte[44]);
            double cutoff=Math.Min(1,(double)WaveAudio.OutputRate/rate)*.94;int radius=(int)Math.Ceiling(16/cutoff);
            var weights=Weights(rate,radius,cutoff,check);int phases=weights.Length-1;
            while(true){
                if((output&255)==0)check();double time=(double)output*rate/WaveAudio.OutputRate;int center=(int)Math.Floor(time);
                Ensure(center+radius);
                if(ended&&output>=(int)Math.Round((double)frames*WaveAudio.OutputRate/rate))break;
                double value=0;
                if(rate==WaveAudio.OutputRate)value=Sample(center);
                else {
                    double phase=((long)output*rate%WaveAudio.OutputRate)*(double)phases/WaveAudio.OutputRate;
                    int left=(int)phase;double blend=phase-left;var a=weights[left];var b=weights[left+1];
                    for(int j=0;j<a.Length;j++)value+=Sample(center-radius+j)*(a[j]+(b[j]-a[j])*blend);
                }
                writer.Write((short)Math.Clamp(Math.Round(Math.Clamp(value,-1,1)*32767),short.MinValue,short.MaxValue));output++;
            }
            if(output<WaveAudio.OutputRate*.03)throw Invalid("choose a sound at least 0.03 seconds long.");
            result.Position=0;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+output*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(WaveAudio.OutputRate);writer.Write(WaveAudio.OutputRate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(output*2);writer.Flush();check();return result.ToArray();
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
        sealed class ImportStream:MemoryStream {
            readonly CancellationToken token;readonly Stopwatch clock=Stopwatch.StartNew();long remaining;
            internal ImportStream(byte[] bytes,CancellationToken token):base(bytes,false){this.token=token;remaining=(long)bytes.Length*8+1048576;}
            internal void Check(){token.ThrowIfCancellationRequested();if(clock.Elapsed.TotalSeconds>120)throw Invalid("decoding exceeded two minutes; try a shorter sound.");}
            public override int Read(byte[] buffer,int offset,int count){Check();int n=base.Read(buffer,offset,count);remaining-=n;if(remaining<0)throw Invalid("decoder input work exceeded its bound.");return n;}
            public override int ReadByte(){Check();if(--remaining<0)throw Invalid("decoder input work exceeded its bound.");return base.ReadByte();}
        }
    }
}
