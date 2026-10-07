// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
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
        const int MaxSegments = 64, MaxChunkSamples = 4800, MaxQueuedSeconds = 8;
        const double ScheduleLeadSeconds = .1;
        sealed class Segment
        {
            public AudioSource Source;
            public AudioClip Clip;
            public double Start, End;
            public long First, Last;
        }
        readonly List<Segment> segments = new();
        long generation, sequence, submitted, completed, reported;
        int sampleRate;
        double nextStart;
        bool open, paused, focused = true;
        AvatarPoseRig rig;
        Transform frame;
        Vector3 mouthOffset = new(0, .08f, .08f), fallbackOffset = new(0, 1.4f, .08f);
        // Test seams control time/tail, never substitute the AudioSource/PCM path.
        internal Func<double> Clock = () => AudioSettings.dspTime;
        internal double? TailOverride;

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
            Stop(); sampleRate = rate; open = true;
            return generation;
        }
        public bool TryWrite(long owner, long nextSequence, short[] pcm, out string error)
        {
            error = null;
            if (!open || owner != generation || !isActiveAndEnabled || paused || !focused) { error = "Speech output was stopped"; return false; }
            if (nextSequence != sequence + 1) { error = "Speech chunk is duplicate or out of order"; return false; }
            if (pcm == null || pcm.Length == 0 || pcm.Length > MaxChunkSamples) { error = "Speech chunk is invalid"; return false; }
            Tick();
            if (segments.Count >= MaxSegments || submitted - completed + pcm.Length > sampleRate * MaxQueuedSeconds)
            { error = "Speech output buffer is full"; return false; }
            AudioClip clip = null; GameObject child = null;
            try
            {
                var samples = new float[pcm.Length];
                for (int i = 0; i < pcm.Length; i++) samples[i] = pcm[i] / 32768f;
                clip = AudioClip.Create("Maestro speech PCM", samples.Length, 1, sampleRate, false);
                if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Speech PCM upload failed");
                child = new GameObject("Speech segment", typeof(AudioSource));
                child.transform.SetParent(transform, false);
                var source = child.GetComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.clip = clip;
                source.spatialBlend = 1; source.dopplerLevel = 0; source.spread = 0;
                source.minDistance = 1; source.maxDistance = 15; source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.spatialize = !string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName());
                var start = Math.Max(Clock() + ScheduleLeadSeconds, nextStart);
                source.PlayScheduled(start);
                var segment = new Segment { Source = source, Clip = clip, Start = start,
                    End = start + (double)pcm.Length / sampleRate, First = submitted, Last = submitted + pcm.Length };
                segments.Add(segment);
                sequence = nextSequence; submitted = segment.Last; nextStart = segment.End;
                return true;
            }
            catch (Exception)
            {
                if (child) { var source = child.GetComponent<AudioSource>(); if (source) source.Stop(); DestroyOwned(child); }
                if (clip) DestroyOwned(clip);
                error = "Speech output could not schedule audio";
                return false;
            }
        }
        public SpeechOutputSnapshot Read()
        {
            Tick();
            long played = completed;
            var now = Clock() - TailSeconds;
            foreach (var segment in segments)
            {
                if (now < segment.Start) break;
                played = Math.Max(played, segment.First + Math.Min(segment.Last - segment.First,
                    Math.Max(0, (long)Math.Floor((now - segment.Start) * sampleRate))));
            }
            reported = Math.Max(reported, played);
            return new SpeechOutputSnapshot { generation = generation, sampleRate = sampleRate,
                acceptedSequence = sequence, submittedSamples = submitted, playedSamples = reported, started = reported > 0 };
        }
        internal void Tick()
        {
            var now = Clock() - TailSeconds;
            while (segments.Count > 0 && now >= segments[0].End)
            {
                var done = segments[0]; segments.RemoveAt(0); completed = done.Last; Release(done);
            }
        }
        public void Stop()
        {
            open = false; generation++;
            foreach (var segment in segments) Release(segment);
            segments.Clear(); sequence = submitted = completed = reported = 0; nextStart = 0;
        }
        static void Release(Segment segment)
        {
            if (segment.Source) { segment.Source.Stop(); segment.Source.clip = null; DestroyOwned(segment.Source.gameObject); }
            if (segment.Clip) DestroyOwned(segment.Clip);
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
