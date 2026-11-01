// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings=UnityEngine.AudioSettings;
namespace Maestro.AudioEvaluation
{
    public sealed partial class OwnedLifecycleTests
    {
        [UnityTest] public IEnumerator ListenerReferencePlayerEntryUsesActualMutedAudioCallbacks()
        {
            var task=EchoReferencePlayerProbe.Run();while(!task.IsCompleted)yield return null;
            Assert.IsFalse(task.IsFaulted,task.Exception?.ToString());var result=task.Result;
            Assert.AreEqual("passed",result.status,result.issue);Assert.GreaterOrEqual(result.frames,25);
            Assert.Greater(result.energy,.001);Assert.IsTrue(result.referenceClosed);
            Debug.Log("NATIVE_REFERENCE_PLAYER_PROBE "+JsonUtility.ToJson(result));
        }
        [UnityTest] public IEnumerator ListenerReferenceContainsNativeReflectionsAndIndependentVoicesExactly()
        {
            var first=Begin(0,dry:0);var second=Begin(1,dry:0);
            Queue(first,440);Queue(second,660);yield return PcmReady(0);yield return PcmReady(1);
            yield return new WaitForSecondsRealtime(.25f);capture.Clear();
            using var reference=listener.GetComponent<RoomAudioTap>().BeginReference();
            var frames=new List<(double Time,float[] Samples)>();double end=Time.realtimeSinceStartupAsDouble+.25;
            while(Time.realtimeSinceStartupAsDouble<end)
            {
                var data=new float[reference.FrameSamples];EchoReferenceRead state;
                while((state=reference.Read(data,out var frame))==EchoReferenceRead.Frame)
                { frames.Add((frame.RenderTime,data));data=new float[reference.FrameSamples]; }
                Assert.AreEqual(EchoReferenceRead.Waiting,state);yield return null;
            }
            var mixed=capture.Read();double start=capture.Started;int compared=0;
            var left=new List<float>();
            foreach(var frame in frames)
            {
                int offset=(int)Math.Round((frame.Time-start)*reference.Rate);
                if(offset<0 || offset+frame.Samples.Length/2>mixed.samples.Length)continue;
                for(int i=0;i<frame.Samples.Length;i+=2)
                {
                    Assert.AreEqual(mixed.samples[offset+i/2],frame.Samples[i]);
                    Assert.AreEqual(mixed.right[offset+i/2],frame.Samples[i+1]);left.Add(frame.Samples[i]);
                }
                compared++;
            }
            Assert.Greater(compared,10);var samples=left.ToArray();
            Assert.Greater(Power(samples,reference.Rate,440),1e-10);Assert.Greater(Power(samples,reference.Rate,660),1e-10);
            reference.Dispose();Assert.IsTrue(sink.Owner.StopPcm(PreparedGraph.VoiceIds[0],first));
            yield return Measure("reference-owner-closed-other-wet-voice-alive");Tone(660);
            Assert.IsTrue(second.Open);Assert.Greater(second.Read(AudioSettings.dspTime).Played,0);
        }
        [UnityTest] public IEnumerator ListenerReferenceKeepsNativeTailAfterSourceConsumption()
        {
            var voice=Begin(0,dry:0);yield return PcmReady(0);
            using var reference=listener.GetComponent<RoomAudioTap>().BeginReference();
            Queue(voice,440,1);Assert.IsTrue(voice.Complete(1,out _));
            var data=new float[reference.FrameSamples];double sourceEnded=double.PositiveInfinity,energy=0;
            double end=Time.realtimeSinceStartupAsDouble+5;int tailFrames=0;
            while(Time.realtimeSinceStartupAsDouble<end && !voice.RendererDrained(AudioSettings.dspTime))
            {
                if(double.IsPositiveInfinity(sourceEnded) && voice.Read(AudioSettings.dspTime).SourceDrained)sourceEnded=AudioSettings.dspTime;
                EchoReferenceRead state;
                while((state=reference.Read(data,out var frame))==EchoReferenceRead.Frame)
                    if(frame.RenderTime>=sourceEnded){energy+=Energy(data);tailFrames++;}
                Assert.AreEqual(EchoReferenceRead.Waiting,state);yield return null;
            }
            Assert.IsTrue(voice.RendererDrained(AudioSettings.dspTime));Assert.IsTrue(double.IsFinite(sourceEnded));
            Assert.Greater(tailFrames,0);Assert.Greater(energy,1e-8,"The reference must include reflected output after input is consumed");
        }
    }
}
