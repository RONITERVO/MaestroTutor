// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Book;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    // Test-only filter AFTER the app tap. Collect the exact rendered samples and
    // silence them before any speaker. All recordings remain bounded in memory.
    public sealed class EchoListenerProbe : MonoBehaviour
    {
        readonly object gate=new();
        readonly float[] samples=new float[192000];
        int count;
        double firstTime;
        internal void Reset() { lock(gate) { Array.Clear(samples,0,samples.Length);count=0; } }
        void OnAudioFilterRead(float[] data,int channels)
        {
            lock(gate)
            {
                if(count==0)firstTime=AudioSettings.dspTime;
                int length=Math.Min(data.Length,samples.Length-count);
                Array.Copy(data,0,samples,count,length);count+=length;
            }
            Array.Clear(data,0,data.Length);
        }
        internal (float[] Samples,double Start) Read()
        { lock(gate) { var copy=new float[count];Array.Copy(samples,copy,count);return(copy,firstTime); } }
    }
    public sealed class EchoListenerTests
    {
        GameObject root,viewer;
        AudioListener listener;
        RoomAudioTap tap;
        EchoListenerProbe probe;
        AudioListener[] others;
        bool[] enabled;
        float volume;
        bool paused;
        readonly List<AudioClip> clips=new();
        [UnitySetUp] public IEnumerator Setup()
        {
            others=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);enabled=new bool[others.Length];
            for(int i=0;i<others.Length;i++){enabled[i]=others[i].enabled;others[i].enabled=false;}
            volume=AudioListener.volume;paused=AudioListener.pause;AudioListener.volume=1;AudioListener.pause=false;
            root=new GameObject("Reference test voices");viewer=new GameObject("Reference test listener");
            listener=viewer.AddComponent<AudioListener>();tap=viewer.AddComponent<RoomAudioTap>();probe=viewer.AddComponent<EchoListenerProbe>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(viewer);
            foreach(var clip in clips)UnityEngine.Object.Destroy(clip);clips.Clear();yield return null;
            for(int i=0;i<others.Length;i++)if(others[i])others[i].enabled=enabled[i];AudioListener.volume=volume;AudioListener.pause=paused;
        }
        AudioSource Tone(int frequency,float pan)
        {
            int rate=AudioSettings.outputSampleRate;var samples=new float[rate];
            for(int i=0;i<samples.Length;i++)samples[i]=(float)(.05*Math.Sin(2*Math.PI*frequency*i/rate));
            var clip=AudioClip.Create("Reference test tone",rate,1,rate,false);clips.Add(clip);clip.SetData(samples,0);
            var voice=new GameObject("Tone "+frequency).AddComponent<AudioSource>();voice.transform.SetParent(root.transform,false);
            voice.clip=clip;voice.loop=true;voice.panStereo=pan;voice.Play();return voice;
        }
        [UnityTest] public IEnumerator ListenerReferenceMatchesFinalStereoMixAndClosingItDoesNotStopOtherSound()
        {
            Assert.AreEqual(AudioSpeakerMode.Stereo,AudioSettings.GetConfiguration().speakerMode);
            var a=Tone(440,-.8f);var b=Tone(660,.8f);yield return new WaitForSecondsRealtime(.1f);
            probe.Reset();using var reference=tap.BeginReference();var frames=new List<(double Time,float[] Samples)>();
            double end=Time.realtimeSinceStartupAsDouble+.25;
            while(Time.realtimeSinceStartupAsDouble<end)
            {
                var data=new float[reference.FrameSamples];EchoReferenceRead state;
                while((state=reference.Read(data,out var frame))==EchoReferenceRead.Frame)
                { frames.Add((frame.RenderTime,data));data=new float[reference.FrameSamples]; }
                Assert.AreEqual(EchoReferenceRead.Waiting,state,"A live listener should supply a continuous reference");yield return null;
            }
            var captured=probe.Read();Assert.Greater(frames.Count,10);double energy=0;int compared=0;
            foreach(var frame in frames)
            {
                int start=(int)Math.Round((frame.Time-captured.Start)*reference.Rate)*2;
                if(start<0 || start+frame.Samples.Length>captured.Samples.Length)continue;
                for(int i=0;i<frame.Samples.Length;i++){Assert.AreEqual(captured.Samples[start+i],frame.Samples[i]);energy+=frame.Samples[i]*(double)frame.Samples[i];}
                compared++;
            }
            Assert.Greater(compared,10);Assert.Greater(energy,.01,"Capture the actual voices, not a silent source stub");
            a.Stop();reference.Dispose();probe.Reset();yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue(b.isPlaying);double remaining=0;foreach(float sample in probe.Read().Samples)remaining+=sample*(double)sample;
            Assert.Greater(remaining,.01,"Closing only the reference must leave remaining world audio playing");
        }
        [UnityTest] public IEnumerator ListenerReferenceOwnershipSurvivesMonitorReplacementAndRejectsAliases()
        {
            using var first=tap.BeginReference();Assert.Throws<InvalidOperationException>(()=>tap.BeginReference());
            var monitor=new AcousticOutputMonitor();tap.Bind(monitor,AudioSettings.outputSampleRate);Assert.IsFalse(first.Closed);
            first.Dispose();using var second=tap.BeginReference();first.Dispose();Assert.IsFalse(second.Closed);
            var data=new float[second.FrameSamples];yield return new WaitForSecondsRealtime(.05f);
            Assert.AreEqual(EchoReferenceRead.Frame,second.Read(data,out _));
        }
        [UnityTest] public IEnumerator ListenerLossPauseFocusAndDisableCloseWithoutAutomaticReopening()
        {
            foreach(string signal in new[]{"listener","pause","focus","component","global-pause"})
            {
                using var reference=tap.BeginReference();
                if(signal=="listener")listener.enabled=false;
                else if(signal=="pause")viewer.SendMessage("OnApplicationPause",true);
                else if(signal=="focus")viewer.SendMessage("OnApplicationFocus",false);
                else if(signal=="component")tap.enabled=false;
                else AudioListener.pause=true;
                yield return null;yield return null;
                Assert.IsTrue(reference.Closed,signal);Assert.Throws<InvalidOperationException>(()=>tap.BeginReference());
                listener.enabled=true;tap.enabled=true;AudioListener.pause=false;
                viewer.SendMessage("OnApplicationPause",false);viewer.SendMessage("OnApplicationFocus",true);
                yield return null;Assert.IsTrue(reference.Closed,"Resuming must not reacquire capture");
            }
        }
        [UnityTest] public IEnumerator ActualAudioResetClosesOldReferenceAndExplicitNewOwnerUsesCurrentFormat()
        {
            using var old=tap.BeginReference();var config=AudioSettings.GetConfiguration();Assert.IsTrue(AudioSettings.Reset(config));
            yield return null;Assert.IsTrue(old.Closed);using var next=tap.BeginReference();Assert.AreEqual(AudioSettings.outputSampleRate,next.Rate);
            var data=new float[next.FrameSamples];yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(EchoReferenceRead.Frame,next.Read(data,out _));Assert.IsTrue(old.Closed);
        }
    }
}
