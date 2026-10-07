// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using UnityEngine;

namespace Maestro.Quest.Book
{
    [Serializable]
    public sealed class SpeechOutputSnapshot
    {
        public long generation, acceptedSequence, submittedSamples, playedSamples;
        public int sampleRate;
        public bool started;
    }

    /// <summary>Owned mono PCM playback, with a DSP timeline rather than network
    /// arrival as completion. No provider, file URL, microphone or app identity.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class NativeSpeechOutput : MonoBehaviour
    {
        const double ScheduleLeadSeconds = .1;
        SpeechPcmStream stream;
        AudioSource source;
        AudioClip carrier;
        SpeechPcmFilter filter;
        long generation;
        int sampleRate;
        bool open, paused, focused = true;
        AvatarPoseRig rig;
        Transform frame;
        Vector3 mouthOffset = new(0, .08f, .08f), fallbackOffset = new(0, 1.4f, .08f);
        // Tests control time/tail and exercise the same render function as the
        // audio callback; clock advancement alone cannot acknowledge playback.
        internal Func<double> Clock = () => AudioSettings.dspTime;
        internal double? TailOverride;
        internal void Render(float[] data, int channels, int rate, double dspTime) => stream.Render(data, channels, rate, dspTime);

        public void ConfigureAnchor(AvatarPoseRig poseRig, Transform avatarFrame, Vector3 headOffset, Vector3 fallback)
        {
            if (!avatarFrame || avatarFrame == transform || avatarFrame.IsChildOf(transform)
                || !Finite(headOffset) || !Finite(fallback) || headOffset.magnitude > .5f || fallback.magnitude > 5)
                throw new ArgumentException("Invalid speech anchor.");
            rig = poseRig; frame = avatarFrame; mouthOffset = headOffset; fallbackOffset = fallback;
            FollowAnchor();
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        internal double TailSeconds
        {
            get
            {
                if (TailOverride.HasValue) return TailOverride.Value;
                AudioSettings.GetDSPBufferSize(out var length, out var buffers);
                return Math.Max(.02, (double)length * Math.Max(1, buffers) / Math.Max(8000, AudioSettings.outputSampleRate) + .02);
            }
        }
        /// <summary>Only the native owner creates generations. A late packet
        /// cannot reopen a stopped output by reusing its old generation.</summary>
        public long Begin(int rate)
        {
            if (rate != 24000) throw new ArgumentException("Speech requires mono PCM16 at 24000 Hz.");
            if (!isActiveAndEnabled || paused || !focused) throw new InvalidOperationException("Speech output is inactive.");
            Stop(); sampleRate = rate;
            try
            {
                int outputRate = AudioSettings.outputSampleRate;
                if (outputRate < rate || outputRate > 192000) throw new InvalidOperationException("Unsupported speech output rate.");
                stream = new SpeechPcmStream(Clock() + ScheduleLeadSeconds, TailSeconds);
                var child = new GameObject("Maestro voice stream");
                child.transform.SetParent(transform, false);
                source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true;
                source.spatialBlend = 1; source.dopplerLevel = 0; source.spread = 0; source.priority = 0;
                source.minDistance = 1; source.maxDistance = 15; source.rolloffMode = AudioRolloffMode.Logarithmic;
                SpeechSpatializer.Configure(source);
                // A silent mono carrier keeps Unity's DSP graph active. The
                // procedural filter replaces its samples; it never replays PCM.
                carrier = AudioClip.Create("Maestro voice carrier", 1024, 1, rate, false);
                source.clip = carrier;
                filter = child.AddComponent<SpeechPcmFilter>(); filter.Bind(stream, outputRate);
                open = true; source.Play();
            }
            catch { Stop(); throw; }
            return generation;
        }
        public bool TryWrite(long owner, long nextSequence, short[] pcm, out string error)
        {
            error = null;
            if (!open || owner != generation || !isActiveAndEnabled || paused || !focused) { error = "Speech output was stopped"; return false; }
            Tick();
            if (!open) { error = "Speech output was stopped"; return false; }
            return stream.TryWrite(nextSequence, pcm, Clock(), out error);
        }
        public SpeechOutputSnapshot Read()
        {
            Tick();
            var state = stream?.Read(Clock()) ?? default;
            return new SpeechOutputSnapshot { generation = generation, sampleRate = sampleRate,
                acceptedSequence = state.Sequence, submittedSamples = state.Submitted, playedSamples = state.Played, started = state.Played > 0 };
        }
        internal void Tick()
        {
            if (open && (!source || !filter || stream.Read(Clock()).Faulted)) Stop();
        }
        public void Stop()
        {
            open = false; generation++;
            stream?.Close(); stream = null;
            if (filter) filter.Stop(); filter = null;
            if (source) { source.Stop(); source.clip = null; DestroyOwned(source.gameObject); } source = null;
            if (carrier) DestroyOwned(carrier); carrier = null;
        }
        static void DestroyOwned(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        internal void FollowAnchor()
        {
            if (!frame) return;
            var head = rig ? rig.Bone(PoseJoint.Head) : null;
            var canonicalHead = rig ? rig.CanonicalBone(PoseJoint.Head) : null;
            // Imported head positions are visible-model positions. Its bone scale
            // and rest axes can differ: offset uses the canonical pose/root metres.
            var rotation = canonicalHead ? canonicalHead.rotation : frame.rotation;
            transform.SetPositionAndRotation(head ? head.position + rotation * Vector3.Scale(frame.lossyScale, mouthOffset)
                : frame.TransformPoint(fallbackOffset), rotation);
        }
        void LateUpdate() { FollowAnchor(); Tick(); }
        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
        void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged; Stop(); }
        void AudioConfigurationChanged(bool _) => Stop();
        void OnApplicationPause(bool value) { paused = value; if (value) Stop(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Stop(); }
    }
}
