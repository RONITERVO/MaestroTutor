// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Optional isolated desktop evaluation. No app, headset, microphone or provider access.
#if STEAMAUDIO_ENABLED
using System;
using System.Collections;
using NUnit.Framework;
using SteamAudio;
using UnityEngine;
using UnityEngine.TestTools;
using AudioSettings = UnityEngine.AudioSettings;
using UVector = UnityEngine.Vector3;
using SVector = SteamAudio.Vector3;
using SMaterial = SteamAudio.Material;

namespace Maestro.AudioEvaluation
{
    public static class Configure
    {
        public static void Run()
        {
#if UNITY_EDITOR
            var settings = SteamAudioSettings.Singleton;
            settings.EnableValidation = true;
            settings.SOFAFiles = Array.Empty<SOFAFile>();
            settings.realTimeRays = 1024;
            settings.realTimeBounces = 8;
            settings.realTimeDuration = .25f;
            settings.realTimeAmbisonicOrder = 1;
            settings.realTimeMaxSources = 4;
            settings.realTimeCPUCoresPercentage = 1;
            settings.simulationUpdateInterval = .05f;
            settings.hybridReverbTransitionTime = .125f;
            UnityEditor.EditorUtility.SetDirty(settings);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("MAESTRO_AUDIO_CANDIDATE_CONFIGURED");
#endif
        }
    }
    public sealed class MutedListener : MonoBehaviour
    {
        readonly object gate = new();
        double energy;
        int blocks;
        bool finite = true;
        void OnAudioFilterRead(float[] samples, int channels)
        {
            double value = 0; bool valid = true;
            foreach (float sample in samples)
            {
                valid &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                value += sample * (double)sample;
            }
            lock (gate) { energy += value; blocks++; finite &= valid; }
            Array.Clear(samples, 0, samples.Length);
        }
        public (double energy, int blocks, bool finite) Read(bool reset = false)
        {
            lock (gate)
            {
                var value = (energy, blocks, finite);
                if (reset) { energy = 0; blocks = 0; finite = true; }
                return value;
            }
        }
    }
    public sealed class CandidateTests
    {
        GameObject root, listener;
        AudioClip clip;
        AudioSource first, second;
        SteamAudioSource a, b;
        StaticMesh room, wall;
        SteamAudio.Scene scene;
        MutedListener probe;
        AudioListener[] otherListeners;
        bool[] listenerEnabled;
        float originalVolume;
        bool originalPause;
        double measured;

