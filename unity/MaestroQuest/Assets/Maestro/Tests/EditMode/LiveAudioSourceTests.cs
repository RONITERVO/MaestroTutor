// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class LiveAudioSourceTests {
        static byte[] Fixture(string name)=>File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/CompressedAudio",name+".bytes"));
        static async Task Finish(Task task){Assert.AreSame(task,await Task.WhenAny(task,Task.Delay(5000)),"Live source work did not drain");await task;}
        static async Task Until(Func<bool> predicate){var end=DateTime.UtcNow.AddSeconds(5);while(!predicate()&&DateTime.UtcNow<end)await Task.Delay(5);Assert.IsTrue(predicate(),"Expected live source state was not reached");}
        [TestCase("stereo-vbr.mp3")][TestCase("mono-cbr.mp3")][TestCase("mono-low.mp3")]
        public async Task LiveMp3DecodesFragmentedForwardOnlyInputAndReportsExactEnd(string name){
            int before=LiveMp3Source.ActiveSources;var input=new Input(Fixture(name),fragment:7);var source=LiveMp3Source.Open(input);
            try{
                await Finish(source.Completion);Assert.AreEqual(1,input.Disposals);Assert.AreEqual(before+1,LiveMp3Source.ActiveSources,"Unread terminal PCM retains its admission until the owner closes");
                var state=source.Observe();Assert.AreEqual("available",state.Phase,state.Error);Assert.AreEqual(0,state.Dropped);
                var actual=new List<short>();var buffer=new short[113];
                while(true){var result=source.Read(buffer,0,buffer.Length);if(result.Count==0){Assert.AreEqual("ended",result.State.Phase,result.State.Error);break;}Assert.AreEqual(actual.Count,result.FirstFrame);actual.AddRange(buffer.Take(result.Count));}
                var expected=WaveAudio.Decode(CompressedAudio.Normalize(Fixture(name)));Assert.AreEqual(expected.Length,actual.Count);
                Assert.That(expected.Zip(actual,(a,b)=>Math.Abs(a-b)).Max(),Is.LessThanOrEqualTo(1),"Fragment boundaries changed decoded sound");
                Assert.AreEqual(actual.Count,source.Observe().Consumed);await Finish(source.Close());await Finish(source.Close());Assert.AreEqual("closed",source.Observe().Phase);Assert.AreEqual(1,input.Disposals);
            }finally{await Finish(source.Close());}
        }
        [Test] public async Task LiveMp3BufferDoesNotTreatStarvationAsEndAndCloseInterruptsRead(){
            using var input=new Input(Array.Empty<byte>(),block:true);var source=LiveMp3Source.Open(input);
            try{
                await Until(()=>input.ReadStarted);var result=source.Read(new short[480],0,480);Assert.AreEqual(0,result.Count);Assert.AreEqual("buffering",result.State.Phase);Assert.IsFalse(source.Completion.IsCompleted);
                await Finish(source.Close());Assert.AreEqual("closed",source.Observe().Phase);Assert.AreEqual(1,input.Disposals);
                result=source.Read(new short[480],0,480);Assert.AreEqual(0,result.Count);Assert.AreEqual("closed",result.State.Phase);
            }finally{await Finish(source.Close());}
        }
        [Test] public async Task LiveMp3CancellationAndLateInputCannotReviveClosedSource(){
            using var token=new CancellationTokenSource();using var input=new Input(Array.Empty<byte>(),block:true);var source=LiveMp3Source.Open(input,token.Token);
            try{await Until(()=>input.ReadStarted);token.Cancel();await Finish(source.Completion);Assert.AreEqual("closed",source.Observe().Phase);Assert.AreEqual(0,source.Observe().Buffered);Assert.AreEqual(1,input.Disposals);}
            finally{await Finish(source.Close());}
        }
        [Test] public async Task LiveMp3AdmissionRejectsAliasesAndRetainsBudgetWhileDisposalDrains(){
            int before=LiveMp3Source.ActiveSources;Assert.AreEqual(0,before);
            using var first=new Input(Array.Empty<byte>(),block:true,holdDispose:true);using var second=new Input(Array.Empty<byte>(),block:true);using var rejected=new Input(Array.Empty<byte>(),block:true);
            var a=LiveMp3Source.Open(first);var b=LiveMp3Source.Open(second);
            try{
                Assert.Throws<InvalidOperationException>(()=>LiveMp3Source.Open(first));Assert.AreEqual(0,first.Disposals,"Alias rejection must not dispose the true owner's source");
                Assert.Throws<InvalidOperationException>(()=>LiveMp3Source.Open(rejected));Assert.AreEqual(0,rejected.Disposals,"Rejected admission never transfers ownership");
                Task closing=a.Close();await Until(()=>first.Disposals>0);Assert.IsFalse(closing.IsCompleted);Assert.AreEqual(2,LiveMp3Source.ActiveSources);
                Assert.Throws<InvalidOperationException>(()=>LiveMp3Source.Open(rejected));
                first.ReleaseDispose();await Finish(closing);Assert.AreEqual(1,LiveMp3Source.ActiveSources);
                var next=LiveMp3Source.Open(rejected);await Finish(next.Close());
            }finally{first.ReleaseDispose();await Finish(a.Close());await Finish(b.Close());}
            Assert.AreEqual(before,LiveMp3Source.ActiveSources);
        }
        [Test] public async Task LiveMp3ExpiredSamplesHaveVisibleGapWithoutUnboundedReplay(){
            // Repeat actual complete frames without ID3/Xing tags. This is a live
            // feed longer than the output buffer, not a duration-header assertion.
            byte[] frames=RawFrames(Fixture("mono-cbr.mp3"));var input=new Input(Enumerable.Range(0,12).SelectMany(_=>frames).ToArray());var source=LiveMp3Source.Open(input);
            try{
                await Finish(source.Completion);var state=source.Observe();Assert.AreEqual("available",state.Phase,state.Error);Assert.Greater(state.Produced,LiveMp3Source.Capacity);Assert.AreEqual(LiveMp3Source.Capacity,state.Buffered);
                var pcm=new short[LiveMp3Source.Capacity];var result=source.Read(pcm,0,pcm.Length);
                Assert.AreEqual(state.Produced-LiveMp3Source.Capacity,result.FirstFrame);Assert.AreEqual(result.FirstFrame,result.Dropped);Assert.AreEqual(result.Dropped,result.State.Dropped);Assert.AreEqual(pcm.Length,result.Count);Assert.AreEqual("ended",result.State.Phase);
                Assert.That(Math.Sqrt(pcm.Average(v=>(double)v*v)),Is.GreaterThan(1000));
                result=source.Read(pcm,0,pcm.Length);Assert.AreEqual(0,result.Count);Assert.AreEqual(0,result.Dropped);Assert.AreEqual(state.Produced,result.FirstFrame);
            }finally{await Finish(source.Close());}
        }
        [TestCase("truncated")][TestCase("metadata")][TestCase("junk")][TestCase("changed")]
        public async Task LiveMp3RejectsDamagedOrChangingFeedsWithoutPretendingTheyEnded(string damage){
            byte[] bytes=Fixture("mono-cbr.mp3");
            if(damage=="truncated")bytes=bytes.Take(bytes.Length-2).ToArray();
            if(damage=="metadata")bytes=new byte[]{73,68,51,4,0,0,0,4,0,1};
            if(damage=="junk")bytes=new byte[]{0,1,2,3,4,5};
            if(damage=="changed")bytes=RawFrames(bytes).Concat(RawFrames(Fixture("mono-low.mp3"))).ToArray();
            var source=LiveMp3Source.Open(new Input(bytes,fragment:3));
            try{await Finish(source.Completion);Assert.AreEqual("failed",source.Observe().Phase);Assert.IsNotEmpty(source.Observe().Error);var result=source.Read(new short[480],0,480);Assert.AreEqual(0,result.Count);Assert.AreEqual("failed",result.State.Phase);}
            finally{await Finish(source.Close());}
        }
        [Test] public async Task LiveMp3RejectedPrecancelledInputRemainsWithCaller(){using var stop=new CancellationTokenSource();stop.Cancel();using var input=new Input(Array.Empty<byte>());Assert.Catch<OperationCanceledException>(()=>LiveMp3Source.Open(input,stop.Token));Assert.AreEqual(0,input.Disposals);await Task.CompletedTask;}
        [Test] public async Task LiveMp3CancellationAlsoRevokesUnreadTerminalAudio(){
            using var stop=new CancellationTokenSource();var input=new Input(Fixture("mono-cbr.mp3"));var source=LiveMp3Source.Open(input,stop.Token);
            try{await Finish(source.Completion);Assert.AreEqual("available",source.Observe().Phase);stop.Cancel();Assert.AreEqual("closed",source.Observe().Phase);Assert.AreEqual(0,source.Observe().Buffered);Assert.AreEqual(0,LiveMp3Source.ActiveSources);Assert.AreEqual(1,input.Disposals);}
            finally{await Finish(source.Close());}
        }
        [Test] public void AudioSampleReaderCanContinueBeyondFileImportDurationWithoutWholeRecordingMemory(){
            const int rate=24000,total=rate*601;int input=0;var reader=new AudioSampleReader((b,o,c)=>{int n=Math.Min(c,total-input);for(int i=0;i<n;i++)b[o+i]=.25f;input+=n;return n;},rate,1,()=>{},long.MaxValue);
            var block=new short[4096];long output=0;int count;while((count=reader.Read(block,0,block.Length))>0){output+=count;Assert.AreEqual(8192,block[0]);Assert.AreEqual(8192,block[count-1]);}Assert.AreEqual(total,output);
            input=0;var bounded=new AudioSampleReader((b,o,c)=>{int n=Math.Min(c,total-input);input+=n;return n;},rate,1,()=>{},100);
            Assert.Throws<InvalidDataException>(()=>bounded.Read(block,0,block.Length),"Finite imports still enforce their separate duration limit");
        }
        [Test] public void AudioSampleReaderDoesNotConfuseInvalidDecoderResultsWithEnd(){
            var output=new short[480];
            foreach(int n in new[]{-1,8193,1}){var reader=new AudioSampleReader((b,o,c)=>n,48000,2,()=>{},long.MaxValue);Assert.Throws<InvalidDataException>(()=>reader.Read(output,0,output.Length));}
            var invalid=new AudioSampleReader((b,o,c)=>{b[o]=float.NaN;return 1;},24000,1,()=>{},long.MaxValue);Assert.Throws<InvalidDataException>(()=>invalid.Read(output,0,output.Length));
        }
        static byte[] RawFrames(byte[] source){
            int at=0;if(source[0]=='I'&&source[1]=='D'&&source[2]=='3')at=10+(source[6]<<21|source[7]<<14|source[8]<<7|source[9]);
            // Fixture starts with an Info/Xing duration frame; omit it so the live
            // decoder cannot use a finite file's gapless duration for repetitions.
            int version=(source[at+1]>>3)&3,sample=(source[at+2]>>2)&3,bitrate=source[at+2]>>4;
            int rate=new[]{44100,48000,32000}[sample]/(version==3?1:version==2?2:4);
            int bits=(version==3?new[]{0,32,40,48,56,64,80,96,112,128,160,192,224,256,320}:new[]{0,8,16,24,32,40,48,56,64,80,96,112,128,144,160})[bitrate];
            int length=(version==3?144000:72000)*bits/rate+((source[at+2]>>1)&1);return source.Skip(at+length).ToArray();
        }
        sealed class Input:Stream {
            readonly byte[] bytes;readonly int fragment;readonly bool block,holdDispose;readonly ManualResetEventSlim closed=new(),disposeReleased=new();int position,disposed;
            internal volatile bool ReadStarted;internal int Disposals=>Volatile.Read(ref disposed);
            internal Input(byte[] bytes,int fragment=int.MaxValue,bool block=false,bool holdDispose=false){this.bytes=bytes;this.fragment=fragment;this.block=block;this.holdDispose=holdDispose;}
            internal void ReleaseDispose()=>disposeReleased.Set();
            public override int Read(byte[] b,int offset,int count){ReadStarted=true;if(Disposals>0)throw new ObjectDisposedException(nameof(Input));if(position==bytes.Length&&block){closed.Wait();throw new ObjectDisposedException(nameof(Input));}int n=Math.Min(count,Math.Min(fragment,bytes.Length-position));Array.Copy(bytes,position,b,offset,n);position+=n;return n;}
            protected override void Dispose(bool disposing){if(Interlocked.Exchange(ref disposed,1)==0){closed.Set();if(holdDispose)disposeReleased.Wait();}base.Dispose(disposing);}
            public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush()=>throw new NotSupportedException();public override long Seek(long n,SeekOrigin o)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        }
    }
}
