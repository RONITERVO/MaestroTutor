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
                at=Mp3Packets.MetadataLength(b);
                if(at>end)throw Invalid("the MP3 metadata is truncated.");
            }
            if(end-at>=128&&Is(b,end-128,"TAG"))end-=128;
            int rate=0,channels=0,frames=0;long samples=0;
            while(at<end){
                check();if(at>end-4)throw Invalid("the MP3 header is truncated.");
                var header=Mp3Packets.Read(b,at);int length=header.Bytes,hz=header.Rate,ch=header.Channels;
                if(length>end-at)throw Invalid("the MP3 frame is truncated.");
                if(frames==0){rate=hz;channels=ch;}else if(rate!=hz||channels!=ch)throw Invalid("MP3 channel or sample-rate changes are unsupported.");
                samples+=header.Samples;if(samples>(long)hz*601)throw Invalid("choose a sound no longer than 600 seconds.");
                at+=length;frames++;
            }
            if(frames<2)throw Invalid("the MP3 has too few complete frames.");
        }
        internal static byte[] Convert(Func<float[],int,int,int> read,int rate,int channels,Action check) {
            if(rate<8000||rate>96000||channels is not (1 or 2))throw Invalid("use mono or stereo sound at 8–96 kHz.");
            var converter=new AudioSampleReader(read,rate,channels,check,(long)rate*(int)WaveAudio.MaximumSeconds);
            using var result=new MemoryStream(262144);
            using var writer=new BinaryWriter(result,Encoding.ASCII,true);writer.Write(new byte[44]);
            var samples=new short[4096];int output=0,count;
            while((count=converter.Read(samples,0,samples.Length))>0){for(int i=0;i<count;i++)writer.Write(samples[i]);output+=count;}
            if(output<WaveAudio.OutputRate*.03)throw Invalid("choose a sound at least 0.03 seconds long.");
            result.Position=0;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+output*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(WaveAudio.OutputRate);writer.Write(WaveAudio.OutputRate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(output*2);writer.Flush();check();return result.ToArray();
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
