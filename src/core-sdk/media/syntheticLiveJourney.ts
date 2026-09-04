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
  SemanticSpeechCapture,
} from './observerSpeechDetection';
import {
  createLiveOpenReason,
  type HeadlessLiveOpenTrigger,
} from '../../../shared/liveOpenReason';
import {
  mergeLiveProviderTurnUsage,
  sumLiveProviderTurnUsage,
  type LiveGatewayUsageCheckpoint,
} from '../../../shared/billing/liveGateway';
import { getLiveCostControlConfig } from '../../../shared/liveCostControls';
import { LIVE_TURN_CALLBACK_QUIET_MS } from './liveTurnFinalizer';

const INPUT_SAMPLE_RATE = 16_000;
const PACKET_DURATION_MS = 100;
const PACKET_MAX_WAIT_MS = 120;
const REPLAY_CHUNK_SAMPLES = 4_096;
const PROVIDER_TURN_QUIET_MS = LIVE_TURN_CALLBACK_QUIET_MS;

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
  /** @deprecated A transport owns one turn; additional sources are rejected. */
  additionalSources?: PcmInputSource[];
  systemInstruction?: string;
  model?: string;
  gateInputOnSpeech?: boolean;
  /** Disable provider VAD and bound each finite synthetic source explicitly. */
  manualActivityBoundaries?: boolean;
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
  /** Camera frames to send once for each corresponding microphone turn. */
  videoFramesByTurn?: Array<Array<{ dataBase64: string; mimeType?: string }>>;
  thinkingMode?: 'minimal' | 'conversation';
  voiceName?: string;
}

