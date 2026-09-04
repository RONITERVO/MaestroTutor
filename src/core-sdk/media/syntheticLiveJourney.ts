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
  getLiveRealtimeInputConfig,
} from './liveModelCompatibility';
import { ContinuousLiveTurnBoundary } from './continuousLiveTurnBoundary';
import { PcmCaptureHandoff, PcmCaptureRouter, type PcmInputSource } from './pcmInput';
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
  /** Begin capture before Live connects, matching browser Whisper/STT ownership. */
  simulateUiSpeechHandoff?: boolean;
  /** Fail unless input capture elapsed at real microphone pace. */
  requireRealtimeInputPacing?: boolean;
  /** Pace the model's 24 kHz PCM through a real-time headless playback sink. */
  playModelAudioRealtime?: boolean;
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
  const boundary = gateEnabled ? new ContinuousLiveTurnBoundary() : null;
  const speechActivity = gateEnabled ? new SpeechActivityTracker({ sampleRate: INPUT_SAMPLE_RATE }) : null;
  const semanticSpeech = input.semanticSpeech ?? true;
  const simulateUiSpeechHandoff = input.simulateUiSpeechHandoff === true;
  const playModelAudioRealtime = input.playModelAudioRealtime === true;
  const requireRealtimeInputPacing = input.requireRealtimeInputPacing === true;
  if (simulateUiSpeechHandoff && (!gateEnabled || !semanticSpeech)) {
    throw new Error('UI speech handoff requires the semantic speech gate.');
  }
  const heldPackets: Int16Array[] = [];
  const sentPackets: Int16Array[] = [];
  const modelAudioChunks: string[] = [];
  let inputTranscript = '';
  let outputTranscript = '';
  let inputTranscriptDeltaCount = 0;
  let outputTranscriptDeltaCount = 0;
  let serverMessageCount = 0;
  const serverMessageKinds = new Set<string>();
  let modelAudioSampleCount = 0;
  let sentVideoFrameCount = 0;
  let videoFramesSent = false;
  let logicalNow = runtime.clock.now();
  let gatedPackets = 0;
  let streamEnds = 0;
  let audioSentSinceLastStreamEnd = false;
  let inputCaptureStartedAt: number | null = null;
  let inputCaptureEndedAt: number | null = null;
  let localTriggerAt: number | null = null;
  let providerConnectStartedAt: number | null = null;
  let providerConnectedAt: number | null = null;
  let connectionHandoffPackets = 0;
  let connectionHandoffSamples = 0;
  let modelPlaybackQueue: Promise<void> = Promise.resolve();
  let modelPlaybackStartedAt: number | null = null;
  let modelPlaybackCompletedAt: number | null = null;
  let modelPlaybackDrainWaitMs = 0;
  let resolved = false;
  let resolveTurn!: () => void;
  let rejectTurn!: (error: Error) => void;
  const turnComplete = new Promise<void>((resolve, reject) => {
    resolveTurn = resolve;
    rejectTurn = reject;
  });
  const attachLiveFailureEvidence = (value: unknown): Error & {
    operationId: string;
    liveDiagnostics: Record<string, unknown>;
  } => {
    const error = value instanceof Error ? value : new Error(String(value));
    return Object.assign(error, {
      operationId,
      liveDiagnostics: {
        serverMessageCount,
        serverMessageKinds: [...serverMessageKinds].sort(),
        inputTranscriptLength: inputTranscript.trim().length,
        outputTranscriptLength: outputTranscript.trim().length,
        modelAudioSampleCount,
        providerConnectStartedAt,
        providerConnectedAt,
      },
    });
  };

  const enqueueModelPlayback = (samples: number) => {
    if (!playModelAudioRealtime || samples <= 0) return;
    modelPlaybackQueue = modelPlaybackQueue.then(async () => {
      modelPlaybackStartedAt ??= runtime.clock.now();
      await runtime.clock.sleep(samples / 24_000 * 1_000);
      modelPlaybackCompletedAt = runtime.clock.now();
    });
  };

  let router: PcmCaptureRouter | null = null;
  let sourceRun: Promise<void> | null = null;
  let uiCaptureHandoff: PcmCaptureHandoff | null = null;

  if (simulateUiSpeechHandoff) {
    uiCaptureHandoff = new PcmCaptureHandoff();
    const triggerActivity = new SpeechActivityTracker({ sampleRate: INPUT_SAMPLE_RATE });
    let triggerResolved = false;
    let resolveTrigger!: () => void;
    const triggerReady = new Promise<void>(resolve => { resolveTrigger = resolve; });
    let triggerNow = runtime.clock.now();
    inputCaptureStartedAt = triggerNow;
    router = new PcmCaptureRouter({
      runtime,
      operationId,
      sink: frame => {
        uiCaptureHandoff!.push(frame.pcm);
        if (triggerResolved) return;
        triggerNow += frame.pcm.length / INPUT_SAMPLE_RATE * 1_000;
        const active = isSpeechLike(measureEnergy(frame.pcm), DEFAULT_SPEECH_GATE);
        if (triggerActivity.observe(frame.pcm.length, active, triggerNow).hasMinimumSpeech) {
          triggerResolved = true;
          localTriggerAt = runtime.clock.now();
          runtime.events.emit({
            operationId,
            journey: 'live',
            phase: 'speech.local-trigger',
            data: { capturedSamples: Math.round((triggerNow - inputCaptureStartedAt!) * INPUT_SAMPLE_RATE / 1_000) },
          });
          resolveTrigger();
        }
      },
    });
    sourceRun = router.attach(input.source);
    await Promise.race([
      triggerReady,
      sourceRun.then(() => {
        if (!triggerResolved) throw new Error('Synthetic UI flow ended before sustained speech was confirmed.');
      }),
    ]);
  }

  runtime.events.emit({
    operationId,
    journey: 'live',
    phase: 'session.connecting',
    data: { model, gateInputOnSpeech: gateEnabled, source: input.source.kind },
  });
  providerConnectStartedAt = runtime.clock.now();
  let session: Awaited<ReturnType<CoreGeminiClient['live']['connect']>>;
  try {
    session = await ai.live.connect({
    model,
    liveOpenReason: createLiveOpenReason(input.liveOpenTrigger, { requestId: operationId }),
    config: {
      responseModalities: [Modality.AUDIO],
      inputAudioTranscription: {},
      outputAudioTranscription: {},
      realtimeInputConfig: getLiveRealtimeInputConfig(false, gateEnabled),
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
        serverMessageCount += 1;
        for (const key of Object.keys(message || {})) serverMessageKinds.add(key);
        if (message?.setupComplete) {
          runtime.events.emit({ operationId, journey: 'live', phase: 'session.setup-complete' });
        }
        if (message?.goAway) {
          runtime.events.emit({
            operationId,
            journey: 'live',
            phase: 'session.go-away',
            data: { hasTimeLeft: Boolean(message.goAway.timeLeft) },
          });
        }
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
              enqueueModelPlayback(samples);
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
        rejectTurn(attachLiveFailureEvidence(error));
      },
      onclose: () => {
        if (resolved) return;
        resolved = true;
        rejectTurn(attachLiveFailureEvidence(new Error('Live session closed before turn completion.')));
      },
    },
    });
    providerConnectedAt = runtime.clock.now();
  } catch (error) {
    await router?.stop();
    throw attachLiveFailureEvidence(error);
  }

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
    audioSentSinceLastStreamEnd = true;
    session.sendRealtimeInput({
      audio: { data: encodePcm16LeBase64(pcm), mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}` },
    });
  };
  const endAudioStream = (reason: 'gate-closed' | 'source-ended') => {
    // A gate close may already have ended the only audio turn. Sending another
    // empty boundary when the finite source finishes can make Live complete an
    // empty turn before returning the model response for the heard speech.
    if (streamEnds > 0 && !audioSentSinceLastStreamEnd) return;
    if (boundary?.isOpen) {
      session.sendRealtimeInput({ activityEnd: {} });
      boundary.reset();
    }
    session.sendRealtimeInput({ audioStreamEnd: true });
    streamEnds += 1;
    audioSentSinceLastStreamEnd = false;
    runtime.events.emit({
      operationId,
      journey: 'live',
      phase: 'audio.input-stream-end',
      data: { reason, streamEnds },
    });
  };
  const packetizer = new RealtimePcmPacketizer({
    sampleRate: INPUT_SAMPLE_RATE,
    packetDurationMs: PACKET_DURATION_MS,
    maxWaitMs: PACKET_MAX_WAIT_MS,
    paceOutput: requireRealtimeInputPacing,
    pacingClock: runtime.clock,
    onPacket: sendPacket,
  });
  const routeCapturedPacket = async (packet: Int16Array) => {
    logicalNow += packet.length / INPUT_SAMPLE_RATE * 1_000;
    if (!gate || !boundary) {
      packetizer.push(packet);
      return;
    }

    const energy = measureEnergy(packet);
    const packetIsSpeech = isSpeechLike(energy, DEFAULT_SPEECH_GATE);
    if (boundary.shouldBeginClosing(logicalNow)) {
      boundary.beginClosing(logicalNow);
      await packetizer.flushPending();
      session.sendRealtimeInput({ activityEnd: {} });
      endAudioStream('gate-closed');
      packetizer.resetPacingEpoch();
      boundary.finishClosing();
      gate.rejectSpeech(logicalNow);
      heldPackets.length = 0;
      speechActivity!.reset();
      gatedPackets += 1;
      return;
    }
    if (boundary.isOpen) {
      // A semantic speech result owns the boundary; VAD may extend it but never
      // filters packets inside the already-confirmed continuous turn.
      if (packetIsSpeech) boundary.refreshConfirmedSpeech(logicalNow);
      packetizer.push(packet);
      return;
    }

    const activity = speechActivity!.observe(packet.length, packetIsSpeech, logicalNow);
    if (activity.candidateReset && gate.isAwaitingConfirmation) {
      gate.rejectSpeech(logicalNow);
      heldPackets.length = 0;
    }
    const decision = gate.evaluate(energy, logicalNow);
    gatedPackets += 1;
    if (decision.send) {
      gate.forceClose();
      return;
    }
    if (decision.reason === 'cooldown' && !activity.active) {
      heldPackets.length = 0;
      speechActivity!.reset();
      return;
    }
    heldPackets.push(packet);
    const recent = recentPcmPackets(heldPackets, PREROLL_SAMPLES);
    heldPackets.splice(0, heldPackets.length, ...recent);
    if (decision.reason !== 'awaiting-confirmation' || !activity.hasMinimumSpeech) return;
    if (!semanticSpeech) {
      gate.rejectSpeech(logicalNow);
      heldPackets.length = 0;
      speechActivity!.reset();
      return;
    }
    if (!gate.confirmSpeech(logicalNow) || !boundary.openFromConfirmedSpeech(logicalNow)) return;
    session.sendRealtimeInput({ activityStart: {} });
    const replay = mergeInt16Arrays(heldPackets);
    heldPackets.length = 0;
    speechActivity!.reset();
    for (let offset = 0; offset < replay.length; offset += REPLAY_CHUNK_SAMPLES) {
      packetizer.push(replay.slice(offset, offset + REPLAY_CHUNK_SAMPLES));
    }
  };
  router ??= new PcmCaptureRouter({
    runtime,
    operationId,
    sink: frame => routeCapturedPacket(frame.pcm),
  });

  try {
    if (uiCaptureHandoff && sourceRun) {
      gate!.openFromConfirmedTrigger(logicalNow);
      boundary!.openFromConfirmedSpeech(logicalNow);
      session.sendRealtimeInput({ activityStart: {} });
      const handoff = uiCaptureHandoff.transferTo(pcm => packetizer.push(pcm));
      connectionHandoffPackets = handoff.bufferedPackets;
      connectionHandoffSamples = handoff.bufferedSamples;
      runtime.events.emit({
        operationId,
        journey: 'live',
        phase: 'speech.capture-handoff',
        data: { ...handoff },
      });
      await sourceRun;
    } else {
      inputCaptureStartedAt = runtime.clock.now();
      await router.attach(input.source);
    }
    inputCaptureEndedAt = runtime.clock.now();
    await packetizer.flushPending();
    endAudioStream('source-ended');
    const timeoutMs = Math.max(1_000, input.timeoutMs ?? 45_000);
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    await Promise.race([
      turnComplete,
      new Promise<never>((_, reject) => {
        timeoutId = globalThis.setTimeout(() => reject(attachLiveFailureEvidence(new Error(
          `Live turn timed out after ${timeoutMs}ms with ${serverMessageCount} server messages`
          + ` (${[...serverMessageKinds].sort().join(', ') || 'no message kinds'}).`,
        ))), timeoutMs);
      }),
    ]).finally(() => {
      if (timeoutId) globalThis.clearTimeout(timeoutId);
    });
    if (playModelAudioRealtime) {
      const drainStartedAt = runtime.clock.now();
      await modelPlaybackQueue;
      modelPlaybackDrainWaitMs = Math.max(0, runtime.clock.now() - drainStartedAt);
    }
    if (!outputTranscript.trim() && modelAudioChunks.length === 0) {
      throw new Error(
        `Live turn completed without model output after ${serverMessageCount} server messages`
        + ` (${[...serverMessageKinds].sort().join(', ') || 'no message kinds'}).`,
      );
    }
    const packetStats = packetizer.getStats();
    const sentAudio = mergeInt16Arrays(sentPackets);
    const inputAudioDurationMs = packetStats.totalInputSamples / INPUT_SAMPLE_RATE * 1_000;
    const inputCaptureElapsedMs = inputCaptureStartedAt === null || inputCaptureEndedAt === null
      ? null
      : Math.max(0, inputCaptureEndedAt - inputCaptureStartedAt);
    const modelAudioDurationMs = modelAudioSampleCount / 24_000 * 1_000;
    const modelPlaybackElapsedMs = modelPlaybackStartedAt === null || modelPlaybackCompletedAt === null
      ? 0
      : Math.max(0, modelPlaybackCompletedAt - modelPlaybackStartedAt);
    const inputPacingPassed = !requireRealtimeInputPacing || (
      inputCaptureElapsedMs !== null
      && inputCaptureElapsedMs >= inputAudioDurationMs * 0.9
      && inputCaptureElapsedMs <= inputAudioDurationMs * 1.1 + 250
    );
    const modelPlaybackPassed = !playModelAudioRealtime || (
      modelAudioSampleCount > 0
      && modelPlaybackElapsedMs >= modelAudioDurationMs * 0.9
    );
    const uiSpeechHandoffPassed = !simulateUiSpeechHandoff || connectionHandoffSamples > 0;
    const providerInputExpectedSpanMs = Math.max(
      0,
      (packetStats.totalOutputSamples - packetStats.maxPacketSamples) / INPUT_SAMPLE_RATE * 1_000,
    );
    const providerInputPacingPassed = !requireRealtimeInputPacing || (
      packetStats.pacedOutput
      && packetStats.outputPacingElapsedMs >= providerInputExpectedSpanMs * 0.9
    );
    const realtimeEvidence = {
      required: requireRealtimeInputPacing || playModelAudioRealtime || simulateUiSpeechHandoff,
      inputPacingPassed,
      providerInputPacingPassed,
      modelPlaybackPassed,
      uiSpeechHandoffPassed,
      passed: inputPacingPassed
        && providerInputPacingPassed
        && modelPlaybackPassed
        && uiSpeechHandoffPassed,
    };
    if (realtimeEvidence.required && !realtimeEvidence.passed) {
      throw new Error(`Live real-time evidence failed: ${JSON.stringify({
        ...realtimeEvidence,
        serverMessageCount,
        serverMessageKinds: [...serverMessageKinds].sort(),
        inputTranscriptLength: inputTranscript.trim().length,
        outputTranscriptLength: outputTranscript.trim().length,
        modelAudioSampleCount,
      })}`);
    }
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
      serverMessageCount,
      serverMessageKinds: [...serverMessageKinds].sort(),
      sentVideoFrameCount,
      ...(input.includeModelAudio ? { modelAudioChunksBase64: modelAudioChunks } : {}),
      gate: { enabled: gateEnabled, semanticSpeech, gatedPackets, streamEnds },
      packetizer: packetStats,
      realtimeEvidence,
      timing: {
        inputAudioDurationMs,
        inputCaptureElapsedMs,
        uiSpeechHandoff: simulateUiSpeechHandoff,
        localTriggerAt,
        providerConnectStartedAt,
        providerConnectedAt,
        providerConnectMs: providerConnectStartedAt === null || providerConnectedAt === null
          ? null
          : Math.max(0, providerConnectedAt - providerConnectStartedAt),
        connectionHandoffPackets,
        connectionHandoffSamples,
        modelAudioDurationMs,
        modelPlaybackRealtime: playModelAudioRealtime,
        modelPlaybackStartedAt,
        modelPlaybackCompletedAt,
        modelPlaybackElapsedMs,
        modelPlaybackDrainWaitMs,
      },
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
  } catch (error) {
    throw attachLiveFailureEvidence(error);
  } finally {
    await router.stop();
    packetizer.dispose();
    try { session.close(); } catch { /* already closed */ }
  }
};
