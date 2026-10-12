// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Runtime.InteropServices;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class PreparedGraph
    {
        sealed class PcmBinding
        {
            internal readonly long Serial;
            internal readonly PcmVoice Voice;
            internal PcmBinding(long serial, PcmVoice voice)
            {
                Serial = serial;
                Voice = voice;
            }
        }
        readonly PcmBinding[] pcm = new PcmBinding[MaximumVoices], consumedPcm = new PcmBinding[MaximumVoices];
        readonly long[] resetAcks = new long[MaximumVoices], responseResets = new long[MaximumVoices];
        readonly IntPtr[] direct = new IntPtr[MaximumVoices];
        readonly float[] renderedDistance = new float[MaximumVoices];
        long pcmSerial;
        internal long ResetAck(int voice) => Volatile.Read(ref resetAcks[voice]);
        internal bool NeedsPcmResponse { get {
            for (int i=0;i<VoiceCount;i++) if (ResetAck(i) > Volatile.Read(ref responseResets[i])) return true;
            return false;
        } }
        internal long[] SnapshotResetAcks()
        {
            var result = new long[VoiceCount];
            for (int i=0;i<result.Length;i++) result[i] = ResetAck(i);
            return result;
        }
        internal bool PcmWetReady(int voice) => Volatile.Read(ref pcm[voice]) is PcmBinding binding
                                                && Volatile.Read(ref geometryAllowed)!=0 && binding.Serial <= Volatile.Read(ref responseResets[voice]) &&
                                                CompletedRevision >= Volatile.Read(ref requiredRevision);
        internal PcmVoice BindPcm(string id, PcmVoice voice)
        {
            int index = ClaimSlot(id);
            if (index < 0) { voice.Close(); return null; }
            voice.Slot = index;
            var previous = Interlocked.Exchange(ref pcm[index], new PcmBinding(++pcmSerial, voice));
            previous?.Voice?.Close();
            Volatile.Write(ref playing[index], 0);
            return voice;
        }
        internal bool StopPcm(string id, PcmVoice voice)
        {
            int index = FindSlot(id);
            if (index < 0 || voice == null || IsRetired || Volatile.Read(ref pcm[index])?.Voice != voice)
                return false;
            voice.Close();
            Volatile.Write(ref pcm[index], new PcmBinding(++pcmSerial, null));
            return true;
        }
        internal bool FinishPcm(string id, PcmVoice voice, double now,
                                out Maestro.Quest.Book.SpeechPcmStream.Cursor receipt)
        {
            receipt = default;
            int index = FindSlot(id);
            if (index < 0 || voice == null || IsRetired || Volatile.Read(ref pcm[index])?.Voice != voice ||
                !voice.RendererDrained(now))
                return false;
            var final = voice.Read(now);
            if (!final.SourceDrained || !StopPcm(id, voice))
                return false;
            receipt = final;
            return true;
        }
        void PreparePcm(ref SteamAudio.AudioSettings audio)
        {
            var settings = new NativeDsp.BinauralSettings { hrtf = hrtf };
            for (int i = 0; i < VoiceCount; i++)
                Check(NativeDsp.iplBinauralEffectCreate(context, ref audio, ref settings, out direct[i]));
        }
        void RetirePcm()
        {
            foreach (var binding in pcm)
                binding?.Voice?.Close();
        }
        // Audio thread alone resets effect history. The producer subsequently
        // simulates against the acknowledged reset serial, never a guessed time.
        void ObservePcm(int voice, PcmBinding binding)
        {
            if (binding == consumedPcm[voice])
                return;
            NativeDsp.iplBinauralEffectReset(direct[voice]);
            NativeDsp.iplReflectionEffectReset(reflections[voice]);
            NativeDsp.iplAmbisonicsDecodeEffectReset(decoders[voice]);
            ResetPcmTail(voice);
            consumedPcm[voice] = binding;
            Volatile.Write(ref resetAcks[voice], binding.Serial);
        }
        void CopyStereo(int voice, float[] output, float gain = 1)
        {
            for (int c = 0; c < 2; c++)
            {
                Marshal.Copy(Marshal.ReadIntPtr(stereo[voice].data, c * IntPtr.Size), channel, 0, Frame);
                for (int i = 0; i < Frame; i++)
                    output[2 * i + c] += gain * channel[i];
            }
        }
        bool RenderPcm(int voice, PcmBinding binding, AcousticFrame frame, bool geometryReady, long geometryRevision,
                       ref NativeDsp.DecodeParams parameters, float[] output, double dspTime)
        {
            ObservePcm(voice, binding);
            renderedPcmAudible[voice] = renderedPcmFinal[voice] = false;
            if (!double.IsFinite(dspTime) || dspTime < pcmDspUntil[voice] - 1e-7)
                return false;
            pcmDspUntil[voice] = dspTime + (double)Frame / Rate;
            Array.Clear(signal, 0, signal.Length);
            Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
            if (binding.Voice?.Open != true || pcmTailPhase[voice] == PcmRenderPhase.Drained)
            {
                ConsumeSilentResponse(voice, ref parameters);
                return true;
            }
            renderedDistance[voice] = binding.Voice.Settings.DirectAttenuation(frame.Listener, frame.Source(voice));
            var cursor = binding.Voice.Read(dspTime);
            if (cursor.SourceDrained || pcmTailPhase[voice] == PcmRenderPhase.Draining)
            {
                pcmTailPhase[voice] = PcmRenderPhase.Draining;
                renderedPcmAudible[voice] = !cursor.Paused && !cursor.Faulted;
                RenderPcmTail(voice, binding.Voice, geometryReady && pcmWetGeometry[voice] == geometryRevision,
                              ref parameters, output);
                // GetTail does not consume a newly published native IR. Do not
                // acknowledge it until a later silent Apply has actually read it.
                return false;
            }
            renderedPcmAudible[voice] = binding.Voice.Render(signal, Rate, dspTime);
            Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
            var listener = frame.Listener;
            var source = frame.Source(voice);
            if (binding.Voice.Settings.Spatial)
            {
                bool coincident = source.origin.x == listener.origin.x && source.origin.y == listener.origin.y &&
                                  source.origin.z == listener.origin.z;
                var spatial = new NativeDsp.BinauralParams {
                    direction = coincident ? V(0, 0, -1) : API.iplCalculateRelativeDirection(context, source.origin,
                        listener.origin, listener.ahead, listener.up),
                    interpolation = 0, spatialBlend = 1, hrtf = hrtf
                };
                directTail[voice] =
                    NativeDsp.iplBinauralEffectApply(direct[voice], ref spatial, ref inputs[voice], ref stereo[voice]);
                CopyStereo(voice, output, binding.Voice.DryGain);
            }
            else
            {
                // Unity's mono nonspatial centre pan is equal-power stereo.
                // Bypassing HRTF also avoids its gain/filter/latency at blend zero.
                float gain = binding.Voice.DryGain * .7071067811865475f;
                for (int i = 0; i < Frame; i++)
                    output[2 * i] = output[2 * i + 1] = signal[i] * gain;
                directTail[voice] = NativeDsp.TailComplete;
            }
            bool wetReady = binding.Voice.Settings.Spatial && geometryReady &&
                            binding.Serial <= Volatile.Read(ref responseResets[voice]);
            if (wetReady)
                pcmWetGeometry[voice] = geometryRevision;
            if (!wetReady)
            {
                Array.Clear(signal, 0, signal.Length);
                Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
            }
            // Continue consuming pending native responses with zero input while
            // reset/geometry is unresolved. Dry PCM is neither stalled nor replayed.
            reflectionTail[voice] = NativeDsp.iplReflectionEffectApply(reflections[voice], ref responses[voice],
                                                                       ref inputs[voice], ref wet[voice], IntPtr.Zero);
            decoderTail[voice] = NativeDsp.iplAmbisonicsDecodeEffectApply(decoders[voice], ref parameters,
                                                                          ref wet[voice], ref stereo[voice]);
            if (wetReady)
                CopyStereo(voice, reflectedMix[voice], binding.Voice?.WetGain ?? 0);
            if (!binding.Voice.Settings.Spatial)
            {
                // Silent IR handoff above does not create an audible room tail.
                reflectionTail[voice] = decoderTail[voice] = NativeDsp.TailComplete;
            }
            return true;
        }
    }
}
