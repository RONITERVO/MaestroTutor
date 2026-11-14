// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class SpeechPcmStreamTests
    {
        void Write(SpeechPcmStream stream, long sequence, short[] pcm, double now = 0)
            => Assert.IsTrue(stream.TryWrite(sequence, pcm, now, out var error), error);

        [Test] public void ReusableProducerBufferCopiesOnlyItsDeclaredFrames()
        {
            var stream=new SpeechPcmStream(0,0);var buffer=new short[4800];buffer[0]=1234;buffer[1]=-2345;buffer[2]=30000;
            Assert.IsTrue(stream.TryWrite(1,buffer,2,0,out var error),error);buffer[0]=0;
            Assert.IsFalse(stream.TryWrite(2,buffer,0,0,out _));Assert.IsFalse(stream.TryWrite(2,buffer,4801,0,out _));
            var rendered=new float[8];stream.Render(rendered,1,24000,0);
            Assert.AreEqual(1234f/32768,rendered[0]);Assert.AreEqual(-2345f/32768,rendered[1]);Assert.IsTrue(rendered.Skip(2).All(x=>x==0));Assert.AreEqual(2,stream.Read(1).Played);
        }

        [Test] public void PauseFencesProceduralConsumptionAndResumeRetainsTheQueuedPosition()
        {
            var stream=new SpeechPcmStream(0,0);Write(stream,1,new short[]{1000,2000,3000,4000});
            var block=new float[2];stream.Render(block,1,24000,0);Assert.AreEqual(2,stream.Read(1).Played);
            stream.SetPaused(true);stream.Render(block,1,24000,5);Assert.IsTrue(block.All(x=>x==0));Assert.AreEqual(2,stream.Read(6).Played);
            stream.SetPaused(false);stream.Render(block,1,24000,10);Assert.AreEqual(3000f/32768,block[0]);Assert.AreEqual(4000f/32768,block[1]);Assert.AreEqual(4,stream.Read(11).Played);
        }

        [TestCase(24000)] [TestCase(44100)] [TestCase(48000)] [TestCase(96000)]
        public void PacketBoundariesDoNotRestartResamplingOrLoseSamples(int rate)
        {
            var split = new SpeechPcmStream(0, .05); var whole = new SpeechPcmStream(0, .05);
            short[] pcm = Enumerable.Range(0, 4800).Select(i => (short)(Math.Sin(i * .09) * 16000)).ToArray();
            Write(whole, 1, pcm);
            for (int i = 0; i < 16; i++) Write(split, i + 1, pcm.Skip(i * 300).Take(300).ToArray());
            pcm[0] = 32767; // Writes must copy caller data.
            int frames = rate / 5;
            var a = new float[frames * 2]; var b = new float[frames * 2];
            split.Render(a, 2, rate, 0); whole.Render(b, 2, rate, 0);
            CollectionAssert.AreEqual(b, a); Assert.AreEqual(0, a[0]);
            for (int i = 0; i < frames; i++) Assert.AreEqual(a[2*i], a[2*i+1]);
            Assert.AreEqual(0, split.Read(.049).Played);
            Assert.That(split.Read(.15).Played, Is.InRange(2399,2401));
            Assert.AreEqual(4800, split.Read(.251).Played);
        }

        [Test] public void ClockAloneCannotCompleteSpeechAndUnderrunNeverRepeatsOldPcm()
        {
            var stream = new SpeechPcmStream(.1, .05); Write(stream, 1, new short[] { 1234, -1234 });
            Assert.AreEqual(0, stream.Read(50).Played);
            var data = new float[16]; stream.Render(data, 1, 48000, 50);
            Assert.AreEqual(1234f/32768, data[0]); Assert.AreEqual(-1234f/32768, data[2]);
            Assert.That(data.Skip(4), Is.All.Zero); Assert.AreEqual(0, stream.Read(50.04).Played);
            Assert.AreEqual(2, stream.Read(50.06).Played);
            stream.Render(data,1,48000,60); Assert.That(data, Is.All.Zero);
            Write(stream, 2, new short[] { 3210 }, 60);
            Assert.AreEqual(2, stream.Read(80).Played);
            stream.Render(data,1,48000,80); Assert.AreEqual(3210f/32768,data[0]);
            Assert.AreEqual(3,stream.Read(80.06).Played);
        }

        [Test] public void ReceiptTimelineExcludesTheGapAndCarriesFractionalResamplingAcrossDspBlocks()
        {
            var stream = new SpeechPcmStream(0,.05); Write(stream,1,Enumerable.Repeat((short)1000,4800).ToArray());
            double time=0; var block=new float[256];
            for(int i=0;i<35;i++) { stream.Render(block,1,44100,time); time += 256.0/44100; }
            Assert.AreEqual(4800,stream.Read(time+.05).Played);
            Write(stream,2,new short[2400],2);
            stream.Render(new float[4800],1,48000,2);
            Assert.AreEqual(4800,stream.Read(2.04).Played);
            Assert.That(stream.Read(2.10).Played,Is.InRange(5999,6001));
            Assert.AreEqual(7200,stream.Read(2.151).Played);
        }

        [Test] public void BoundedRingWrapPreservesOrderAndDoesNotFreeUnheardSamplesEarly()
        {
            var stream = new SpeechPcmStream(0,.05);
            for(int i=1;i<=40;i++) Write(stream,i,Enumerable.Repeat((short)i,4800).ToArray());
            Assert.IsFalse(stream.TryWrite(41,new short[1],0,out _));
            var first = new float[96000]; stream.Render(first,1,24000,0);
            Assert.IsFalse(stream.TryWrite(41,new short[1],.01,out _));
            Assert.AreEqual(96000,stream.Read(4.06).Played);
            for(int i=41;i<=60;i++) Write(stream,i,Enumerable.Repeat((short)i,4800).ToArray(),4.06);
            var rest = new float[192000]; stream.Render(rest,1,24000,4.06);
            for(int i=0;i<40;i++) Assert.AreEqual((21+i)/32768f,rest[i*4800]);
            Assert.AreEqual(288000,stream.Read(12.12).Played);
        }

        [Test] public void CloseFencesAnAudioThreadAndRejectedWritesDoNotAdvanceCounters()
        {
            var stream = new SpeechPcmStream(0,.05); Write(stream,1,new short[] { 1,2 });
            Assert.IsFalse(stream.TryWrite(1,new short[1],0,out _));
            Assert.IsFalse(stream.TryWrite(3,new short[1],0,out _));
            Assert.IsFalse(stream.TryWrite(2,new short[4801],0,out _));
            Assert.AreEqual(2,stream.Read(1).Submitted);
            var buffer=new float[256];
            var rendering=Task.Run(() => { for(int i=0;i<1000;i++) stream.Render(buffer,1,48000,i/100.0); });
            stream.Close(); rendering.GetAwaiter().GetResult();
            stream.Render(buffer,1,48000,11); Assert.That(buffer,Is.All.Zero);
            Assert.IsFalse(stream.TryWrite(2,new short[1],12,out _)); Assert.AreEqual(0,stream.Read(12).Submitted);
        }

        [Test] public void RenderAndReceiptsStayBoundedWithoutPerBlockManagedAllocations()
        {
            var stream=new SpeechPcmStream(0,.1); var pcm=new short[128]; var output=new float[256];
            stream.TryWrite(1,pcm,0,out _); stream.Render(output,1,48000,0); stream.Read(1);
            int failedSequence=0;
            long allocations=ManagedAllocations.Measure(() => {
                for(int i=2;i<10002;i++) {
                    double time=i/100.0;
                    if(!stream.TryWrite(i,pcm,time,out _)) { failedSequence=i;break; }
                    stream.Render(output,1,48000,time); stream.Read(time);
                }
            });
            TestContext.WriteLine($"Speech render/receipts: {allocations} managed allocation events for 10000 blocks");
            Assert.AreEqual(0,failedSequence,"Steady stream exhausted its bound");Assert.AreEqual(0,allocations);
            Assert.IsFalse(stream.Read(200).Faulted); Assert.AreEqual(10001*128,stream.Read(200).Played);
        }

        [Test] public void ExplicitInputCompletionWaitsForActualReceiptsAndRejectsLaterPackets()
        {
            var stream=new SpeechPcmStream(0,.05);Write(stream,1,new short[]{1000,2000});
            Assert.IsFalse(stream.TryComplete(0,out _));Assert.IsFalse(stream.TryComplete(2,out _));
            Assert.IsFalse(stream.Read(10).InputComplete);Assert.IsTrue(stream.TryComplete(1,out var error),error);
            Assert.IsTrue(stream.TryComplete(1,out _),"Completion is idempotent for the same accepted sequence");
            var queued=stream.Read(10);Assert.IsTrue(queued.InputComplete);Assert.IsFalse(queued.SourceDrained);
            Assert.IsFalse(stream.TryWrite(2,new short[]{3000},10,out _));Assert.AreEqual(2,stream.Read(10).Submitted);
            var block=new float[4];stream.Render(block,1,48000,10);
            CollectionAssert.AreEqual(new float[]{1000f/32768,1500f/32768,2000f/32768,2000f/32768},block);
            Assert.IsFalse(stream.Read(10.04).SourceDrained);var drained=stream.Read(10.06);
            Assert.AreEqual(2,drained.Played);Assert.IsTrue(drained.SourceDrained);Assert.IsFalse(drained.Closed);
            stream.Render(block,1,48000,11);Assert.That(block,Is.All.Zero);Assert.AreEqual(2,stream.Read(12).Played);
        }
        [Test] public void SealedPausedStreamKeepsItsPositionAndCloseCannotBecomeNaturalCompletion()
        {
            var stream=new SpeechPcmStream(0,0);Write(stream,1,new short[]{1000,2000,3000,4000});
            var block=new float[2];stream.Render(block,1,24000,0);Assert.AreEqual(2,stream.Read(1).Played);
            stream.SetPaused(true);Assert.IsTrue(stream.TryComplete(1,out _));
            stream.Render(block,1,24000,5);Assert.That(block,Is.All.Zero);
            var paused=stream.Read(6);Assert.IsTrue(paused.Paused);Assert.IsTrue(paused.InputComplete);Assert.IsFalse(paused.SourceDrained);Assert.AreEqual(2,paused.Played);
            stream.SetPaused(false);stream.Render(block,1,24000,10);Assert.AreEqual(3000f/32768,block[0]);
            Assert.IsTrue(stream.Read(11).SourceDrained);stream.Close();stream.SetPaused(true);
            var stopped=stream.Read(12);Assert.IsTrue(stopped.Closed);Assert.IsFalse(stopped.Paused);Assert.IsFalse(stopped.SourceDrained);
            Assert.IsFalse(stream.TryComplete(1,out _));Assert.IsFalse(stream.TryWrite(2,new short[1],12,out _));
        }
        [Test] public void RenderAdmissionLinearizesPauseWithoutDroppingAnAlreadyConsumedBlock()
        {
            var stream=new SpeechPcmStream(0,0);Write(stream,1,new short[]{1000,2000,3000,4000});var block=new float[2];
            Assert.IsTrue(stream.Render(block,1,24000,0));stream.SetPaused(true);
            Assert.AreEqual(1000f/32768,block[0],"An admitted pre-pause block belongs to the output timeline");
            Assert.AreEqual(2,stream.Read(1).Played);Assert.IsFalse(stream.Render(block,1,24000,1));Assert.That(block,Is.All.Zero);
            Assert.AreEqual(2,stream.Read(2).Played);stream.SetPaused(false);
            Assert.IsTrue(stream.Render(block,1,24000,2));Assert.AreEqual(3000f/32768,block[0]);
            Assert.IsFalse(stream.Render(block,1,24000,2),"Duplicate callbacks are not admitted");
        }
        [Test] public void EmptyOrFaultedStreamCannotInventSuccessfulPlayback()
        {
            var empty=new SpeechPcmStream(0,0);Assert.IsFalse(empty.Read(100).SourceDrained);
            Assert.IsTrue(empty.TryComplete(0,out _));Assert.IsTrue(empty.Read(100).SourceDrained);Assert.AreEqual(0,empty.Read(100).Played);
            var broken=new SpeechPcmStream(0,0);Write(broken,1,new short[1]);Assert.IsTrue(broken.TryComplete(1,out _));
            broken.Render(new float[4],9,24000,0);Assert.IsTrue(broken.Read(1).Faulted);Assert.IsFalse(broken.Read(1).SourceDrained);Assert.IsFalse(broken.TryComplete(1,out _));
        }

        [Test] public void UnsupportedAudioLayoutFailsWithoutAdvancingPlayback()
        {
            var stream=new SpeechPcmStream(0,.05); Write(stream,1,new short[] {1234});
            var buffer=new float[4]; stream.Render(buffer,9,48000,0);
            Assert.IsTrue(stream.Read(1).Faulted); Assert.That(buffer,Is.All.Zero);
            Assert.AreEqual(0,stream.Read(1).Played); Assert.IsFalse(stream.TryWrite(2,new short[1],1,out _));
        }
    }
}
