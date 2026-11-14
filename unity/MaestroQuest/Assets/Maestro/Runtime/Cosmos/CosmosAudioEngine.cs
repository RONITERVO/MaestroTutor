// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    /// <summary>
    /// Dual-slot A/B crossfading zone ambient drone synthesizer (0.25s crossfade, -9 dB / ~38ms speech ducking)
    /// ported 1:1 from HyperCosmosVR crates/cosmos_audio/src/synth.rs.
    /// </summary>
    public sealed class CosmosZoneAmbientSynth
    {
        public const float DuckingLinearGain = 0.3548134f; // -9 dB
        public const float CrossfadeDurationSec = 0.25f;
        public const float DuckTauSec = 0.038f;

        struct AmbientVoiceSlot
        {
            public float BaseHz;
            public float FilterHz;
            public float NoiseLevel;
            public float PulseDepth;
            public float PulseHz;
            public float TargetGain;
            public float WSin, WTri, WSaw, WSq, WSub, WNoise;

            double oscPhase;
            double subPhase;
            float lfoPhase;
            uint rngState;
            float b0, b1, b2; // Paul Kellet pink noise state
            float svfLow, svfBand;

            public void ResetRng(uint seed)
            {
                if (rngState == 0) rngState = seed == 0 ? 0x9E3779B9u : seed;
            }

            public void ApplySound(bool hasSound, float baseHz, float filterHz, float noise, float pulse, int waveformIndex, int sceneIndex)
            {
                if (!hasSound)
                {
                    TargetGain = 0f;
                    return;
                }
                ResetRng(0x6D2B79F5u);
                BaseHz = Mathf.Clamp(baseHz, 28f, 2000f);
                float sceneDetune = sceneIndex >= 0 ? 1f + (sceneIndex % 5) * 0.012f : 1f;
                FilterHz = Mathf.Clamp(filterHz * sceneDetune, 120f, 12000f);
                NoiseLevel = Mathf.Clamp01(noise);
                PulseDepth = Mathf.Clamp01(pulse);
                PulseHz = Mathf.Clamp(0.25f + PulseDepth * 3.2f, 0.15f, 6.0f);

                WNoise = NoiseLevel * 0.55f;
                WSub = 0.32f;
                int wf = Mathf.Clamp(waveformIndex, 0, 3);
                WSin = wf == 0 ? 0.85f : 0f;
                WTri = wf == 1 ? 0.85f : 0f;
                WSaw = wf == 2 ? 0.45f : 0f;
                WSq = wf == 3 ? 0.40f : 0f;
                TargetGain = 0.18f;
            }

            public float TickSample(float sampleRate)
            {
                if (TargetGain <= 1e-6f) return 0f;
                float srInv = 1f / Mathf.Max(8000f, sampleRate);
                const float TwoPi = Mathf.PI * 2f;

                oscPhase += BaseHz * srInv;
                oscPhase -= Math.Floor(oscPhase);
                subPhase += (BaseHz * 0.5f) * srInv;
                subPhase -= Math.Floor(subPhase);

                float p = (float)oscPhase;
                float sin = Mathf.Sin(p * TwoPi);
                float tri = 1f - 4f * Mathf.Abs(p - 0.5f);
                float saw = 2f * p - 1f;
                float sq = p < 0.5f ? 1f : -1f;
                float sub = Mathf.Sin((float)subPhase * TwoPi);

                // Fast xorshift32 white -> approximate pink noise
                uint x = rngState == 0 ? 0x12345678u : rngState;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                rngState = x;
                float white = ((x & 0xFFFFu) / 32768f) - 1f;
                b0 = 0.99765f * b0 + white * 0.0990460f;
                b1 = 0.96300f * b1 + white * 0.2965164f;
                b2 = 0.57000f * b2 + white * 1.0526913f;
                float pink = (b0 + b1 + b2 + white * 0.1848f) * 0.11f;

                float raw = sin * WSin + tri * WTri + saw * WSaw + sq * WSq + sub * WSub + (pink * 0.25f) * WNoise;

                // Chamberlin 2-pole state-variable lowpass at Q = 0.85 (2x oversampled for stability at high FilterHz)
                float f = Mathf.Clamp(2f * Mathf.Sin(Mathf.PI * Mathf.Min(FilterHz, sampleRate * 0.24f) * srInv), 0.001f, 0.95f);
                const float qInv = 1f / 0.85f;
                for (int os = 0; os < 2; os++)
                {
                    svfLow += f * svfBand;
                    float high = raw - svfLow - qInv * svfBand;
                    svfBand += f * high;
                }

                lfoPhase += PulseHz * srInv;
                if (lfoPhase >= 1f) lfoPhase -= Mathf.Floor(lfoPhase);
                float lfo = 1f - 0.5f * PulseDepth * (1f - Mathf.Sin(lfoPhase * TwoPi));
                return svfLow * lfo * TargetGain;
            }
        }

        readonly AmbientVoiceSlot[] slots = new AmbientVoiceSlot[2];
        int activeSlot;
        float xfade;
        float duckGain = 1f;
        bool hasZone;
        float curBaseHz, curFilterHz, curNoise, curPulse;
        int curWaveform = -1;
        int curSceneIndex = -2;

        public float ActiveDuckGain => duckGain;
        public float CrossfadeProgress => xfade;
        public float ActiveBaseHz => slots[activeSlot].BaseHz;
        public float ActiveFilterHz => slots[activeSlot].FilterHz;

        public void SetZone(CosmosZoneSoundRecipe recipe, int sceneIndex = -1) =>
            SetZone(recipe.BaseHz, recipe.FilterHz, recipe.NoiseAmount, recipe.PulseRate, (int)recipe.Waveform, sceneIndex);

        public void SetZone(float baseHz, float filterHz, float noise, float pulse, int waveformIndex, int sceneIndex = -1)
        {
            if (hasZone
                && Mathf.Approximately(curBaseHz, baseHz)
                && Mathf.Approximately(curFilterHz, filterHz)
                && Mathf.Approximately(curNoise, noise)
                && Mathf.Approximately(curPulse, pulse)
                && curWaveform == waveformIndex
                && curSceneIndex == sceneIndex)
            {
                return;
            }

            hasZone = true;
            curBaseHz = baseHz;
            curFilterHz = filterHz;
            curNoise = noise;
            curPulse = pulse;
            curWaveform = waveformIndex;
            curSceneIndex = sceneIndex;

            int next = 1 - activeSlot;
            slots[next].ApplySound(true, baseHz, filterHz, noise, pulse, waveformIndex, sceneIndex);
            activeSlot = next;
        }

        public void ClearZone()
        {
            if (!hasZone) return;
            hasZone = false;
            int next = 1 - activeSlot;
            slots[next].ApplySound(false, 0f, 0f, 0f, 0f, 0, -1);
            activeSlot = next;
        }

        public float MixBlock(float[] buffer, int frames, int channels, bool narrationActive, int sampleRate)
        {
            if (buffer == null || frames <= 0 || channels <= 0) return 0f;
            float sr = Mathf.Max(8000, sampleRate);
            float duckSmooth = 1f - Mathf.Exp(-1f / (DuckTauSec * sr));
            float xfadeStep = 1f / (CrossfadeDurationSec * sr);
            float targetDuck = narrationActive ? DuckingLinearGain : 1f;
            float targetXfade = activeSlot == 0 ? 0f : 1f;
            float sumSq = 0f;

            for (int f = 0; f < frames; f++)
            {
                duckGain += duckSmooth * (targetDuck - duckGain);
                float diff = targetXfade - xfade;
                if (Mathf.Abs(diff) > xfadeStep) xfade += diff > 0f ? xfadeStep : -xfadeStep;
                else xfade = targetXfade;

                float s0 = slots[0].TickSample(sr);
                float s1 = slots[1].TickSample(sr);
                float mono = s0 * ((1f - xfade) * duckGain) + s1 * (xfade * duckGain);
                float l = mono * 0.96f + s1 * 0.05f * duckGain;
                float r = mono * 0.96f - s0 * 0.05f * duckGain;

                int idx = f * channels;
                if (channels == 1)
                {
                    buffer[idx] += mono;
                    sumSq += mono * mono;
                }
                else
                {
                    buffer[idx] += l;
                    buffer[idx + 1] += r;
                    sumSq += 0.5f * (l * l + r * r);
                }
            }

            return Mathf.Sqrt(sumSq / Mathf.Max(1, frames));
        }
    }

    /// <summary>
    /// Procedural dwell-shimmer (528->924Hz), lock chime (1318.5Hz), and ascending pentatonic wrist-bead ticks (880..2637.02Hz).
    /// </summary>
    public sealed class CosmosFeedbackChimeSynth
    {
        static readonly float[] BeadPentatonicHz =
        {
            880.0f, 987.77f, 1108.73f, 1318.51f, 1479.98f, 1760.0f, 1975.53f, 2217.46f, 2637.02f
        };

        float dwellProgress;
        float shimmerPhase;
        float lockEnv;
        float lockPhase;
        float tickEnv;
        float tickPhase;
        float tickHz = 1180f;

        public float DwellProgress => dwellProgress;
        public float ResonanceGlow => Mathf.Clamp01(Mathf.Max(dwellProgress, tickEnv * 0.8f + lockEnv));

        public void SetDwellProgress(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (dwellProgress < 0.99f && clamped >= 0.99f)
                lockEnv = 1f;
            dwellProgress = clamped;
        }

        public void TriggerLockChime() => lockEnv = 1f;

        public void TriggerSpeedTick(int beadIndex)
        {
            int idx = Mathf.Clamp(beadIndex, 0, BeadPentatonicHz.Length - 1);
            tickHz = BeadPentatonicHz[idx];
            tickEnv = 1f;
        }

        public float MixBlock(float[] buffer, int frames, int channels, int sampleRate)
        {
            if (buffer == null || frames <= 0 || channels <= 0) return ResonanceGlow;
            float sr = Mathf.Max(8000, sampleRate);
            float srInv = 1f / sr;
            const float TwoPi = Mathf.PI * 2f;

            float shimmerAmp = dwellProgress > 0.22f
                ? Mathf.Clamp01((dwellProgress - 0.22f) / 0.78f) * 0.032f
                : 0f;
            float shimmerHz = 528.0f + dwellProgress * 396.0f;
            float lockDecay = Mathf.Exp(-1f / (0.14f * sr));
            float tickDecay = Mathf.Exp(-1f / (0.045f * sr));

            for (int f = 0; f < frames; f++)
            {
                float sample = 0f;
                if (shimmerAmp > 0.0001f)
                {
                    shimmerPhase += shimmerHz * srInv;
                    if (shimmerPhase >= 1f) shimmerPhase -= Mathf.Floor(shimmerPhase);
                    float s1 = Mathf.Sin(shimmerPhase * TwoPi);
                    float s2 = Mathf.Sin(shimmerPhase * 2f * TwoPi) * 0.35f;
                    sample += (s1 + s2) * shimmerAmp;
                }
                if (lockEnv > 0.0005f)
                {
                    lockPhase += 1318.5f * srInv;
                    if (lockPhase >= 1f) lockPhase -= Mathf.Floor(lockPhase);
                    sample += Mathf.Sin(lockPhase * TwoPi) * lockEnv * 0.065f;
                    lockEnv *= lockDecay;
                }
                if (tickEnv > 0.0005f)
                {
                    tickPhase += tickHz * srInv;
                    if (tickPhase >= 1f) tickPhase -= Mathf.Floor(tickPhase);
                    sample += Mathf.Sin(tickPhase * TwoPi) * tickEnv * 0.07f;
                    tickEnv *= tickDecay;
                }

                int idx = f * channels;
                if (channels == 1)
                {
                    buffer[idx] += sample;
                }
                else
                {
                    buffer[idx] += sample;
                    buffer[idx + 1] += sample * 0.98f;
                }
            }

            return ResonanceGlow;
        }
    }

    internal sealed class CosmosFilterRelay : MonoBehaviour
    {
        internal Action<float[], int> Handler;

        void OnAudioFilterRead(float[] data, int channels)
        {
            Handler?.Invoke(data, channels);
        }
    }

    /// <summary>
    /// Quest-safe pooled spatial & procedural audio host for HyperCosmosVR.
    /// Allocates 3 pooled AudioSources (&lt;= 6 max limit), keeps EnableAcoustics = false,
    /// avoids RoomAudioOutput/RoomAcousticMapScheduler, and silences cleanly on focus/pause/destroy.
    /// </summary>
    public sealed class CosmosAudioEngine : MonoBehaviour
    {
        public const int MaxPooledSources = 6;
        public const float SpatialHrtfBlend = 0.70f;

        readonly object dspLock = new object();
        readonly CosmosZoneAmbientSynth ambientSynth = new CosmosZoneAmbientSynth();
        readonly CosmosFeedbackChimeSynth chimeSynth = new CosmosFeedbackChimeSynth();

        AudioSource[] pooledSources;
        AudioSource ambientSource;
        AudioSource chimeSource;
        AudioSource cueSource;
        AudioClip silentCarrier;
        int outputSampleRate = 48000;
        volatile bool narrationActive;
        volatile bool userMuted;
        volatile bool lifecyclePaused;
        volatile bool componentActive;
        volatile bool disposed;

        public CosmosZoneAmbientSynth AmbientSynth => ambientSynth;
        public CosmosFeedbackChimeSynth ChimeSynth => chimeSynth;
        public int PooledAudioSourceCount => pooledSources?.Length ?? 0;
        public bool IsMuted => userMuted || lifecyclePaused || !componentActive;
        public float ActiveDuckGain { get { lock (dspLock) return ambientSynth.ActiveDuckGain; } }
        public float CrossfadeProgress { get { lock (dspLock) return ambientSynth.CrossfadeProgress; } }

        void Awake()
        {
            componentActive = isActiveAndEnabled;
            Initialize();
        }

        internal void Initialize()
        {
            componentActive = isActiveAndEnabled;
            if (pooledSources != null) return;
            outputSampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            silentCarrier = CreateSilentCarrier(outputSampleRate);

            ambientSource = CreatePooledChild("CosmosAmbientDrone", spatialize: false, spatialBlend: 0f, out var ambientRelay);
            ambientRelay.Handler = RenderAmbientAudio;

            chimeSource = CreatePooledChild("CosmosFeedbackChime", spatialize: true, spatialBlend: SpatialHrtfBlend, out var chimeRelay);
            chimeRelay.Handler = RenderChimeAudio;

            cueSource = CreatePooledChild("CosmosSpatialCue", spatialize: true, spatialBlend: SpatialHrtfBlend, out _);

            pooledSources = new[] { ambientSource, chimeSource, cueSource };
            ApplySourcePlaybackState();
        }

        public void SetZone(CosmosZoneSoundRecipe recipe, int sceneIndex = -1) =>
            SetZone(recipe.BaseHz, recipe.FilterHz, recipe.NoiseAmount, recipe.PulseRate, (int)recipe.Waveform, sceneIndex);

        public void SetZone(float baseHz, float filterHz, float noise, float pulse, int waveformIndex, int sceneIndex = -1)
        {
            lock (dspLock)
            {
                ambientSynth.SetZone(baseHz, filterHz, noise, pulse, waveformIndex, sceneIndex);
            }
        }

        public void ClearZone()
        {
            lock (dspLock)
            {
                ambientSynth.ClearZone();
            }
        }

        public void SetNarrationActive(bool active)
        {
            narrationActive = active;
        }

        public void SetDwellProgress(float progress)
        {
            lock (dspLock)
            {
                chimeSynth.SetDwellProgress(progress);
            }
        }

        public void TriggerLockChime(Vector3 worldPos)
        {
            if (chimeSource) chimeSource.transform.position = worldPos;
            lock (dspLock)
            {
                chimeSynth.TriggerLockChime();
            }
        }

        public void TriggerSpeedTick(int beadIndex, Vector3 worldPos)
        {
            if (chimeSource) chimeSource.transform.position = worldPos;
            lock (dspLock)
            {
                chimeSynth.TriggerSpeedTick(beadIndex);
            }
        }

        public void SetChimeWorldPosition(Vector3 worldPos)
        {
            if (chimeSource) chimeSource.transform.position = worldPos;
        }

        public void SetUserMuted(bool muted)
        {
            userMuted = muted;
            ApplySourcePlaybackState();
        }

        void RenderAmbientAudio(float[] data, int channels)
        {
            if (disposed || IsMuted || data == null || channels <= 0) return;
            int frames = data.Length / channels;
            lock (dspLock)
            {
                ambientSynth.MixBlock(data, frames, channels, narrationActive, outputSampleRate);
            }
        }

        void RenderChimeAudio(float[] data, int channels)
        {
            if (disposed || IsMuted || data == null || channels <= 0) return;
            int frames = data.Length / channels;
            lock (dspLock)
            {
                chimeSynth.MixBlock(data, frames, channels, outputSampleRate);
            }
        }

        AudioSource CreatePooledChild(string childName, bool spatialize, float spatialBlend, out CosmosFilterRelay relay)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = silentCarrier != null;
            src.clip = silentCarrier;
            src.volume = 1f;
            src.spatialBlend = spatialBlend;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 0.35f;
            src.maxDistance = 60f;
            src.spatialize = spatialize && CanConfigureMetaSpatializer();
            // Run Meta XR Audio HRTF after CosmosFilterRelay.OnAudioFilterRead fills PCM over the silent carrier.
            src.spatializePostEffects = src.spatialize;

            if (src.spatialize)
            {
                var spatializer = go.AddComponent<MetaXRAudioSource>();
                spatializer.EnableSpatialization = true;
                // Strictly opt out of room acoustics so ProductionAcousticsTests invariants stay 100% green.
                spatializer.EnableAcoustics = false;
                spatializer.GainBoostDb = 0f;
                spatializer.UpdateParameters();
            }

            relay = go.AddComponent<CosmosFilterRelay>();
            return src;
        }

        static bool CanConfigureMetaSpatializer()
        {
            if (AudioSettings.GetSpatializerPluginName() != "Meta XR Audio") return false;
            try
            {
                int result = MetaXRAcousticNativeInterface.UnityNativeInterface.ovrAudio_GetPluginContext(out var ctx);
                return result == 0 && ctx != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        static AudioClip CreateSilentCarrier(int sampleRate)
        {
            if (sampleRate <= 0) return null;
            try
            {
                int lengthSamples = Mathf.Max(1024, sampleRate);
                var clip = AudioClip.Create("CosmosSilenceCarrier", lengthSamples, 2, sampleRate, false);
                var zeros = new float[lengthSamples * 2];
                clip.SetData(zeros, 0);
                return clip;
            }
            catch
            {
                return null;
            }
        }

        void ApplySourcePlaybackState()
        {
            if (pooledSources == null) return;
            bool muted = IsMuted;
            for (int i = 0; i < pooledSources.Length; i++)
            {
                var src = pooledSources[i];
                if (!src) continue;
                src.mute = muted;
                if (muted)
                {
                    if (src.isPlaying) src.Pause();
                }
                else if (src.clip && !src.isPlaying && gameObject.activeInHierarchy && enabled)
                {
                    src.Play();
                }
            }
        }

        void SilenceAll()
        {
            if (pooledSources == null) return;
            for (int i = 0; i < pooledSources.Length; i++)
            {
                var src = pooledSources[i];
                if (!src) continue;
                src.mute = true;
                src.Stop();
            }
        }

        void OnEnable()
        {
            disposed = false;
            componentActive = true;
            if (pooledSources != null) ApplySourcePlaybackState();
        }

        void OnApplicationPause(bool pause)
        {
            lifecyclePaused = pause;
            if (pause) SilenceAll();
            else ApplySourcePlaybackState();
        }

        void OnApplicationFocus(bool focus)
        {
            lifecyclePaused = !focus;
            if (!focus) SilenceAll();
            else ApplySourcePlaybackState();
        }

        void OnDisable()
        {
            componentActive = false;
            SilenceAll();
        }

        void OnDestroy()
        {
            componentActive = false;
            disposed = true;
            SilenceAll();
            if (silentCarrier)
            {
                ArtResources.Release(silentCarrier);
                silentCarrier = null;
            }
        }
    }
}
