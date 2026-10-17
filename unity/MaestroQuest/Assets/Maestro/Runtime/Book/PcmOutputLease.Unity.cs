// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Book
{
    internal sealed partial class PcmOutputLease
    {
        // The application keeps its current Unity/Meta direct renderer by default.
        // Optional backend evaluation copies the independent lease and selector.
        static PcmOutputLease(){CreateDefault=UnityPcmOutput.Open;}
        internal RoomAudioOutput ReflectionOutput=>Source&&Source.outputAudioMixerGroup
            ?Source.GetComponentInParent<RoomAudioOutput>():null;
        sealed class UnityPcmOutput : IPcmOutput
        {
            readonly SpeechPcmStream stream;
            GameObject view;AudioClip carrier;SpeechPcmFilter filter;
            public AudioSource Source {get;private set;}
            bool closed;
            public bool Available=>!closed&&view&&view.activeInHierarchy&&Source&&Source.enabled
                &&filter&&filter.isActiveAndEnabled&&carrier&&Source.clip==carrier;
            UnityPcmOutput(SpeechPcmStream stream){this.stream=stream;}
            internal static IPcmOutput Open(PcmOutputRequest request,SpeechPcmStream stream)
            {
                var result=new UnityPcmOutput(stream);
                try{result.Prepare(request);return result;}catch{result.Dispose();throw;}
            }
            void Prepare(PcmOutputRequest request)
            {
                int rate=AudioSettings.outputSampleRate;
                if(rate<SpeechPcmStream.Rate||rate>192000)throw new InvalidOperationException("The audio output rate is unsupported.");
                view=new GameObject(request.Name);view.transform.SetParent(request.Parent,false);Source=view.AddComponent<AudioSource>();
                Source.playOnAwake=false;Source.loop=true;Source.volume=request.Gain;Source.priority=request.Priority;
                Source.spatialBlend=request.Spatial?1:0;Source.dopplerLevel=0;Source.spread=0;
                Source.minDistance=request.MinDistance;Source.maxDistance=request.MaxDistance;Source.rolloffMode=AudioRolloffMode.Logarithmic;
                if(request.Spatial)SpeechSpatializer.Configure(Source);
                carrier=AudioClip.Create(request.Name+" carrier",1024,1,SpeechPcmStream.Rate,false);Source.clip=carrier;
                filter=view.AddComponent<SpeechPcmFilter>();filter.Bind(stream,rate);
            }
            public void Start()=>Source.Play();
            public void SetPose(Vector3 position,Quaternion rotation)=>view.transform.SetPositionAndRotation(position,rotation);
            public void SetPaused(bool value){stream.SetPaused(value);if(value)Source.Pause();else Source.UnPause();}
            public void SetGain(float value)=>Source.volume=value;
            public bool IsDrained(double now)=>stream.Read(now).SourceDrained;
            public void Dispose()
            {
                if(closed)return;closed=true;
                if(filter)filter.Stop();filter=null;
                if(Source){Source.Stop();Source.clip=null;}Source=null;
                if(view)DestroyOwned(view);view=null;if(carrier)DestroyOwned(carrier);carrier=null;
            }
            static void DestroyOwned(UnityEngine.Object value)
            {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        }
    }
}