        [UnitySetUp] public IEnumerator Setup()
        {
            // This project contains only this fixture and the pinned vendor package.
            Assert.AreEqual("Steam Audio Spatializer", AudioSettings.GetSpatializerPluginName());
            Assert.IsNotNull(SteamAudioManager.Singleton);
            originalVolume = AudioListener.volume; originalPause = AudioListener.pause;
            AudioListener.volume = 1; AudioListener.pause = false;
            otherListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            listenerEnabled = new bool[otherListeners.Length];
            for (int i=0;i<otherListeners.Length;i++)
            { listenerEnabled[i]=otherListeners[i].enabled; otherListeners[i].enabled=false; }
            root = new GameObject("Isolated candidate room");
            listener = new GameObject("Muted candidate listener", typeof(AudioListener), typeof(MutedListener));
            listener.transform.position = new UVector(-1,0,0);
            probe = listener.GetComponent<MutedListener>();
            SteamAudioManager.NotifyAudioListenerChangedTo(listener.transform);
            scene = SteamAudioManager.CurrentScene;
            clip = CreateClip();
            (first,a) = Voice("First",new UVector(1,0,0));
            (second,b) = Voice("Second",new UVector(1,0,.4f));
            yield return null;
        }
        static AudioClip CreateClip()
        {
            var pcm = new float[24000]; var random = new System.Random(941);
            for (int i=0; i<pcm.Length; i++) pcm[i] = (float)(random.NextDouble() - .5) * .08f;
            var result = AudioClip.Create("Synthetic evaluation noise", pcm.Length, 1, 24000, false);
            result.SetData(pcm, 0); return result;
        }
        (AudioSource,SteamAudioSource) Voice(string label,UVector position)
        {
            var go = new GameObject(label); go.SetActive(false); go.transform.SetParent(root.transform);
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>(); audio.playOnAwake=false; audio.clip=clip;
            audio.spatialBlend=1; audio.spatialize=true; audio.loop=true; audio.dopplerLevel=0;
            var source = go.AddComponent<SteamAudioSource>();
            source.reflections=true; source.applyHRTFToReflections=true; source.occlusion=true;
            go.SetActive(true); audio.Play(); return (audio,source);
        }
        StaticMesh Mesh(SVector[] vertices,int[] indices)
        {
            var triangles = new Triangle[indices.Length/3];
            for (int i=0;i<triangles.Length;i++) triangles[i]=new Triangle {index0=indices[3*i],index1=indices[3*i+1],index2=indices[3*i+2]};
            var material = new SMaterial {absorptionLow=.2f,absorptionMid=.2f,absorptionHigh=.2f,scattering=.5f};
            var mesh = new StaticMesh(SteamAudioManager.Context,scene,vertices,triangles,new int[triangles.Length],new[]{material});
            mesh.AddToScene(scene); SteamAudioManager.ScheduleCommitScene(); return mesh;
        }
        static SVector V(float x,float y,float z) => new SVector{x=x,y=y,z=z};
        void AddRoom() => room=Mesh(new[]{V(-3,-3,-3),V(3,-3,-3),V(3,3,-3),V(-3,3,-3),V(-3,-3,3),V(3,-3,3),V(3,3,3),V(-3,3,3)},new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5});
        void Remove(ref StaticMesh mesh)
        {
            if (mesh==null)return;
            mesh.RemoveFromScene(scene); mesh.Release(); mesh=null; SteamAudioManager.ScheduleCommitScene();
        }
        IEnumerator Measure(string label)
        {
            yield return new WaitForSecondsRealtime(.6f);
            probe.Read(true); yield return new WaitForSecondsRealtime(.3f);
            var sample=probe.Read();
            Assert.Greater(sample.blocks,0,"Actual Unity listener callbacks required");
            Assert.IsTrue(sample.finite,"Invalid native output samples");
            measured=sample.energy;
            Debug.Log("CANDIDATE_MEASURE "+label+" energy="+measured+" blocks="+sample.blocks);
        }
        [UnityTest] public IEnumerator EditableGeometryAndIndependentVoicesRenderThroughUnity()
        {
            yield return Measure("empty-direct"); Assert.Greater(measured,1e-7);
            Assert.Greater(a.occlusionValue,.99f);
            wall=Mesh(new[]{V(0,-3,-3),V(0,3,-3),V(0,3,3),V(0,-3,3)},new[]{0,1,2,0,2,3});
            yield return Measure("blocked-direct"); Assert.Less(a.occlusionValue,.01f);
            Remove(ref wall); yield return Measure("wall-removed"); Assert.Greater(a.occlusionValue,.99f);
            AddRoom(); a.directMixLevel=b.directMixLevel=0;
            yield return Measure("reflection-only-two-voices"); Assert.Greater(measured,1e-7);
            UnityEngine.Object.Destroy(first.gameObject); first=null;
            yield return Measure("first-voice-destroyed"); Assert.Greater(measured,1e-7);
            Assert.IsTrue(second.isPlaying); double enclosed=measured;
            Remove(ref room); yield return Measure("room-deleted"); Assert.Less(measured,enclosed*.01);
            AddRoom(); yield return Measure("room-reconstructed"); Assert.Greater(measured,1e-7);
        }
        [UnityTest] public IEnumerator ExistingVoiceSurvivesSameConfigurationAudioReset()
        {
            AddRoom(); a.directMixLevel=b.directMixLevel=0;
            yield return Measure("before-audio-reset"); Assert.Greater(measured,1e-7);
            var oldSource=b.GetSource().Get();
            Assert.IsTrue(AudioSettings.Reset(AudioSettings.GetConfiguration()));
            // Recreate the ephemeral PCM clip: Unity documents script clips as
            // lost on audio reset. Keep the logical voice/component under test.
            UnityEngine.Object.Destroy(clip); clip=CreateClip();
            first.clip=clip; second.clip=clip; first.Play(); second.Play();
            yield return Measure("after-audio-reset");
            Assert.AreEqual(oldSource,b.GetSource().Get());
            Assert.Greater(measured,1e-7,"Existing reflected voice did not recover after the reset");
        }
        [UnityTest] public IEnumerator ChangedDeviceFormatUpdatesSimulationConfiguration()
        {
            AddRoom(); a.directMixLevel=b.directMixLevel=0;
            yield return Measure("before-format-change"); Assert.Greater(measured,1e-7);
            var original=AudioSettings.GetConfiguration();
            try
            {
                var requested=original; requested.sampleRate=original.sampleRate==24000?48000:24000;
                Assert.IsTrue(AudioSettings.Reset(requested),"Apply a real supported Unity audio format change");
                UnityEngine.Object.Destroy(clip); clip=CreateClip();
                first.clip=clip; second.clip=clip; first.Play(); second.Play();
                yield return Measure("after-format-change");
                var actual=AudioSettings.GetConfiguration();
                Debug.Log("CANDIDATE_FORMAT actual="+actual.sampleRate+" cached="+SteamAudioManager.AudioSettings.samplingRate);
                Assert.AreNotEqual(original.sampleRate,actual.sampleRate,"The requested format change must really occur");
                Assert.AreEqual(actual.sampleRate,SteamAudioManager.AudioSettings.samplingRate,"Simulation retains the previous device rate");
                Assert.Greater(measured,1e-7);
            }
            finally { AudioSettings.Reset(original); }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Remove(ref wall); Remove(ref room);
            if(root) UnityEngine.Object.Destroy(root);
            yield return new WaitForSecondsRealtime(.5f);
            if(listener) UnityEngine.Object.Destroy(listener);
            if(clip) UnityEngine.Object.Destroy(clip);
            if (otherListeners != null)
                for (int i=0;i<otherListeners.Length;i++)
                    if (otherListeners[i]) otherListeners[i].enabled=listenerEnabled[i];
            AudioListener.volume=originalVolume; AudioListener.pause=originalPause;
            yield return null;
        }
    }
}
#endif
