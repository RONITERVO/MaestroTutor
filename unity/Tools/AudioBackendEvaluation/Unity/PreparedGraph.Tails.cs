// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using SteamAudio;

namespace Maestro.AudioEvaluation
{
    internal sealed partial class PreparedGraph
    {
        // Only the audio consumer advances or resets a voice phase.
        enum PcmRenderPhase
        {
            Input,
            Draining,
            Drained
        }
        readonly PcmRenderPhase[] pcmTailPhase = new PcmRenderPhase[MaximumVoices];
        readonly int[] directTail = new int[MaximumVoices], reflectionTail = new int[MaximumVoices],
                       decoderTail = new int[MaximumVoices];
        readonly long[] pcmWetGeometry = new long[MaximumVoices];
        readonly double[] pcmDspUntil = new double[MaximumVoices];
        readonly bool[] renderedPcmFinal = new bool[MaximumVoices];
        void ResetPcmTail(int voice)
        {
            pcmTailPhase[voice] = PcmRenderPhase.Input;
            pcmWetGeometry[voice] = -1;
            directTail[voice] = reflectionTail[voice] = decoderTail[voice] = NativeDsp.TailComplete;
            pcmDspUntil[voice] = double.NegativeInfinity;
        }
        void ConsumeSilentResponse(int voice, ref NativeDsp.DecodeParams parameters)
        {
            // The source input is already zero. A drained/stopped slot still
            // consumes IR publications, but never reopens its completed output.
            NativeDsp.iplReflectionEffectApply(reflections[voice], ref responses[voice], ref inputs[voice],
                                               ref wet[voice], IntPtr.Zero);
            NativeDsp.iplAmbisonicsDecodeEffectApply(decoders[voice], ref parameters, ref wet[voice],
                                                     ref stereo[voice]);
        }
        void RenderPcmTail(int voice, PcmVoice source, bool geometryReady, ref NativeDsp.DecodeParams parameters,
                           float[] output)
        {
            if (directTail[voice] != NativeDsp.TailComplete)
            {
                directTail[voice] = NativeDsp.iplBinauralEffectGetTail(direct[voice], ref stereo[voice]);
                // TailComplete describes the state AFTER this final output block.
                CopyStereo(voice, output, source.DryGain);
            }
            if (reflectionTail[voice] != NativeDsp.TailComplete)
            {
                reflectionTail[voice] =
                    NativeDsp.iplReflectionEffectGetTail(reflections[voice], ref wet[voice], IntPtr.Zero);
                // Even the last reflection block is input to the decoder.
                decoderTail[voice] = NativeDsp.iplAmbisonicsDecodeEffectApply(decoders[voice], ref parameters,
                                                                              ref wet[voice], ref stereo[voice]);
                if (geometryReady)
                    CopyStereo(voice, reflectedMix[voice], source.WetGain);
            }
            else if (decoderTail[voice] != NativeDsp.TailComplete)
            {
                decoderTail[voice] = NativeDsp.iplAmbisonicsDecodeEffectGetTail(decoders[voice], ref stereo[voice]);
                if (geometryReady)
                    CopyStereo(voice, reflectedMix[voice], source.WetGain);
            }
            if (directTail[voice] == NativeDsp.TailComplete && reflectionTail[voice] == NativeDsp.TailComplete &&
                decoderTail[voice] == NativeDsp.TailComplete)
            {
                pcmTailPhase[voice] = PcmRenderPhase.Drained;
                renderedPcmFinal[voice] = true;
            }
        }
    }
}
