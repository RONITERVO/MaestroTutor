// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { Modality } from '@google/genai';
import { getGeminiModels } from '../../core/config/models';
import {
  DEFAULT_SPEECH_GATE,
  isSpeechLike,
  SpeechGate,
  measureEnergy,
} from '../../../shared/audio/speechGate';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../runtime';
import { mergeInt16Arrays } from './audioProcessing';
import {
  getLiveConversationThinkingConfig,
  getLiveMinimalThinkingConfig,
} from './liveModelCompatibility';
import { PcmCaptureRouter, type PcmInputSource } from './pcmInput';
import { RealtimePcmPacketizer } from './realtimePcmPacketizer';
import {
  OBSERVER_SPEECH_PREROLL_MS,
  recentPcmPackets,
  SpeechActivityTracker,
} from './observerSpeechDetection';
import {
  createLiveOpenReason,
  type HeadlessLiveOpenTrigger,
} from '../../../shared/liveOpenReason';

const INPUT_SAMPLE_RATE = 16_000;
const PACKET_DURATION_MS = 100;
const PACKET_MAX_WAIT_MS = 120;
const PREROLL_SAMPLES = Math.round(INPUT_SAMPLE_RATE * OBSERVER_SPEECH_PREROLL_MS / 1_000);
const REPLAY_CHUNK_SAMPLES = 4_096;

const encodePcm16LeBase64 = (pcm: Int16Array): string => {
  const bytes = new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  let binary = '';
  const chunkSize = 0x8000;
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(offset, Math.min(bytes.length, offset + chunkSize)));
  }
  return globalThis.btoa(binary);
};

export interface SyntheticLiveJourneyInput {
  liveOpenTrigger: HeadlessLiveOpenTrigger;
  source: PcmInputSource;
  systemInstruction?: string;
  model?: string;
  gateInputOnSpeech?: boolean;
  semanticSpeech?: boolean;
  timeoutMs?: number;
  includeModelAudio?: boolean;
  videoFrames?: Array<{ dataBase64: string; mimeType?: string }>;
  thinkingMode?: 'minimal' | 'conversation';
  voiceName?: string;
}

