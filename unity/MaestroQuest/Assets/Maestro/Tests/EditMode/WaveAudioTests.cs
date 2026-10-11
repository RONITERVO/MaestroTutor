// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class WaveAudioTests {
        internal static byte[] File(int rate=24000,int channels=1,int bits=16,bool floating=false,Func<int,int,double> sample=null,double seconds=.1){
            int frames=(int)Math.Round(rate*seconds),block=channels*(bits/8),size=frames*block;
            using var stream=new MemoryStream();using var w=new BinaryWriter(stream,Encoding.UTF8,true);
            void Tag(string s)=>w.Write(Encoding.ASCII.GetBytes(s));
            Tag("RIFF");w.Write(36+size+(size&1));Tag("WAVE");Tag("fmt ");w.Write(16);w.Write((ushort)(floating?3:1));w.Write((ushort)channels);w.Write(rate);w.Write(rate*block);w.Write((ushort)block);w.Write((ushort)bits);Tag("data");w.Write(size);
            for(int i=0;i<frames;i++)for(int c=0;c<channels;c++){
                double v=sample?.Invoke(i,c)??.25;
                if(floating)w.Write((float)v);
                else if(bits==8)w.Write((byte)Math.Clamp(Math.Round(v*128+128),0,255));
                else if(bits==16)w.Write((short)Math.Clamp(Math.Round(v*32768),short.MinValue,short.MaxValue));
                else if(bits==24){int n=(int)Math.Clamp(Math.Round(v*8388608),-8388608,8388607);w.Write((byte)n);w.Write((byte)(n>>8));w.Write((byte)(n>>16));}
                else w.Write((int)Math.Clamp(Math.Round(v*2147483648),int.MinValue,int.MaxValue));
            }
            if((size&1)!=0)w.Write((byte)0);w.Flush();return stream.ToArray();
        }
        [TestCase(8)][TestCase(16)][TestCase(24)][TestCase(32)]
        public void IntegerFormatsRetainAmplitude(int bits){var bytes=File(bits:bits);var info=WaveAudio.Inspect(bytes);Assert.AreEqual(2400,info.Frames);Assert.That(info.Seconds,Is.EqualTo(.1).Within(1e-9));var pcm=WaveAudio.Decode(bytes);Assert.AreEqual(2400,pcm.Length);Assert.That(pcm.All(v=>Math.Abs(v-8192)<=1),Is.True);}
        [Test] public void StereoFloatIsExplicitlyMixedForSpatialMono(){var bytes=File(channels:2,bits:32,floating:true,sample:(i,c)=>c==0?.75:-.25);Assert.That(WaveAudio.Decode(bytes).All(v=>Math.Abs(v-8192)<=1),Is.True);}
        [TestCase(8000)][TestCase(44100)][TestCase(96000)]
        public void ResamplingKeepsDurationAndDc(int rate){var pcm=WaveAudio.Decode(File(rate:rate));Assert.AreEqual(2400,pcm.Length);Assert.That(pcm.All(v=>Math.Abs(v-8192)<=1),Is.True);}
        [Test] public void DownsamplingRejectsOutOfBandTone(){
            double Rms(short[] x)=>Math.Sqrt(x.Skip(100).Take(x.Length-200).Average(v=>(double)v*v))/32767;
            var low=WaveAudio.Decode(File(rate:48000,sample:(i,c)=>.5*Math.Sin(2*Math.PI*500*i/48000)));
            var high=WaveAudio.Decode(File(rate:48000,sample:(i,c)=>.5*Math.Sin(2*Math.PI*20000*i/48000)));
            Assert.That(Rms(low),Is.InRange(.34,.37));Assert.That(Rms(high),Is.LessThan(.005));
        }
        [Test] public void InvalidSamplesAndHeadersDoNotDecode(){
            Assert.Throws<InvalidDataException>(()=>WaveAudio.Decode(File(bits:32,floating:true,sample:(i,c)=>i==1?double.NaN:0)));
            var rate=File();rate[28]=0;Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(rate));
            var length=File();length[4]--;Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(length));
            var chunk=File();for(int i=40;i<44;i++)chunk[i]=255;Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(chunk));
            var compressed=File();compressed[20]=2;Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(compressed));
            Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(File(seconds:.01)));
            Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(File(seconds:30.1)));
        }
        [Test] public void OptionalChunksAndOddPaddingAreRespected(){
            var original=File(rate:8000,bits:8,seconds:.030125);var extra=Encoding.ASCII.GetBytes("JUNK").Concat(new byte[]{1,0,0,0,7,0}).ToArray();
            var bytes=original.Take(12).Concat(extra).Concat(original.Skip(12)).ToArray();Array.Copy(BitConverter.GetBytes(bytes.Length-8),0,bytes,4,4);
            Assert.AreEqual(241,WaveAudio.Inspect(bytes).Frames);Assert.AreEqual(723,WaveAudio.Decode(bytes).Length);
            var duplicate=original.Concat(original.Skip(12).Take(24)).ToArray();Array.Copy(BitConverter.GetBytes(duplicate.Length-8),0,duplicate,4,4);Assert.Throws<InvalidDataException>(()=>WaveAudio.Inspect(duplicate));
        }
        [Test] public void CancellationStopsInspectionAndDecode(){using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>WaveAudio.Decode(File(),cancel.Token));}
    }
}
