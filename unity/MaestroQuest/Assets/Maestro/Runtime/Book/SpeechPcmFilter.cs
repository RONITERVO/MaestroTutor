// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>A single source filter retained across provider packet boundaries.
    /// Spatialization runs after this filter, so it processes the generated PCM.</summary>
    [RequireComponent(typeof(AudioSource))]
    internal sealed class SpeechPcmFilter : MonoBehaviour
    {
        SpeechPcmStream stream;
        readonly WaterPcmTreatment water=new();
        RoomWaterAudio waterOwner;
        internal void BindWater(RoomWaterAudio owner){if(waterOwner)waterOwner.Unregister(this);waterOwner=owner;if(owner)owner.Register(this);}
        internal void SetWater(WaterSoundMix value,double until)=>water.Publish(value,until);
        internal WaterSoundMix WaterAt(double now)=>water.Read(now);
        internal void ResetWaterOnNextRender()=>water.RequestReset();
        int outputRate;
        internal void Bind(SpeechPcmStream value, int rate) { outputRate = rate; Volatile.Write(ref stream, value); }
        void OnAudioFilterRead(float[] data,int channels)=>Render(data,channels,outputRate,AudioSettings.dspTime);
        internal void Render(float[] data,int channels,int rate,double time){
            var current=Volatile.Read(ref stream);
            if(current==null){Array.Clear(data,0,data.Length);water.Reset();return;}
            if(current.Render(data,channels,rate,time))water.Process(data,channels,rate,time);else water.Reset();
        }
        internal void Stop(){
            if(waterOwner)waterOwner.Unregister(this);waterOwner=null;
            SetWater(WaterSoundMix.Dry,double.PositiveInfinity);Interlocked.Exchange(ref stream,null)?.Close();
        }
        void OnDisable() => Stop();
    }
}