export const runSyntheticLiveJourney = async (
  ai: CoreGeminiClient,
  input: SyntheticLiveJourneyInput,
  options: { runtime?: CoreRuntime } = {},
) => {
  if (input.source.sampleRate !== INPUT_SAMPLE_RATE) {
    throw new Error(`Synthetic Live input must be ${INPUT_SAMPLE_RATE} Hz PCM16 mono.`);
  }
  const runtime = options.runtime || createCoreRuntime();
  const operationId = runtime.ids.create('synthetic-live');
  const model = input.model || getGeminiModels().audio.stt;
  const gateEnabled = input.gateInputOnSpeech ?? true;
  const gate = gateEnabled ? new SpeechGate({ requireConfirmation: true }) : null;
  const speechActivity = gateEnabled ? new SpeechActivityTracker({ sampleRate: INPUT_SAMPLE_RATE }) : null;
  const semanticSpeech = input.semanticSpeech ?? true;
  const heldPackets: Int16Array[] = [];
  const sentPackets: Int16Array[] = [];
  const modelAudioChunks: string[] = [];
  let inputTranscript = '';
  let outputTranscript = '';
  let inputTranscriptDeltaCount = 0;
  let outputTranscriptDeltaCount = 0;
  let modelAudioSampleCount = 0;
  let sentVideoFrameCount = 0;
  let videoFramesSent = false;
  let logicalNow = runtime.clock.now();
  let gatedPackets = 0;
  let streamEnds = 0;
  let resolved = false;
  let resolveTurn!: () => void;
  let rejectTurn!: (error: Error) => void;
  const turnComplete = new Promise<void>((resolve, reject) => {
    resolveTurn = resolve;
    rejectTurn = reject;
  });

  runtime.events.emit({
    operationId,
    journey: 'live',
    phase: 'session.connecting',
    data: { model, gateInputOnSpeech: gateEnabled, source: input.source.kind },
  });
  const session = await ai.live.connect({
    model,
    liveOpenReason: createLiveOpenReason(input.liveOpenTrigger, { requestId: operationId }),
    config: {
      responseModalities: [Modality.AUDIO],
      inputAudioTranscription: {},
      outputAudioTranscription: {},
      thinkingConfig: input.thinkingMode === 'conversation'
        ? getLiveConversationThinkingConfig(model)
        : getLiveMinimalThinkingConfig(model),
      speechConfig: input.voiceName
        ? { voiceConfig: { prebuiltVoiceConfig: { voiceName: input.voiceName } } }
        : undefined,
      systemInstruction: input.systemInstruction
        || 'You are a smart parrot. Listen to the user input and repeat it back, correcting errors while preserving the original language. Do not answer questions; return only the corrected utterance.',
    },
    callbacks: {
      onopen: () => runtime.events.emit({ operationId, journey: 'live', phase: 'session.opened' }),
      onmessage: (rawMessage: unknown) => {
        const message = rawMessage as any;
        const inputText = message?.serverContent?.inputTranscription?.text;
        const outputText = message?.serverContent?.outputTranscription?.text;
        if (typeof inputText === 'string' && inputText) {
          inputTranscript += inputText;
          inputTranscriptDeltaCount += 1;
          runtime.events.emit({
            operationId, journey: 'live', phase: 'transcript.input-delta',
            data: { deltaLength: inputText.length, fullLength: inputTranscript.length },
          });
        }
        if (typeof outputText === 'string' && outputText) {
          outputTranscript += outputText;
          outputTranscriptDeltaCount += 1;
          runtime.events.emit({
            operationId, journey: 'live', phase: 'transcript.output-delta',
            data: { deltaLength: outputText.length, fullLength: outputTranscript.length },
          });
        }
        const parts = message?.serverContent?.modelTurn?.parts;
        if (Array.isArray(parts)) {
          for (const part of parts) {
            const data = part?.inlineData?.data;
            const mimeType = part?.inlineData?.mimeType;
            if (typeof data === 'string' && typeof mimeType === 'string' && mimeType.startsWith('audio/')) {
              modelAudioChunks.push(data);
              const byteLength = Math.floor(globalThis.atob(data).length / 2) * 2;
              const samples = byteLength / 2;
              modelAudioSampleCount += samples;
              runtime.events.emit({
                operationId, journey: 'live', phase: 'audio.output-chunk',
                data: { samples, totalSamples: modelAudioSampleCount },
              });
            }
          }
        }
        if (message?.serverContent?.turnComplete && !resolved) {
          resolved = true;
          resolveTurn();
        }
      },
      onerror: (error: unknown) => {
        if (resolved) return;
        resolved = true;
        rejectTurn(error instanceof Error ? error : new Error(String(error)));
      },
      onclose: () => {
        if (resolved) return;
        resolved = true;
        rejectTurn(new Error('Live session closed before turn completion.'));
      },
    },
  });

  const sendVideoFrames = () => {
    if (videoFramesSent) return;
    videoFramesSent = true;
    for (const frame of input.videoFrames || []) {
      const data = frame.dataBase64.replace(/^data:[^;]+;base64,/i, '');
      if (!data) continue;
      const inferredMimeType = /^data:([^;,]+)(?:;[^,]*)?,/i.exec(frame.dataBase64)?.[1];
      const mimeType = frame.mimeType?.trim() || inferredMimeType || 'image/jpeg';
      session.sendRealtimeInput({ video: { data, mimeType } });
      sentVideoFrameCount += 1;
      runtime.events.emit({
        operationId, journey: 'live', phase: 'video.input-frame',
        data: { mimeType, frame: sentVideoFrameCount },
      });
    }
  };
  const sendPacket = async (pcm: Int16Array) => {
    sendVideoFrames();
    sentPackets.push(pcm.slice());
    session.sendRealtimeInput({
      audio: { data: encodePcm16LeBase64(pcm), mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}` },
    });
  };
  const packetizer = new RealtimePcmPacketizer({
    sampleRate: INPUT_SAMPLE_RATE,
    packetDurationMs: PACKET_DURATION_MS,
    maxWaitMs: PACKET_MAX_WAIT_MS,
    onPacket: async packet => {
      logicalNow += packet.length / INPUT_SAMPLE_RATE * 1_000;
      if (!gate) {
        await sendPacket(packet);
        return;
      }
      const energy = measureEnergy(packet);
      const activity = speechActivity!.observe(
        packet.length,
        isSpeechLike(energy, DEFAULT_SPEECH_GATE),
        logicalNow,
      );
      if (activity.candidateReset && gate.isAwaitingConfirmation) {
        gate.rejectSpeech(logicalNow);
        heldPackets.length = 0;
      }
      const decision = gate.evaluate(energy, logicalNow);
      if (decision.send) {
        await sendPacket(packet);
        return;
      }
      gatedPackets += 1;
      if (decision.closing) {
        session.sendRealtimeInput({ audioStreamEnd: true });
        streamEnds += 1;
        heldPackets.length = 0;
        speechActivity!.reset();
        return;
      }
      if (decision.reason === 'playback') {
        heldPackets.length = 0;
        speechActivity!.reset();
        return;
      }
      if (decision.reason === 'cooldown') {
        if (!activity.active) {
          heldPackets.length = 0;
          speechActivity!.reset();
          return;
        }
        heldPackets.push(packet);
        const recent = recentPcmPackets(heldPackets, PREROLL_SAMPLES);
        heldPackets.splice(0, heldPackets.length, ...recent);
        return;
      }
      heldPackets.push(packet);
      const recent = recentPcmPackets(heldPackets, PREROLL_SAMPLES);
      heldPackets.splice(0, heldPackets.length, ...recent);
      if (decision.reason !== 'awaiting-confirmation') return;
      if (!activity.hasMinimumSpeech) return;
      if (!semanticSpeech) {
        gate.rejectSpeech(logicalNow);
        heldPackets.length = 0;
        speechActivity!.reset();
        return;
      }
      if (!gate.confirmSpeech(logicalNow)) return;
      const replay = mergeInt16Arrays(heldPackets);
      heldPackets.length = 0;
      speechActivity!.reset();
      for (let offset = 0; offset < replay.length; offset += REPLAY_CHUNK_SAMPLES) {
        await sendPacket(replay.slice(offset, offset + REPLAY_CHUNK_SAMPLES));
      }
    },
  });
  const router = new PcmCaptureRouter({
    runtime,
    operationId,
    sink: frame => packetizer.push(frame.pcm),
  });

  try {
    await router.attach(input.source);
    await packetizer.flushPending();
    session.sendRealtimeInput({ audioStreamEnd: true });
    streamEnds += 1;
    const timeoutMs = Math.max(1_000, input.timeoutMs ?? 45_000);
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    await Promise.race([
      turnComplete,
      new Promise<never>((_, reject) => {
        timeoutId = globalThis.setTimeout(() => reject(new Error(`Live turn timed out after ${timeoutMs}ms.`)), timeoutMs);
      }),
    ]).finally(() => {
      if (timeoutId) globalThis.clearTimeout(timeoutId);
    });
    if (!outputTranscript.trim() && modelAudioChunks.length === 0) {
      throw new Error('Live turn completed without model output.');
    }
    const packetStats = packetizer.getStats();
    const sentAudio = mergeInt16Arrays(sentPackets);
    const result = {
      operationId,
      inputTranscript: inputTranscript.trim(),
      outputTranscript: outputTranscript.trim(),
      transcript: outputTranscript.trim() || inputTranscript.trim(),
      inputSamples: input.source.kind === 'synthetic' ? packetStats.totalInputSamples : undefined,
      sentSamples: sentAudio.length,
      modelAudioChunkCount: modelAudioChunks.length,
      modelAudioSampleCount,
      inputTranscriptDeltaCount,
      outputTranscriptDeltaCount,
      sentVideoFrameCount,
      ...(input.includeModelAudio ? { modelAudioChunksBase64: modelAudioChunks } : {}),
      gate: { enabled: gateEnabled, semanticSpeech, gatedPackets, streamEnds },
      packetizer: packetStats,
    };
    runtime.events.emit({
      operationId,
      journey: 'live',
      phase: 'session.completed',
      data: {
        transcriptLength: result.transcript.length,
        sentSamples: result.sentSamples,
        modelAudioChunkCount: result.modelAudioChunkCount,
        modelAudioSampleCount,
        inputTranscriptDeltaCount,
        outputTranscriptDeltaCount,
        sentVideoFrameCount,
        gatedPackets,
      },
    });
    return result;
  } finally {
    await router.stop();
    packetizer.dispose();
    try { session.close(); } catch { /* already closed */ }
  }
};
