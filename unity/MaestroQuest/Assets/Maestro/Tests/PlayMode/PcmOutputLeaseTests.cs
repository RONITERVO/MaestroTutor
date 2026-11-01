// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class PcmOutputLeaseTests
    {
        GameObject root;
        PcmOutputLease first,second;
        [SetUp] public void Setup() { root=new GameObject("Shared PCM ownership test"); }
        [UnityTearDown] public IEnumerator Cleanup()
        { first?.Dispose();second?.Dispose();if(root)UnityEngine.Object.Destroy(root);yield return null; }
        PcmOutputLease Open(string name) => PcmOutputLease.Open(root.transform,name,
            new SpeechPcmStream(AudioSettings.dspTime+.1,.05),false,0,96,.5f,15);
        [Test] public void RejectedConfigurationClosesTheTransferredStreamWithoutCreatingAView()
        {
            var stream=new SpeechPcmStream(AudioSettings.dspTime+.1,.05);
            Assert.Throws<ArgumentException>(()=>PcmOutputLease.Open(root.transform,"Invalid",stream,false,float.NaN,96,.5f,15));
            Assert.IsTrue(stream.Read(AudioSettings.dspTime).Closed);
            Assert.IsFalse(stream.TryWrite(1,new short[1],AudioSettings.dspTime,out _));
            Assert.AreEqual(0,root.transform.childCount);
        }
        [UnityTest] public IEnumerator ComponentLossStillReleasesTheOwnedViewAndLeavesAnotherVoiceOpen()
        {
            first=Open("First");second=Open("Second");first.Start();second.Start();
            var view=first.Source.gameObject;var carrier=first.Source.clip;var source=first.Source;
            var peer=second.Source;
            // Simulate native-view teardown losing dependent components before
            // the controlling owner next disposes its retained renderer lease.
            UnityEngine.Object.Destroy(source.GetComponent<SpeechPcmFilter>());yield return null;
            UnityEngine.Object.Destroy(source);yield return null;
            Assert.IsFalse(first.Available);Assert.IsTrue(view);
            first.Dispose();first.Dispose();
            Assert.IsTrue(first.Stream.Read(AudioSettings.dspTime).Closed);
            Assert.IsFalse(first.Stream.TryWrite(1,new short[1],AudioSettings.dspTime,out _));
            Assert.Throws<InvalidOperationException>(()=>first.Start());
            yield return null;
            Assert.IsFalse(view,"A missing AudioSource must not orphan its owned GameObject");
            Assert.IsFalse(carrier,"The generated carrier has an independent lifetime");
            Assert.IsTrue(second.Available);Assert.AreSame(peer,second.Source);
            Assert.IsTrue(second.Stream.TryWrite(1,new short[1],AudioSettings.dspTime,out var error),error);
            second.SetPaused(true);Assert.IsTrue(second.Stream.IsPaused);Assert.IsTrue(second.Available);
            second.SetGain(.1f);Assert.AreEqual(.1f,peer.volume);second.SetPaused(false);
            Assert.IsFalse(second.Stream.IsPaused);Assert.IsFalse(second.IsDrained(AudioSettings.dspTime));
        }
        [UnityTest] public IEnumerator LostSpeechViewRetiresItsGenerationAndCarrierBeforeStartingFresh()
        {
            var output=root.AddComponent<NativeSpeechOutput>();
            var generation=output.Begin(24000);
            Assert.IsTrue(output.TryWrite(generation,1,new short[2400],out var error),error);
            var source=root.GetComponentInChildren<AudioSource>();var carrier=source.clip;
            UnityEngine.Object.Destroy(source.gameObject);yield return null;output.Tick();
            Assert.IsFalse(output.TryWrite(generation,2,new short[1],out _));
            yield return null;Assert.IsFalse(carrier);
            var next=output.Begin(24000);Assert.AreNotEqual(generation,next);
            Assert.IsFalse(output.TryWrite(generation,1,new short[1],out _));
            Assert.IsTrue(output.TryWrite(next,1,new short[1],out error),error);
            Assert.AreEqual(1,root.GetComponentsInChildren<AudioSource>().Length);
            output.Stop();yield return null;Assert.AreEqual(0,root.transform.childCount);
        }
    }
}
