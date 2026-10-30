// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Reflection;
using System.Threading;
using Maestro.Quest.Book;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class EchoReferenceTests
    {
        double now=10;
        EchoReferenceStream Stream(int rate=48000,int channels=2) => new(rate,channels,()=>now);
        [Test] public void RepacketsRenderBlocksWithoutChangingSamplesChannelsOrTiming()
        {
            using var stream=Stream(); var block=new float[512];var output=new float[960];
            int written=0,read=0;double first=20;
            for(int b=0;b<120;b++)
            {
                for(int s=0;s<block.Length;s++)block[s]=written+s;var copy=(float[])block.Clone();
                stream.Observe(block,2,first+written/2.0/48000,now);written+=block.Length;
                CollectionAssert.AreEqual(copy,block,"Borrowed output cannot be modified or clamped");
                while(stream.Read(output,out var frame)==EchoReferenceRead.Frame)
                {
                    for(int s=0;s<output.Length;s++)Assert.AreEqual(read+s,output[s]);
                    Assert.AreEqual(first+read/2.0/48000,frame.RenderTime,1e-9);Assert.AreEqual(1,frame.Epoch);
                    Assert.AreEqual(read/960+1,frame.Sequence);Assert.AreEqual(now,frame.ObservedAt);read+=960;
                }
            }
            Assert.Greater(read,stream.CapacitySamples,"Exercise ring wrap, including split frames");
        }
        [Test] public void SilenceIsAFrameButMissingDataIsNotFabricatedSilence()
        {
            using var stream=Stream(44100,1);var frame=new float[441];
            Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(frame,out _));
            stream.Observe(new float[441],1,20,now);Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(frame,out _));
            Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(frame,out _));
            foreach(var value in frame)Assert.AreEqual(0,value);
        }
        [Test] public void InvalidBlocksDropQueuedAudioAndAnnounceARecoveryEpoch()
        {
            using var stream=Stream();var output=new float[960];var valid=new float[960];valid[0]=.8f;
            foreach(var invalid in new[]{null,Array.Empty<float>(),new float[3],new[]{float.NaN,0f},new[]{float.PositiveInfinity,0f}})
            {
                stream.Observe(valid,2,20,now);stream.Observe(invalid,2,20.01,now);
                Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(output,out var gap));Assert.AreNotEqual(EchoReferenceGap.None,gap.Gap);
                Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(output,out _));
                stream.Observe(valid,2,30,now);Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(output,out var recovered));
                Assert.AreEqual(gap.Epoch,recovered.Epoch);Assert.AreEqual(.8f,output[0]);
                // Advance to a clean stream boundary for the next case.
                stream.Observe(valid,1,30.01,now);Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(output,out _));
            }
        }
        [Test] public void OverflowAndRenderGapsNeverJoinUnrelatedAudio()
        {
            using var stream=Stream();var half=new float[480];var output=new float[960];half[0]=.5f;
            stream.Observe(half,2,1,now);stream.Observe(half,2,2,now);
            Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(output,out var gap));Assert.AreEqual(EchoReferenceGap.Clock,gap.Gap);
            Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(output,out _));
            stream.Observe(half,2,2.005,now);Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(output,out _));
            var full=new float[stream.CapacitySamples];stream.Observe(full,2,2.01,now);
            stream.Observe(new float[960],2,2.51,now);
            Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(output,out gap));Assert.AreEqual(EchoReferenceGap.Overflow,gap.Gap);
            Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(output,out var latest));Assert.AreEqual(2.51,latest.RenderTime);
            Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(output,out _));
        }
        [Test] public void StaleOrBackwardsClockDropsReferenceInsteadOfOpeningCapture()
        {
            foreach(double invalid in new[]{11.0,9.0,double.NaN})
            {
                now=10;using var stream=Stream();var data=new float[960];data[0]=1;
                stream.Observe(data,2,20,now);now=invalid;
                Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(data,out var gap));Assert.AreEqual(EchoReferenceGap.Stale,gap.Gap);
                now=12;Assert.AreEqual(EchoReferenceRead.Waiting,stream.Read(data,out _));
                stream.Observe(data,2,30,now);Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(data,out _));
            }
        }
        [Test] public void CloseClearsSamplesAndCannotBeReopenedByLateCallbacks()
        {
            using var stream=Stream();var data=new float[960];Array.Fill(data,.6f);stream.Observe(data,2,20,now);
            stream.Dispose();stream.Observe(data,2,20.01,now);
            Assert.IsTrue(stream.Closed);Assert.AreEqual(EchoReferenceRead.Closed,stream.Read(data,out _));
            foreach(float value in data)Assert.AreEqual(0,value);
            var stored=(float[])typeof(EchoReferenceStream).GetField("samples",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stream);
            foreach(float value in stored)Assert.AreEqual(0,value,"No retained PCM after close");
        }
        [Test] public void FormatAndCallerBufferAreValidatedBeforeConsumption()
        {
            foreach(int rate in new[]{0,7999,22050,192100})Assert.Throws<ArgumentOutOfRangeException>(()=>Stream(rate));
            foreach(int channels in new[]{0,3,8})Assert.Throws<ArgumentOutOfRangeException>(()=>Stream(48000,channels));
            using var stream=Stream();var data=new float[960];data[0]=.4f;stream.Observe(data,2,20,now);
            Assert.Throws<ArgumentException>(()=>stream.Read(new float[959],out _));
            Assert.AreEqual(EchoReferenceRead.Frame,stream.Read(data,out _));Assert.AreEqual(.4f,data[0]);
            stream.Observe(data,2,double.NaN,now);Assert.AreEqual(EchoReferenceRead.Gap,stream.Read(data,out _));
        }
        [Test] public void ContinuousRenderAndReadAllocateNoManagedMemory()
        {
            using var stream=Stream();var data=new float[960];var output=new float[960];
            stream.Observe(data,2,0,now);stream.Read(output,out _);
            int received=0;
            long allocations=ManagedAllocations.Measure(() => {
                for(int i=1;i<=1000;i++) { stream.Observe(data,2,i*.01,now);if(stream.Read(output,out _)==EchoReferenceRead.Frame)received++; }
            });
            TestContext.WriteLine($"Echo render/read: {allocations} managed allocation events for {received} frames");
            Assert.AreEqual(1000,received);Assert.AreEqual(0,allocations);
        }
        [Test] public void ReadSamplesClockAfterConcurrentCallbackRatherThanBeforeItsLock()
        {
            using var stream=Stream();var data=new float[960];var sync=typeof(EchoReferenceStream).GetField("sync",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stream);
            using var entered=new ManualResetEventSlim(false);EchoReferenceRead result=default;Exception failure=null;
            var reader=new Thread(()=>{entered.Set();try{result=stream.Read(data,out _);}catch(Exception error){failure=error;}}){IsBackground=true};
            bool started,waiting;
            lock(sync)
            {
                reader.Start();started=entered.Wait(3000);waiting=SpinWait.SpinUntil(()=>(reader.ThreadState&ThreadState.WaitSleepJoin)!=0,3000);
                now=10.2;stream.Observe(new float[960],2,20,now);
            }
            Assert.IsTrue(reader.Join(3000));Assert.IsTrue(started);Assert.IsTrue(waiting);Assert.IsNull(failure);Assert.AreEqual(EchoReferenceRead.Frame,result);
        }
    }
}
