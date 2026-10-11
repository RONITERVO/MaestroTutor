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
        readonly PcmBinding[] pcm = new PcmBinding[VoiceCount], consumedPcm = new PcmBinding[VoiceCount];
        readonly long[] resetAcks = new long[VoiceCount], responseResets = new long[VoiceCount];
        readonly IntPtr[] direct = new IntPtr[VoiceCount];
        long pcmSerial;
        internal long ResetAck(int voice) => Volatile.Read(ref resetAcks[voice]);
        internal bool NeedsPcmResponse => ResetAck(0) > Volatile.Read(ref responseResets[0]) ||
                                          ResetAck(1) > Volatile.Read(ref responseResets[1]);
        internal bool PcmWetReady(int voice) => Volatile.Read(ref pcm[voice]) is PcmBinding binding
                                                && binding.Serial <= Volatile.Read(ref responseResets[voice]) &&
                                                CompletedRevision >= Volatile.Read(ref requiredRevision);
        internal PcmVoice BeginPcm(string id, double startAt, double tail, float dryGain, float wetGain)
        {
            int index = Array.IndexOf(VoiceIds, id);
            if (index < 0 || IsRetired)
                return null;
            var voice = new PcmVoice(startAt, tail, dryGain, wetGain);
            var previous = Interlocked.Exchange(ref pcm[index], new PcmBinding(++pcmSerial, voice));
            previous?.Voice?.Close();
            Volatile.Write(ref playing[index], 0);
            return voice;
        }
        internal bool StopPcm(string id, PcmVoice voice)
        {
            int index = Array.IndexOf(VoiceIds, id);
            if (index < 0 || voice == null || IsRetired || Volatile.Read(ref pcm[index])?.Voice != voice)
                return false;
            voice.Close();
            Volatile.Write(ref pcm[index], new PcmBinding(++pcmSerial, null));
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
        void RenderPcm(int voice, PcmBinding binding, AcousticFrame frame, bool geometryReady,
                       ref NativeDsp.DecodeParams parameters, float[] output, double dspTime)
        {
            ObservePcm(voice, binding);
            Array.Clear(signal, 0, signal.Length);
            bool audible = binding.Voice?.Open == true;
            if (audible)
                audible = binding.Voice.Render(signal, Rate, dspTime);
            renderedPcmAudible[voice] = audible;
            Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
            var listener = frame.Listener;
            var source = voice == 0 ? frame.First : frame.Second;
            var spatial = new NativeDsp.BinauralParams { direction = API.iplCalculateRelativeDirection(
                                                             context, source.origin, listener.origin, listener.ahead,
                                                             listener.up),
                                                         interpolation = 0, spatialBlend = 1, hrtf = hrtf };
            NativeDsp.iplBinauralEffectApply(direct[voice], ref spatial, ref inputs[voice], ref stereo[voice]);
            CopyStereo(voice, output, binding.Voice?.DryGain ?? 0);
            bool wetReady = geometryReady && binding.Serial <= Volatile.Read(ref responseResets[voice]);
            if (!wetReady)
            {
                Array.Clear(signal, 0, signal.Length);
                Marshal.Copy(signal, 0, Marshal.ReadIntPtr(inputs[voice].data), Frame);
            }
            // Continue consuming pending native responses with zero input while
            // reset/geometry is unresolved. Dry PCM is neither stalled nor replayed.
            NativeDsp.iplReflectionEffectApply(reflections[voice], ref responses[voice], ref inputs[voice],
                                               ref wet[voice], IntPtr.Zero);
            NativeDsp.iplAmbisonicsDecodeEffectApply(decoders[voice], ref parameters, ref wet[voice],
                                                     ref stereo[voice]);
            if (wetReady)
                CopyStereo(voice, reflectedMix[voice], binding.Voice?.WetGain ?? 0);
        }
    }
}
