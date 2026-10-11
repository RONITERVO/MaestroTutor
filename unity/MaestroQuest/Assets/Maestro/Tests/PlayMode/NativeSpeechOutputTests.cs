// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class NativeSpeechOutputTests
    {
        GameObject root;
        NativeSpeechOutput output;
        double clock;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Speech output test"); output = root.AddComponent<NativeSpeechOutput>();
            clock = AudioSettings.dspTime + 5; output.Clock = () => clock; output.TailOverride = .05;
        }
        [UnityTearDown] public IEnumerator TearDown() { if (root) UnityEngine.Object.Destroy(root); yield return null; }
        void Write(long generation, long sequence, short[] pcm)
        {
            Assert.IsTrue(output.TryWrite(generation, sequence, pcm, out var error), error);
            // Controlled render time verifies the native PCM path, not hearing.
            foreach (var source in root.GetComponentsInChildren<AudioSource>()) source.volume = 0;
        }
        [UnityTest] public IEnumerator PcmIsMonoCopiedAndCompletionIncludesTheDspTail()
        {
            var generation = output.Begin(24000);
            var pcm = new short[2400]; pcm[0] = short.MinValue; pcm[1] = short.MaxValue;
            Write(generation, 1, pcm); pcm[0] = 0;
            var source = root.GetComponentInChildren<AudioSource>();
            Assert.AreEqual(1, source.clip.channels); Assert.AreEqual(24000, source.clip.frequency);
            Assert.AreEqual(1, source.spatialBlend); Assert.AreEqual(0, source.dopplerLevel);
            Assert.IsTrue(source.loop); Assert.IsTrue(source.spatializePostEffects);
            var samples = new float[2400]; output.Render(samples, 1, 24000, clock + .1);
            Assert.AreEqual(-1f, samples[0]); Assert.AreEqual(32767f / 32768f, samples[1]);
            Assert.AreEqual(0, output.Read().playedSamples);
            clock += .16; Assert.That(output.Read().playedSamples, Is.InRange(239, 241));
            clock += .08; Assert.Less(output.Read().playedSamples, 2400);
            clock += .011; Assert.AreEqual(2400, output.Read().playedSamples);
            yield return null; Assert.AreSame(source,root.GetComponentInChildren<AudioSource>());
        }
        [UnityTest] public IEnumerator DuplicateOutOfOrderStoppedAndDisabledPacketsNeverPlay()
        {
            var generation = output.Begin(24000); Write(generation, 1, new short[2400]);
            Assert.IsFalse(output.TryWrite(generation, 1, new short[2400], out _));
            Assert.IsFalse(output.TryWrite(generation, 3, new short[2400], out _));
            Assert.AreEqual(2400, output.Read().submittedSamples);
            output.Stop(); Assert.IsFalse(output.TryWrite(generation, 2, new short[2400], out _));
            var newer = output.Begin(24000); Assert.AreNotEqual(generation, newer);
            Assert.IsFalse(output.TryWrite(generation, 1, new short[2400], out _));
            Write(newer, 1, new short[2400]); root.SendMessage("OnApplicationFocus", false);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            Assert.Throws<InvalidOperationException>(() => output.Begin(24000));
            root.SendMessage("OnApplicationFocus", true);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            newer = output.Begin(24000); Write(newer, 1, new short[2400]);
            root.SendMessage("AudioConfigurationChanged", true);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            newer = output.Begin(24000); Write(newer, 1, new short[2400]); root.SetActive(false);
            Assert.IsFalse(output.TryWrite(newer, 2, new short[2400], out _));
            Assert.Throws<InvalidOperationException>(() => output.Begin(24000));
            yield return null; Assert.AreEqual(0, root.GetComponentsInChildren<AudioSource>(true).Length);
        }
        [UnityTest] public IEnumerator QueueBoundsRejectBeforeAllocationAndLateAudioCountsNoGapSamples()
        {
            var generation = output.Begin(24000);
            Assert.Throws<ArgumentException>(() => output.Begin(48000));
            Assert.IsFalse(output.TryWrite(generation, 1, new short[4801], out _));
            for (int i = 1; i <= 40; i++) Write(generation, i, new short[4800]);
            Assert.IsFalse(output.TryWrite(generation, 41, new short[1], out var error));
            Assert.That(error, Does.Contain("full"));
            Assert.AreEqual(1, root.GetComponentsInChildren<AudioSource>().Length);
            output.Render(new float[192000],1,24000,clock+.1);
            clock += 12; Assert.AreEqual(192000, output.Read().playedSamples);
            Write(generation, 41, new short[2400]); Assert.AreEqual(192000, output.Read().playedSamples);
            output.Render(new float[2400],1,24000,clock+.1);
            clock += .26; Assert.AreEqual(194400, output.Read().playedSamples);
            yield return null;
        }
        [UnityTest] public IEnumerator AvatarOwnsTheEmitterAndFollowsPosedHeadAndRootScale()
        {
            var avatar = root.AddComponent<MaestroAvatar>(); var voice = avatar.SpeechOutput;
            Assert.IsNotNull(voice); Assert.IsNotNull(avatar.PoseRig.Bone(PoseJoint.Head));
            var head = avatar.PoseRig.Bone(PoseJoint.Head);
            root.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 60, 0));
            root.transform.localScale = Vector3.one * 1.5f;
            head.localRotation *= Quaternion.Euler(0, 25, 0);
            voice.FollowAnchor();
            Assert.That(Vector3.Distance(head.position + head.rotation * new Vector3(0, .12f, .12f), voice.transform.position), Is.LessThan(.0001f));
            voice.ConfigureAnchor(null, root.transform, Vector3.zero, new Vector3(0, 1, 0));
            Assert.That(Vector3.Distance(root.transform.TransformPoint(Vector3.up), voice.transform.position), Is.LessThan(.0001f));
            yield return null;
        }

        [UnityTest] public IEnumerator ActualDspCallbacksRenderOneContinuousVoiceAndStopClearsIt()
        {
            float previousVolume=AudioListener.volume; bool previousPause=AudioListener.pause;
            var listenerObject=new GameObject("Muted speech probe listener",typeof(AudioListener));
            var existing=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled=new bool[existing.Length];
            for(int i=0;i<existing.Length;i++) { enabled[i]=existing[i].enabled; existing[i].enabled=existing[i].gameObject==listenerObject; }
            AudioListener.volume=0; AudioListener.pause=false;
            try
            {
                output.Clock=()=>AudioSettings.dspTime; output.TailOverride=null;
                var generation=output.Begin(24000); var source=root.GetComponentInChildren<AudioSource>();
                var probe=source.gameObject.AddComponent<SpeechRenderProbe>();
                for(int packet=0;packet<3;packet++)
                {
                    var pcm=new short[4800];
                    for(int i=0;i<pcm.Length;i++) pcm[i]=(short)Math.Round(Math.Sin((packet*4800+i)*440.0/24000*Math.PI*2)*3276);
                    Assert.IsTrue(output.TryWrite(generation,packet+1,pcm,out var error),error);
                }
                double deadline=Time.realtimeSinceStartupAsDouble+8;
                while(output.Read().playedSamples<14400 && Time.realtimeSinceStartupAsDouble<deadline) yield return null;
                Assert.AreEqual(14400,output.Read().playedSamples,"Real audio callbacks did not complete queued PCM");
                Assert.AreSame(source,root.GetComponentInChildren<AudioSource>());
                Assert.AreEqual(1,root.GetComponentsInChildren<AudioSource>().Length);
                var result=probe.Read(); Assert.Greater(result.Blocks,1); Assert.That(result.Channels,Is.InRange(1,2));
                Assert.That(result.Peak,Is.InRange(.09f,.11f)); Assert.Less(result.Step,.025f,"Unexpected discontinuity between packets");
                Assert.IsTrue(source.isPlaying,"The carrier should remain alive during an underrun");
                output.Stop(); Assert.IsFalse(source.isPlaying);
                Assert.IsFalse(output.TryWrite(generation,4,new short[240],out _));
                yield return null;
            }
            finally
            {
                output.Stop(); UnityEngine.Object.Destroy(listenerObject);
                for(int i=0;i<existing.Length;i++) if(existing[i]) existing[i].enabled=enabled[i];
                AudioListener.pause=previousPause; AudioListener.volume=previousVolume;
            }
        }

        [UnityTest] public IEnumerator NativeHrtfRendersAtTheListenerAndRespondsToHeadRotation()
        {
            float previousVolume = AudioListener.volume; bool previousPause = AudioListener.pause;
            var listenerObject = new GameObject("Muted HRTF listener", typeof(AudioListener), typeof(SpeechListenerProbe));
            var probe = listenerObject.GetComponent<SpeechListenerProbe>();
            var existing = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabled = new bool[existing.Length];
            for (int i=0; i<existing.Length; i++) { enabled[i]=existing[i].enabled; existing[i].enabled=existing[i].gameObject==listenerObject; }
            AudioListener.volume = 1; AudioListener.pause = false; // Probe clears the final mix.
            try
            {
                Assert.AreEqual(SpeechSpatializer.PluginName, AudioSettings.GetSpatializerPluginName());
                Assert.AreEqual(32, MetaXRAudioSettings.Instance.voiceLimit);
                output.Clock = () => AudioSettings.dspTime; output.TailOverride = null;
                root.transform.position = new Vector3(2,0,0);
                for (int direction=0; direction<2; direction++)
                {
                    listenerObject.transform.rotation = Quaternion.Euler(0,direction*180,0);
                    probe.Reset();
                    long generation = output.Begin(24000);
                    var source = root.GetComponentInChildren<AudioSource>();
                    var spatializer = source.GetComponent<MetaXRAudioSource>();
                    Assert.IsTrue(spatializer.EnableSpatialization); Assert.IsFalse(spatializer.EnableAcoustics);
                    Assert.IsTrue(source.spatializePostEffects);
                    var random = new System.Random(124);
                    for (int packet=0; packet<4; packet++)
                    {
                        var pcm = new short[4800];
                        for (int i=0; i<pcm.Length; i++) pcm[i]=(short)random.Next(-1000,1001);
                        Assert.IsTrue(output.TryWrite(generation,packet+1,pcm,out var error),error);
                    }
                    double deadline = Time.realtimeSinceStartupAsDouble+8;
                    float peakVoices = 0;
                    while (output.Read().playedSamples<19200 && Time.realtimeSinceStartupAsDouble<deadline)
                    {
                        if (source.GetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_READONLY_NUM_VOICES,out var activeVoices))
                            peakVoices = Math.Max(peakVoices,activeVoices);
                        yield return null;
                    }
                    Assert.AreEqual(19200,output.Read().playedSamples);
                    var result = probe.Read();
                    Debug.Log($"MAESTRO_HRTF_PROBE: rotation={direction*180}; blocks={result.Blocks}; channels={result.Channels}; left={result.Left}; right={result.Right}; peakVoices={peakVoices}");
                    Assert.Greater(peakVoices,0,"No native spatial voice was active during playback");
                    Assert.IsTrue(source.GetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_DISABLE_RFL,out var disabledReflections));
                    Assert.AreEqual(1,disabledReflections);
                    Assert.Greater(result.Blocks,1); Assert.AreEqual(2,result.Channels);
                    Assert.Greater(Math.Min(result.Left,result.Right),.0001,"The listener never received both rendered channels");
                    if (direction==0) Assert.Greater(result.Right,result.Left*1.1,"Right-side voice did not favor the right ear");
                    else Assert.Greater(result.Left,result.Right*1.1,"Head rotation did not reverse the heard direction");
                    output.Stop(); yield return null;
                }
            }
            finally
            {
                output.Stop(); UnityEngine.Object.Destroy(listenerObject);
                for(int i=0;i<existing.Length;i++) if(existing[i]) existing[i].enabled=enabled[i];
                AudioListener.pause=previousPause; AudioListener.volume=previousVolume;
            }
        }
    }
}