export const runSyntheticLiveJourney = async (
  ai: CoreGeminiClient,
  input: SyntheticLiveJourneyInput,
  options: { runtime?: CoreRuntime; operationId?: string } = {},
) => {
  const sources = [input.source, ...(input.additionalSources || [])];
  if (sources.length !== 1) {
    throw new Error('Live connections accept one turn. Start a fresh chat turn to rebuild context.');
  }
  if (sources.some(source => source.sampleRate !== INPUT_SAMPLE_RATE)) {
    throw new Error(`Synthetic Live input must be ${INPUT_SAMPLE_RATE} Hz PCM16 mono.`);
  }
  const runtime = options.runtime || createCoreRuntime();
  const operationId = options.operationId || runtime.ids.create('synthetic-live');
  const model = input.model || getGeminiModels().audio.stt;
  const gateEnabled = input.gateInputOnSpeech ?? true;
  const manualActivityBoundaries = gateEnabled || input.manualActivityBoundaries === true;
  const gate = gateEnabled ? new SpeechGate({ requireConfirmation: true }) : null;
  const boundary = gateEnabled ? new ContinuousLiveTurnBoundary() : null;
  const pendingSpeech = gateEnabled ? new SemanticSpeechCapture({ sampleRate: INPUT_SAMPLE_RATE }) : null;
  const semanticSpeech = input.semanticSpeech ?? true;
  const simulateUiSpeechHandoff = input.simulateUiSpeechHandoff === true;
  const playModelAudioRealtime = input.playModelAudioRealtime === true;
  const requireRealtimeInputPacing = input.requireRealtimeInputPacing === true;
  if (simulateUiSpeechHandoff && (!gateEnabled || !semanticSpeech)) {
    throw new Error('UI speech handoff requires the semantic speech gate.');
  }
  const sentPackets: Int16Array[] = [];
  const modelAudioChunks: string[] = [];
  let inputTranscript = '';
  let outputTranscript = '';
  let inputTranscriptDeltaCount = 0;
  let outputTranscriptDeltaCount = 0;
  let serverMessageCount = 0;
  let providerActivitySequence = 0;
  const serverMessageKinds = new Set<string>();
  const providerUsageSnapshots: Array<{
    turn: number;
    afterCompletedTurns: number;
    usageMetadata: Record<string, unknown>;
  }> = [];
  let providerTurnUsage: LiveGatewayUsageCheckpoint['providerTurnUsage'] = [];
  let modelAudioSampleCount = 0;
  let sentVideoFrameCount = 0;
  const videoFramesSentForTurns = new Set<number>();
  let activeTurnIndex = 0;
  let logicalNow = runtime.clock.now();
  let gatedPackets = 0;
  let streamEnds = 0;
  let audioSentSinceLastStreamEnd = false;
  let inputCaptureStartedAt: number | null = null;
  let localTriggerAt: number | null = null;
  let providerConnectStartedAt: number | null = null;
  let providerConnectedAt: number | null = null;
  let connectionHandoffPackets = 0;
  let connectionHandoffSamples = 0;
  let modelPlaybackQueue: Promise<void> = Promise.resolve();
  let modelPlaybackStartedAt: number | null = null;
  let modelPlaybackCompletedAt: number | null = null;
  let modelPlaybackDrainWaitMs = 0;
  let completedTurnCount = 0;
  let terminalError: Error | null = null;
  const turnWaiters = new Map<number, { resolve: () => void; reject: (error: Error) => void }>();
  const waitForTurn = (turnNumber: number): Promise<void> => {
    if (completedTurnCount >= turnNumber) return Promise.resolve();
    if (terminalError) return Promise.reject(terminalError);
    return new Promise<void>((resolve, reject) => {
      turnWaiters.set(turnNumber, { resolve, reject });
    });
  };
  const waitForProviderTurnCallbacks = async (): Promise<number> => {
    // The SDK can dispatch transcription/audio callbacks that were already in
    // flight after the callback carrying turnComplete. Wait for one quiet
    // window before snapshotting transcripts or the playback queue.
    let observedSequence = providerActivitySequence;
    let drainWaitMs = 0;
    while (true) {
      await runtime.clock.sleep(PROVIDER_TURN_QUIET_MS);
      const drainStartedAt = runtime.clock.now();
      await modelPlaybackQueue;
      drainWaitMs += Math.max(0, runtime.clock.now() - drainStartedAt);
      if (providerActivitySequence === observedSequence) return drainWaitMs;
      observedSequence = providerActivitySequence;
    }
  };
  const failSession = (error: Error) => {
    if (terminalError) return;
    terminalError = error;
    for (const waiter of turnWaiters.values()) waiter.reject(error);
    turnWaiters.clear();
  };
  const turnCompletionTimes: number[] = [];
  const lastModelAudioByteTimes: Array<number | null> = [];
  const turnResults: Array<Record<string, unknown>> = [];
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
    gate?.notePlayback(true, logicalNow);
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
    const triggerCapture = new SemanticSpeechCapture({ sampleRate: INPUT_SAMPLE_RATE });
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
        triggerCapture.append(frame.pcm);
        if (triggerCapture.beginWhisperCheck(INPUT_SAMPLE_RATE)) {
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
        if (!triggerResolved) throw new Error('Synthetic UI flow ended before semantic speech was confirmed.');
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
      ...getLiveCostControlConfig(),
      responseModalities: [Modality.AUDIO],
      inputAudioTranscription: {},
      outputAudioTranscription: {},
      realtimeInputConfig: getLiveRealtimeInputConfig(false, manualActivityBoundaries),
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
        providerActivitySequence += 1;
        serverMessageCount += 1;
        for (const key of Object.keys(message || {})) serverMessageKinds.add(key);
        if (message?.usageMetadata && typeof message.usageMetadata === 'object') {
          const providerTurn = activeTurnIndex + 1;
          providerUsageSnapshots.push({
            turn: providerTurn,
            afterCompletedTurns: completedTurnCount + (message?.serverContent?.turnComplete ? 1 : 0),
            usageMetadata: { ...message.usageMetadata },
          });
          providerTurnUsage = mergeLiveProviderTurnUsage(providerTurnUsage, [{
            turn: providerTurn,
            usageMetadata: message.usageMetadata,
          }]);
        }
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
               lastModelAudioByteTimes[activeTurnIndex] = runtime.clock.now();
              enqueueModelPlayback(samples);
              runtime.events.emit({
                operationId, journey: 'live', phase: 'audio.output-chunk',
                data: { samples, totalSamples: modelAudioSampleCount },
              });
            }
          }
        }
        if (message?.serverContent?.turnComplete) {
          completedTurnCount += 1;
          turnCompletionTimes[completedTurnCount - 1] = runtime.clock.now();
          turnWaiters.get(completedTurnCount)?.resolve();
          turnWaiters.delete(completedTurnCount);
        }
      },
      onerror: (error: unknown) => {
        failSession(attachLiveFailureEvidence(error));
      },
      onclose: () => {
        failSession(attachLiveFailureEvidence(new Error('Live session closed before all turn completions.')));
      },
    },
    });
    providerConnectedAt = runtime.clock.now();
  } catch (error) {
    await router?.stop();
    throw attachLiveFailureEvidence(error);
  }

  const sendVideoFrames = () => {
    if (videoFramesSentForTurns.has(activeTurnIndex)) return;
    videoFramesSentForTurns.add(activeTurnIndex);
    const frames = input.videoFramesByTurn?.[activeTurnIndex]
      ?? (activeTurnIndex === 0 ? input.videoFrames : undefined)
      ?? [];
    for (const frame of frames) {
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
    } else if (input.manualActivityBoundaries === true) {
      session.sendRealtimeInput({ activityEnd: {} });
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
      pendingSpeech!.reset();
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

    pendingSpeech!.append(packet);
    const decision = gate.evaluate(energy, logicalNow);
    gatedPackets += 1;
    if (decision.send) {
      gate.forceClose();
      return;
    }
    if (decision.reason === 'cooldown') return;
    if (decision.reason !== 'awaiting-confirmation') return;
    if (!pendingSpeech!.beginWhisperCheck(INPUT_SAMPLE_RATE)) return;
    if (!semanticSpeech) {
      gate.rejectSpeech(logicalNow);
      pendingSpeech!.finishWhisperCheck();
      return;
    }
    if (!gate.confirmSpeech(logicalNow) || !boundary.openFromConfirmedSpeech(logicalNow)) return;
    session.sendRealtimeInput({ activityStart: {} });
    const replay = mergeInt16Arrays(pendingSpeech!.takeConfirmedPackets());
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
    let totalInputCaptureElapsedMs = 0;
    for (let turnIndex = 0; turnIndex < sources.length; turnIndex += 1) {
      activeTurnIndex = turnIndex;
      const source = sources[turnIndex];
      const transcriptStart = inputTranscript.length;
      const outputTranscriptStart = outputTranscript.length;
      const modelAudioSamplesStart = modelAudioSampleCount;
      const sentSamplesStart = sentPackets.reduce((total, packet) => total + packet.length, 0);
      const sentVideoFramesStart = sentVideoFrameCount;
      const turnCaptureStartedAt = turnIndex === 0 && inputCaptureStartedAt !== null
        ? inputCaptureStartedAt
        : runtime.clock.now();

      if (!gateEnabled && input.manualActivityBoundaries === true) {
        session.sendRealtimeInput({ activityStart: {} });
      }
      if (turnIndex === 0 && uiCaptureHandoff && sourceRun) {
        gate!.openFromConfirmedTrigger(logicalNow);
        boundary!.openFromConfirmedSpeech(logicalNow);
        session.sendRealtimeInput({ activityStart: {} });
        // After the one-time pre-connect handoff, keep every later microphone
        // packet on the same gated route used by the browser's persistent
        // observer. Routing directly to the packetizer here would make the
        // second headless turn bypass semantic gating entirely.
        const handoff = uiCaptureHandoff.transferTo(pcm => { void routeCapturedPacket(pcm); });
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
        inputCaptureStartedAt ??= turnCaptureStartedAt;
        await router.attach(source);
      }
      const turnCaptureEndedAt = runtime.clock.now();
      totalInputCaptureElapsedMs += Math.max(0, turnCaptureEndedAt - turnCaptureStartedAt);
      await packetizer.flushPending();
      endAudioStream('source-ended');

      const timeoutMs = Math.max(1_000, input.timeoutMs ?? 120_000);
      let timeoutId: ReturnType<typeof setTimeout> | undefined;
      await Promise.race([
        waitForTurn(turnIndex + 1),
        new Promise<never>((_, reject) => {
          timeoutId = globalThis.setTimeout(() => reject(attachLiveFailureEvidence(new Error(
            `Live turn ${turnIndex + 1}/${sources.length} timed out after ${timeoutMs}ms with ${serverMessageCount} server messages`
            + ` (${[...serverMessageKinds].sort().join(', ') || 'no message kinds'}).`,
          ))), timeoutMs);
        }),
      ]).finally(() => {
        if (timeoutId) globalThis.clearTimeout(timeoutId);
      });
      let turnPlaybackDrainWaitMs = await waitForProviderTurnCallbacks();
      modelPlaybackDrainWaitMs += turnPlaybackDrainWaitMs;
      if (playModelAudioRealtime) {
        const drainStartedAt = runtime.clock.now();
        const playbackThroughTurn = modelPlaybackQueue;
        await playbackThroughTurn;
        const additionalDrainWaitMs = Math.max(0, runtime.clock.now() - drainStartedAt);
        turnPlaybackDrainWaitMs += additionalDrainWaitMs;
        modelPlaybackDrainWaitMs += additionalDrainWaitMs;
        gate?.notePlayback(false, logicalNow);
      }

      const turnInputTranscript = inputTranscript.slice(transcriptStart).trim();
      const turnOutputTranscript = outputTranscript.slice(outputTranscriptStart).trim();
      const turnModelAudioSampleCount = modelAudioSampleCount - modelAudioSamplesStart;
      if (!turnOutputTranscript && turnModelAudioSampleCount === 0) {
        throw new Error(
          `Live turn completed without model output (${turnIndex + 1}/${sources.length}) after ${serverMessageCount} server messages`
          + ` (${[...serverMessageKinds].sort().join(', ') || 'no message kinds'}).`,
        );
      }
      const playbackCompletedAt = playModelAudioRealtime ? runtime.clock.now() : null;
      const lastModelAudioByteAt = lastModelAudioByteTimes[turnIndex] ?? null;
      turnResults.push({
        turn: turnIndex + 1,
        inputTranscript: turnInputTranscript,
        outputTranscript: turnOutputTranscript,
        sentSamples: sentPackets.reduce((total, packet) => total + packet.length, 0) - sentSamplesStart,
        modelAudioSampleCount: turnModelAudioSampleCount,
        sentVideoFrameCount: sentVideoFrameCount - sentVideoFramesStart,
        turnCompleteAt: turnCompletionTimes[turnIndex] ?? null,
        lastModelAudioByteAt,
        playbackCompletedAt,
        playbackDrainWaitMs: turnPlaybackDrainWaitMs,
        playbackCompletedAfterLastByte: !playModelAudioRealtime
          || (lastModelAudioByteAt !== null && playbackCompletedAt !== null && playbackCompletedAt >= lastModelAudioByteAt),
      });

      if (turnIndex + 1 < sources.length) {
        boundary?.reset();
        gate?.rejectSpeech(logicalNow);
        pendingSpeech?.reset();
        packetizer.resetPacingEpoch();
      }
    }
    const packetStats = packetizer.getStats();
    const sentAudio = mergeInt16Arrays(sentPackets);
    const inputAudioDurationMs = packetStats.totalInputSamples / INPUT_SAMPLE_RATE * 1_000;
    const inputCaptureElapsedMs = totalInputCaptureElapsedMs;
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
        inputAudioDurationMs,
        inputCaptureElapsedMs,
        modelAudioDurationMs,
        modelPlaybackElapsedMs,
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
      providerUsageSnapshots,
      providerTurnUsage,
      providerUsageMetadata: sumLiveProviderTurnUsage(providerTurnUsage),
      providerLatestUsageMetadata: providerUsageSnapshots.length > 0
        ? providerUsageSnapshots[providerUsageSnapshots.length - 1].usageMetadata
        : null,
      connectedTurnCount: turnResults.length,
      turns: turnResults,
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
