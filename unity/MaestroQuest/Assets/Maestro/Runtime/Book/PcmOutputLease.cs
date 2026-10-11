// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;

namespace Maestro.Quest.Book
{
    // One transient playback owner shared by speech and authored world audio.
    // Source definitions, provider generations and decode reservations stay with
    // their callers. The renderer owns its view even if a component disappears.
    internal sealed class PcmOutputLease : IDisposable
    {
        internal readonly SpeechPcmStream Stream;
        internal AudioSource Source { get; private set; }
        GameObject view;
        AudioClip carrier;
        SpeechPcmFilter filter;
        bool closed, started;
        internal bool Available => !closed && view && view.activeInHierarchy && Source && Source.enabled
            && filter && filter.isActiveAndEnabled && carrier && Source.clip==carrier;
        internal RoomAudioOutput ReflectionOutput => Source && Source.outputAudioMixerGroup
            ? view.GetComponentInParent<RoomAudioOutput>() : null;
        internal static double DeviceTail
        {
            get {
                AudioSettings.GetDSPBufferSize(out var length,out var buffers);
                return Math.Max(.02,(double)length*Math.Max(1,buffers)/Math.Max(8000,AudioSettings.outputSampleRate)+.02);
            }
        }
        PcmOutputLease(SpeechPcmStream stream) { Stream=stream??throw new ArgumentNullException(nameof(stream)); }
        internal static PcmOutputLease Open(Transform parent,string name,SpeechPcmStream stream,
            bool spatial,float gain,int priority,float minDistance,float maxDistance)
        {
            var result=new PcmOutputLease(stream);
            try { result.Prepare(parent,name,spatial,gain,priority,minDistance,maxDistance);return result; }
            catch { result.Dispose();throw; }
        }
        void Prepare(Transform parent,string name,bool spatial,float gain,int priority,float minDistance,float maxDistance)
        {
            if(!parent||string.IsNullOrEmpty(name)||!float.IsFinite(gain)||gain<0||gain>1||priority<0||priority>256
                ||!float.IsFinite(minDistance)||!float.IsFinite(maxDistance)||minDistance<=0||maxDistance<minDistance)
                throw new ArgumentException("Invalid native PCM emitter configuration.");
            int rate=AudioSettings.outputSampleRate;
            if(rate<SpeechPcmStream.Rate||rate>192000)throw new InvalidOperationException("The audio output rate is unsupported.");
            view=new GameObject(name);view.transform.SetParent(parent,false);
            Source=view.AddComponent<AudioSource>();
            Source.playOnAwake=false;Source.loop=true;Source.volume=gain;Source.priority=priority;
            Source.spatialBlend=spatial?1:0;Source.dopplerLevel=0;Source.spread=0;
            Source.minDistance=minDistance;Source.maxDistance=maxDistance;Source.rolloffMode=AudioRolloffMode.Logarithmic;
            if(spatial)SpeechSpatializer.Configure(Source);
            carrier=AudioClip.Create(name+" carrier",1024,1,SpeechPcmStream.Rate,false);Source.clip=carrier;
            filter=view.AddComponent<SpeechPcmFilter>();filter.Bind(Stream,rate);
        }
        internal void Start()
        {
            RequireAvailable();if(started)return;started=true;Source.Play();
        }
        internal void SetPose(Vector3 position,Quaternion rotation)
        { RequireAvailable();view.transform.SetPositionAndRotation(position,rotation); }
        internal void SetPaused(bool value)
        {
            RequireAvailable();Stream.SetPaused(value);
            if(value)Source.Pause();else Source.UnPause();
        }
        internal void SetGain(float value)
        {
            if(!float.IsFinite(value)||value<0||value>1)throw new ArgumentOutOfRangeException(nameof(value));
            RequireAvailable();Source.volume=value;
        }
        // Current direct renderer has no independently owned reflection tail.
        // A replacement renderer must include its downstream completion here,
        // while preserving the source receipt and the exact owning lease.
        internal bool IsDrained(double now) => Available && Stream.Read(now).SourceDrained;
        void RequireAvailable()
        { if(!Available)throw new InvalidOperationException("The native PCM renderer is unavailable."); }
        public void Dispose()
        {
            if(closed)return;closed=true;
            Stream.Close();if(filter)filter.Stop();filter=null;
            if(Source){Source.Stop();Source.clip=null;}Source=null;
            // Retain the root independently of AudioSource/Filter destruction.
            if(view)DestroyOwned(view);view=null;
            if(carrier)DestroyOwned(carrier);carrier=null;
        }
        static void DestroyOwned(UnityEngine.Object value)
        { if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value); }
    }
}
