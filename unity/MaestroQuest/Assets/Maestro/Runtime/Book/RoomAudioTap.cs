// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Observes the Unity listener mix after spatialization/reflections.
    /// Optional PCM ownership is explicit and ephemeral. Later filters, platform
    /// mixing, WebView audio and hardware latency are outside this reference.</summary>
    [RequireComponent(typeof(AudioListener)), DisallowMultipleComponent]
    internal sealed class RoomAudioTap : MonoBehaviour
    {
        sealed class Binding
        {
            internal readonly AcousticOutputMonitor Monitor;
            internal readonly int Rate;
            internal Binding(AcousticOutputMonitor monitor, int rate) { Monitor=monitor; Rate=rate; }
        }
        Binding binding;
        EchoReferenceStream reference;
        AudioListener listener;
        bool paused, focused=true;
        internal void Bind(AcousticOutputMonitor monitor, int rate) => Volatile.Write(ref binding,new Binding(monitor,rate));
        // Main-thread admission. Disposing an older lease cannot cancel a later
        // one. Merely enabling this component never opens PCM capture.
        internal EchoReferenceStream BeginReference()
        {
            if(!isActiveAndEnabled || !listener || !listener.isActiveAndEnabled || paused || !focused || AudioListener.pause)
                throw new InvalidOperationException("The listener reference is unavailable");
            var current=Volatile.Read(ref reference);
            if(current!=null && !current.Closed) throw new InvalidOperationException("The listener reference already has an owner");
            var config=AudioSettings.GetConfiguration();
            int channels=config.speakerMode==AudioSpeakerMode.Mono?1:config.speakerMode==AudioSpeakerMode.Stereo?2:0;
            if(channels==0 || config.dspBufferSize<1 || config.dspBufferSize>config.sampleRate/2)
                throw new InvalidOperationException("The listener format cannot provide a bounded reference");
            var next=new EchoReferenceStream(config.sampleRate,channels);
            Volatile.Write(ref reference,next); return next;
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            double now=AcousticOutputMonitor.Now;
            var current=Volatile.Read(ref binding);
            current?.Monitor.Observe(data,channels,current.Rate,now);
            Volatile.Read(ref reference)?.Observe(data,channels,AudioSettings.dspTime,now);
        }
        void CloseReference() => Interlocked.Exchange(ref reference,null)?.Dispose();
        void OnEnable() { listener=GetComponent<AudioListener>(); AudioSettings.OnAudioConfigurationChanged+=AudioChanged; }
        void OnDisable() { AudioSettings.OnAudioConfigurationChanged-=AudioChanged; CloseReference(); }
        void OnDestroy() { CloseReference(); Volatile.Write(ref binding,null); }
        void AudioChanged(bool _) => CloseReference();
        void LateUpdate() { if(!listener || !listener.isActiveAndEnabled || AudioListener.pause) CloseReference(); }
        void OnApplicationPause(bool value) { paused=value; if(value) CloseReference(); }
        void OnApplicationFocus(bool value) { focused=value; if(!value) CloseReference(); }
    }
}
