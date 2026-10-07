// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Maestro.Quest.Book
{
    /// <summary>One authored reflection mixer for the leased native room. Source
    /// Stop never resets this shared reverb. Capture activity observes its final
    /// listener output and is separate from each source's played-sample cursor.
    /// Not installed by MaestroRoom: a full-mix activity gate cannot distinguish
    /// speech tails from continuous world audio. Multi-emitter capture acceptance
    /// is required before production activation (see unity/AUDIO.md).</summary>
    [DefaultExecutionOrder(220)]
    public sealed class RoomAudioOutput : MonoBehaviour
    {
        static RoomAudioOutput owner;
        RoomAcoustics room;
        AudioListener listener;
        RoomAudioTap tap;
        AudioMixer mixer;
        AudioMixerGroup group;
        AcousticOutputMonitor monitor = new();
        bool paused, focused = true, routed, applied;
        internal string Issue { get; private set; }
        internal bool ReflectionsActive { get; private set; }
        internal bool Routed => owner == this && routed && isActiveAndEnabled && !paused && focused;
        internal bool MicrophoneSuppressed => monitor.Read(AcousticOutputMonitor.Now, DeviceTail).Suppressed;
        internal bool MonitorFailed => monitor.Read(AcousticOutputMonitor.Now, DeviceTail).Failed;
        internal static double DeviceTail
        {
            get { AudioSettings.GetDSPBufferSize(out var length, out var count); return .02 + (double)length * Math.Max(1, count) / Math.Max(8000, AudioSettings.outputSampleRate); }
        }
        internal void Configure(RoomAcoustics acoustics, AudioListener viewer)
        {
            room = acoustics; listener = viewer;
            if (listener) tap = listener.GetComponent<RoomAudioTap>() ?? listener.gameObject.AddComponent<RoomAudioTap>();
            ResetMonitor();
        }
        internal bool Route(AudioSource source)
        {
            Refresh();
            if (!Routed || !group) return false;
            source.outputAudioMixerGroup = group; return true;
        }
        internal void NoteSpeech() => monitor.Arm(AcousticOutputMonitor.Now);
        void ResetMonitor()
        {
            monitor = new AcousticOutputMonitor();
            if (tap) tap.Bind(monitor, AudioSettings.outputSampleRate);
        }
        internal void Refresh()
        {
            if (!isActiveAndEnabled || paused || !focused || !room || !room.ContextOpen || !listener || !listener.isActiveAndEnabled)
            { DisableMixer(); return; }
            if (owner && owner != this) { Issue = "Another room owns the reflection output"; return; }
            if (owner != this)
            {
                mixer = Resources.Load<AudioMixer>("MaestroRoomAudio");
                var groups = mixer ? mixer.FindMatchingGroups("Master") : Array.Empty<AudioMixerGroup>();
                if (groups.Length != 1) { Issue = "The room reflection mixer is unavailable"; return; }
                owner = this; group = groups[0]; routed = true;
            }
            bool enable = room.MapReady;
            if ((!applied || ReflectionsActive != enable) &&
                (!mixer.SetFloat("Early", enable ? 1 : 0) || !mixer.SetFloat("Late", enable ? 1 : 0) || !mixer.SetFloat("Wet", 0)))
            { DisableMixer(); Issue = "The room reflection mixer could not be configured"; return; }
            applied = true; ReflectionsActive = enable; Issue = null;
        }
        void DisableMixer()
        {
            if (owner == this)
            {
                if (mixer) { mixer.SetFloat("Early", 0); mixer.SetFloat("Late", 0); }
                owner = null;
            }
            applied = ReflectionsActive = routed = false;
        }
        void LateUpdate() => Refresh();
        void OnEnable() { AudioSettings.OnAudioConfigurationChanged += AudioChanged; ResetMonitor(); }
        void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= AudioChanged; DisableMixer(); }
        void AudioChanged(bool _) { DisableMixer(); ResetMonitor(); }
        void OnApplicationPause(bool value) { paused = value; if (value) DisableMixer(); else ResetMonitor(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) DisableMixer(); else ResetMonitor(); }
    }
}
