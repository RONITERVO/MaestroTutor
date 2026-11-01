// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class WaveStreamingTests {
        [TestCase(8000,8,false)][TestCase(24000,16,false)][TestCase(44100,24,false)][TestCase(96000,32,true)]
        public void ArbitraryBlocksExactlyMatchResidentResampling(int rate,int bits,bool floating){
            var bytes=WaveAudioTests.File(rate:rate,bits:bits,floating:floating,channels:2,seconds:1.207,sample:(i,c)=>.45*Math.Sin(i*.031+c));
            var expected=WaveAudio.Decode(bytes);var info=WaveAudio.Inspect(bytes);using var input=new MemoryStream(bytes,false);
            foreach(int start in new[]{0,1,4799,23999,24000,expected.Length-1}){
                int count=Math.Min(4001,expected.Length-start);CollectionAssert.AreEqual(expected.Skip(start).Take(count),WaveAudio.DecodeBlock(input,in info,start,count));
            }
        }
        sealed class MeasuredInput:MemoryStream {
            internal long ReadBytes;internal int LargestRead;
            internal MeasuredInput(byte[] bytes):base(bytes,false){}
            public override int Read(byte[] buffer,int offset,int count){LargestRead=Math.Max(count,LargestRead);int n=base.Read(buffer,offset,count);ReadBytes+=n;return n;}
        }
        [Test] public void LongInspectionSkipsIntegerPayloadAndBlockReadsStayBounded(){
            var bytes=WaveAudioTests.File(rate:96000,channels:2,bits:16,seconds:32);using var input=new MeasuredInput(bytes);
            var info=WaveAudio.Inspect(input);Assert.That(input.ReadBytes,Is.LessThan(100));Assert.AreEqual(32,info.Seconds);
            var pcm=WaveAudio.DecodeBlock(input,in info,24*24000,24000);Assert.AreEqual(24000,pcm.Length);
            Assert.That(input.ReadBytes,Is.LessThan(390000));Assert.That(input.LargestRead,Is.LessThanOrEqualTo(65536));
            Assert.Throws<InvalidDataException>(()=>WaveAudio.Decode(bytes));
        }
        static async Task<int> Read(AudioPcmAsset asset,int frame,short[] data){
            var deadline=DateTime.UtcNow.AddSeconds(10);int count;
            while((count=asset.Read(frame,data,data.Length))==0){Assert.Less(DateTime.UtcNow,deadline);await Task.Delay(5).ConfigureAwait(false);}
            return count;
        }
        [Test] public async Task SharedSourceHasIndependentReadsAndAnEvictingBoundedCache(){
            string dir=Path.Combine(Path.GetTempPath(),"maestro-stream-"+Guid.NewGuid().ToString("N"));var gate=new WorkspaceWriteGate();var library=new AudioLibrary(dir,gate);AudioPcmAsset source=null;
            try{
                var bytes=WaveAudioTests.File(rate:8000,bits:8,seconds:80,sample:(i,c)=>.6*Math.Sin(i*.01));var asset=AudioLibrary.Inspect("Long sound",bytes);await library.SaveAsync(asset).ConfigureAwait(false);
                source=await library.PrepareAsync(asset.Hash,80).ConfigureAwait(false);Assert.IsNull(source.ResidentSamples);Assert.AreEqual(80*24000,source.Frames);
                Assert.IsTrue(gate.CanFreeze(out _));using(var freeze=gate.TryFreeze(out var error)){Assert.IsNotNull(freeze,error);}
                Assert.IsTrue(library.TryCaptureArchive(out var capture));using(capture){
                    var docs=new Dictionary<string,byte[]>();var assets=new Dictionary<string,Func<Stream>>();capture.Collect(docs,assets);
                    using var archiveRead=assets["audio/"+asset.Hash+".wav"]();using var copy=new MemoryStream();archiveRead.CopyTo(copy);CollectionAssert.AreEqual(bytes,copy.ToArray());
                }
                using var input=new MemoryStream(bytes,false);var info=WaveAudio.Inspect(bytes);var data=new short[4800];
                foreach(int frame in Enumerable.Range(0,40).Select(i=>(i%2==0?i/2:79-i/2)*24000+123)){
                    int count=await Read(source,frame,data).ConfigureAwait(false);CollectionAssert.AreEqual(WaveAudio.DecodeBlock(input,in info,frame,count),data.Take(count));
                    Assert.That(source.BufferedBytes,Is.LessThanOrEqualTo(WavePcmAsset.MaximumPcmBytes));
                }
                Assert.That(source.CapacityBytes,Is.LessThan(source.Frames*2));var retiring=gate.Retire();Assert.IsFalse(retiring.IsCompleted);
                await source.Close().ConfigureAwait(false);source=null;await retiring.ConfigureAwait(false);Assert.Throws<InvalidOperationException>(()=>gate.RetainRead());
            }finally{if(source!=null)await source.Close().ConfigureAwait(false);if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test] public async Task CancellationDrainsAWaitingBlockAndItsWorkspaceReader(){
            var bytes=WaveAudioTests.File(seconds:32);var info=WaveAudio.Inspect(bytes);var input=new MemoryStream(bytes,false);
            var first=WaveAudio.DecodeBlock(input,in info,0,24000);using var lane=new SemaphoreSlim(0,1);var gate=new WorkspaceWriteGate();
            var source=new WavePcmAsset(input,info,first,lane,CancellationToken.None,gate.RetainRead());
            Assert.AreEqual(0,source.Read(24000,new short[100],100));var retired=gate.Retire();Assert.IsFalse(retired.IsCompleted);
            await source.Close().ConfigureAwait(false);await retired.ConfigureAwait(false);Assert.IsFalse(input.CanRead);Assert.AreEqual(0,source.BufferedBytes);
        }
        [Test] public async Task DamagedPrivateSoundCannotOpenOrLeakAWorkspaceReader(){
            string dir=Path.Combine(Path.GetTempPath(),"maestro-stream-"+Guid.NewGuid().ToString("N"));var gate=new WorkspaceWriteGate();var library=new AudioLibrary(dir,gate);
            try{
                var bytes=WaveAudioTests.File(rate:8000,bits:8,seconds:32);var asset=AudioLibrary.Inspect("Long sound",bytes);await library.SaveAsync(asset).ConfigureAwait(false);
                bytes[44]^=1;File.WriteAllBytes(Path.Combine(dir,asset.Hash+".wav"),bytes);
                try{await library.PrepareAsync(asset.Hash,32).ConfigureAwait(false);Assert.Fail("Damaged bytes must not become a stream");}catch(InvalidDataException){}
                Assert.IsTrue(gate.CanFreeze(out _));Assert.IsTrue(gate.Retire().IsCompleted);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test] public void ReadRetentionDoesNotHideOutstandingWritesFromPreservationOrRetirement(){
            var gate=new WorkspaceWriteGate();var read=gate.RetainRead();var write=gate.Write();Assert.IsFalse(gate.CanFreeze(out _));
            var retired=gate.Retire();write.Dispose();Assert.IsFalse(retired.IsCompleted);read.Dispose();Assert.IsTrue(retired.IsCompleted);read.Dispose();
        }
    }
}
