// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createLiveInputCapture } from './inputCapture';
import { createLiveProviderCallbacks } from './providerCallbacks';
import {
  Modality
} from '@google/genai';
import {
  SpeechGate
} from '../../../../shared/audio/speechGate';
import { getLiveCostControlConfig } from '../../../../shared/liveCostControls';
import {
  createLiveOpenReason,
  LIVE_OPEN_TRIGGER
} from '../../../../shared/liveOpenReason';
import { ContinuousLiveTurnBoundary } from '../../../core-sdk/media/continuousLiveTurnBoundary';
import {
  getLiveConversationThinkingConfig,
  getLiveRealtimeInputConfig,
} from '../../../core-sdk/media/liveModelCompatibility';
import {
  OBSERVER_WHISPER_MODEL,
  SemanticSpeechCapture
} from '../../../core-sdk/media/observerSpeechDetection';
import {
  type LocalSpeechTriggerResult
} from '../utils/localSpeechTrigger';
import { createLiveActivity } from './activity';
import { createLiveLifecycle } from './lifecycle';
import { createLiveModelAudio } from './modelAudio';
import type { LiveRuntimePorts } from './ports';
import { createLiveSessionState } from './state';
import { createLiveTelemetry } from './telemetry';
import { createLiveTranscripts } from './transcripts';
import { INPUT_SAMPLE_RATE, OUTPUT_SAMPLE_RATE, type StartLiveConversationOptions, type UseGeminiLiveConversationCallbacks } from './types';

// One process-wide sequence retains the original stale-callback fence.
let liveConversationSessionCounter = 0;
/** Session lifetime and setup. React renders only replace callback ports; all
 * mutable ownership lives in the session and the dedicated lifecycle owners. */
export function createLiveConversationController(ports: LiveRuntimePorts, callbacks: UseGeminiLiveConversationCallbacks = {}) {
  const state = createLiveSessionState(callbacks);
  const {
    sessionRef, inputAudioContextRef, outputAudioContextRef,
    microphoneStreamRef, canvasRef, workletNodeRef,
    playbackNodeRef, logRef, logFinalizedRef,
    modelRef, serverMessageQueueRef, currentSessionIdRef,
    speechTriggerAbortRef, isCleaningUpRef, currentInputTranscriptionRef,
    localSpeechPendingRef, concealedSpeechProgressRef, concealedSpeechSamplesRef,
    turnTimingRef, currentOutputTranscriptionRef, inputAudioTelemetryRef,
    playbackTelemetryRef, playbackDrainCoordinatorRef, speechGateRef,
    speechTurnBoundaryRef, semanticSpeechCaptureRef, observerWhisperRef,
    lastWhisperRequestAtRef, loadingFallbackOnsetAtRef, speechGateEpochRef,
    playbackUntilRef, playbackActiveRef, inputClosedByServerRef,
    callbacksRef,
  } = state;
  const activity = createLiveActivity(state, ports);
  const { updateState, setLocalSpeechTriggerPhase, setVadActivity } = activity;
  const telemetry = createLiveTelemetry(state);
  const { resetAudioTelemetry, getAudioTelemetrySnapshot } = telemetry;
  const transcripts = createLiveTranscripts(state, ports);
  const { emitTurnTranscriptUpdate } = transcripts;
  const modelAudio = createLiveModelAudio(state, ports);
  const { startNextModelAudioTurn, ensureInputCodecWorker } = modelAudio;
  const video = ports.createVideo(state);
  const { ensureVideoElementReady, startVideoFrameLoop } = video;
  const lifecycle = createLiveLifecycle(state, { ...activity, ...telemetry, ...transcripts, ...modelAudio, ...video, flushCaptureWorkletNode: ports.flushCaptureWorkletNode });
  const { cleanup } = lifecycle;
  const {
    getGeminiModels, getAi, debugLogService,
    beginTurnTiming, createLiveUsageTracker, acquireLocalWhisperClient,
    releaseLocalWhisperClient, waitForLocalSpeechTrigger, isNativePlatform,
    getAudioContextConstructor, getUserMedia, createAudioWorkletNode,
    createCanvas, FLOAT_TO_INT16_PROCESSOR_URL, FLOAT_TO_INT16_PROCESSOR_NAME,
    PCM_PLAYBACK_PROCESSOR_URL, PCM_PLAYBACK_PROCESSOR_NAME,
  } = ports;
  const ensureCaptureWorklet = async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    await ctx.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL);
  };
  const ensurePlaybackWorklet = async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    await ctx.audioWorklet.addModule(PCM_PLAYBACK_PROCESSOR_URL);
  };
  const start = async (opts: StartLiveConversationOptions) => {
    const {
      liveOpenTrigger, stream, videoElement,
      systemInstruction, voiceName, responseModalities = [Modality.AUDIO],
      playModelAudio = true, emitTurns = true, allowModelInterruptions = false,
      costFeature = 'liveConversation', gateInputOnSpeech = false, gateAudioAfterConnect = gateInputOnSpeech,
    } = opts;
    const speechGateEnabled = gateInputOnSpeech || gateAudioAfterConnect;
    const observerActivity = liveOpenTrigger === LIVE_OPEN_TRIGGER.WHISPER_OBSERVER;

    // Wait for any in-progress cleanup to finish
    while (isCleaningUpRef.current) {
      await new Promise(resolve => setTimeout(resolve, 10));
    }

    // Ensure previous session is fully cleaned
    await cleanup();

    // Generate a new session ID for this start call
    const sessionId = ++liveConversationSessionCounter;
    concealedSpeechProgressRef.current = 0;
    concealedSpeechSamplesRef.current = 0;
    turnTimingRef.current = beginTurnTiming(crypto.randomUUID());
    currentSessionIdRef.current = sessionId;
    inputClosedByServerRef.current = false;
    startNextModelAudioTurn(sessionId);
    serverMessageQueueRef.current = Promise.resolve();
    resetAudioTelemetry();

    const abortIfInvalidated = async () => {
      if (currentSessionIdRef.current !== sessionId) {
        while (isCleaningUpRef.current) {
          await new Promise(resolve => setTimeout(resolve, 10));
        }
        await cleanup();
        return true;
      }
      return false;
    };

    try {
      if (
        liveOpenTrigger === LIVE_OPEN_TRIGGER.WHISPER_OBSERVER
        && !gateInputOnSpeech
      ) {
        throw new Error('The Whisper observer reason requires a completed local speech gate.');
      }
      let localSpeechTrigger: LocalSpeechTriggerResult | null = null;
      if (speechGateEnabled) {
        observerWhisperRef.current ??= acquireLocalWhisperClient({
          model: OBSERVER_WHISPER_MODEL,
          allowFp32Fallback: !isNativePlatform(),
        });
        // User-opened Live sessions do not pass through the pre-connect helper,
        // so warm the shared detector here as well. Capture remains available
        // and the documented energy fallback takes over after the grace period.
        void observerWhisperRef.current.initialize().catch(() => undefined);
      }
      if (gateInputOnSpeech) {
        updateState('armed');
        const triggerAbort = new AbortController();
        speechTriggerAbortRef.current = triggerAbort;
        localSpeechTrigger = await waitForLocalSpeechTrigger({
          detector: observerWhisperRef.current!,
          signal: triggerAbort.signal,
          onPhaseChange: setLocalSpeechTriggerPhase,
          onCaptureStarted: () => turnTimingRef.current?.markOnce('capture.started'),
          onPendingSpeechSamples: samples => {
            if (currentSessionIdRef.current !== sessionId || Date.now() < playbackUntilRef.current) return;
            turnTimingRef.current?.markLatest('speech.last-capture-energy-detected');
            if (!localSpeechPendingRef.current) return;
            concealedSpeechSamplesRef.current += samples;
            const progress = Math.min(24, 3 + Math.floor(concealedSpeechSamplesRef.current / INPUT_SAMPLE_RATE));
            if (progress !== concealedSpeechProgressRef.current) {
              concealedSpeechProgressRef.current = progress;
              emitTurnTranscriptUpdate('input');
            }
          },
          onVadActivityChange: active => setVadActivity(active, observerActivity),
        });
        if (speechTriggerAbortRef.current === triggerAbort) speechTriggerAbortRef.current = null;
        // Transfer ownership immediately. The same graph keeps capturing while
        // video, playback and the paid transport are initialized, so words
        // spoken after Whisper recognizes the prefix cannot fall into a gap.
        microphoneStreamRef.current = localSpeechTrigger.microphoneStream;
        inputAudioContextRef.current = localSpeechTrigger.capture.audioContext;
        workletNodeRef.current = localSpeechTrigger.capture.workletNode;
        inputAudioTelemetryRef.current.speechTriggerSamples = localSpeechTrigger.pcm.length;
        if (await abortIfInvalidated()) return;
        localSpeechPendingRef.current = true;
        turnTimingRef.current?.mark('speech.whisper-trigger', {
          capturedAudioMs: localSpeechTrigger.pcm.length / INPUT_SAMPLE_RATE * 1000,
        });
        emitTurnTranscriptUpdate('pending-user');
      }

      // Cleanup deliberately clears all per-session gate state, so initialize
      // it only after the local pre-connect trigger has completed.
      speechGateRef.current = speechGateEnabled
        ? new SpeechGate({ requireConfirmation: true })
        : null;
      speechTurnBoundaryRef.current = speechGateEnabled
        ? new ContinuousLiveTurnBoundary()
        : null;
      semanticSpeechCaptureRef.current = speechGateEnabled
        ? new SemanticSpeechCapture({ sampleRate: INPUT_SAMPLE_RATE })
        : null;
      playbackUntilRef.current = 0;
      playbackActiveRef.current = false;
      lastWhisperRequestAtRef.current = 0;
      loadingFallbackOnsetAtRef.current = null;
      const speechGateEpoch = speechGateEpochRef.current;
      updateState('connecting');

      if (stream && stream.active) {
        // Video setup is optional in observer mode (audio-only fallback).
        await ensureVideoElementReady(stream, videoElement);
        if (await abortIfInvalidated()) return;
        if (!canvasRef.current) {
          canvasRef.current = createCanvas();
        }
      }

      // Audio Setup
      const AudioContextCtor: typeof AudioContext = getAudioContextConstructor();
      const micStream = localSpeechTrigger?.microphoneStream
        || await getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
      microphoneStreamRef.current = micStream;
      if (await abortIfInvalidated()) return;

      let inputSource: MediaStreamAudioSourceNode | null = null;
      let workletNode = localSpeechTrigger?.capture.workletNode || null;
      if (!localSpeechTrigger) {
        const inputCtx = new AudioContextCtor({ sampleRate: INPUT_SAMPLE_RATE });
        inputAudioContextRef.current = inputCtx;
        inputSource = inputCtx.createMediaStreamSource(micStream);
        await ensureCaptureWorklet(inputCtx);
        if (await abortIfInvalidated()) return;
        workletNode = createAudioWorkletNode(inputCtx, FLOAT_TO_INT16_PROCESSOR_NAME, {
          numberOfInputs: 1,
          numberOfOutputs: 0,
        });
        workletNodeRef.current = workletNode;
      }

      if (playModelAudio) {
        let outputCtx: AudioContext;
        try {
          outputCtx = new AudioContextCtor({ sampleRate: OUTPUT_SAMPLE_RATE });
        } catch (error) {
          console.warn('Failed to create 24kHz playback AudioContext, retrying with default device rate', error);
          playbackTelemetryRef.current.contextFallbackToDefaultRate = true;
          try {
            outputCtx = new AudioContextCtor();
          } catch (fallbackError) {
            console.error('Failed to create playback AudioContext', fallbackError);
            throw new Error('Failed to initialize model audio playback');
          }
        }

        if (await abortIfInvalidated()) {
          try { await outputCtx.close(); } catch { }
          return;
        }

        playbackTelemetryRef.current.outputSampleRate = outputCtx.sampleRate;
        if (outputCtx.sampleRate !== OUTPUT_SAMPLE_RATE) {
          playbackTelemetryRef.current.resampledOutput = true;
          console.warn(
            `Playback AudioContext sample rate mismatch: expected ${OUTPUT_SAMPLE_RATE}, got ${outputCtx.sampleRate}. Falling back to worklet resampling.`
          );
        }

        outputAudioContextRef.current = outputCtx;
        await ensurePlaybackWorklet(outputCtx);
        if (await abortIfInvalidated()) return;
        const playbackNode = createAudioWorkletNode(outputCtx, PCM_PLAYBACK_PROCESSOR_NAME, {
          numberOfInputs: 0,
          numberOfOutputs: 1,
          outputChannelCount: [1],
        });
        playbackNode.port.onmessage = (event: MessageEvent<{
          type?: string;
          event?: 'started' | 'resumed' | 'underrun';
        }>) => {
          const telemetryMessage = event.data;
          if (playbackDrainCoordinatorRef.current.handleMessage(telemetryMessage)) return;
          if (!telemetryMessage || telemetryMessage.type !== 'telemetry') return;
          if (telemetryMessage.event === 'started') {
            turnTimingRef.current?.markOnce('playback.first-render-notified');
            playbackTelemetryRef.current.starts += 1;
            return;
          }
          if (telemetryMessage.event === 'resumed') {
            playbackTelemetryRef.current.resumes += 1;
            return;
          }
          if (telemetryMessage.event === 'underrun') {
            playbackTelemetryRef.current.underruns += 1;
          }
        };
        playbackNode.connect(outputCtx.destination);
        playbackNodeRef.current = playbackNode;
      }

      const model = getGeminiModels().audio.conversation;
      const usageTracker = createLiveUsageTracker({ feature: costFeature, configuredModel: model });
      const realtimeInputConfig = getLiveRealtimeInputConfig(
        allowModelInterruptions,
        speechGateEnabled,
      );
      modelRef.current = model;
      logFinalizedRef.current = false;
      logRef.current = debugLogService.logRequest('useGeminiLiveConversation', model, {
        responseModalities,
        systemInstruction: systemInstruction || '',
        inputAudioTranscription: {},
        outputAudioTranscription: {},
        realtimeInputConfig,
      });

      const ai = await getAi();
      turnTimingRef.current?.mark('context.build-start');
      const freshSystemInstruction = opts.buildSystemInstruction
        ? await opts.buildSystemInstruction()
        : systemInstruction;
      turnTimingRef.current?.mark('context.ready', { instructionCharacters: freshSystemInstruction?.length ?? 0 });
      if (await abortIfInvalidated()) return;
      const providerCallbacks = createLiveProviderCallbacks(state, {
        activity, audio: modelAudio, transcripts, cleanup, getAudioTelemetrySnapshot, debugLogService,
      }, { sessionId, playModelAudio, emitTurns, observerActivity, usageTracker });
      turnTimingRef.current?.mark('provider.connect-start');
      const session = await ai.live.connect({
        turnTiming: turnTimingRef.current ?? undefined,
        model,
        liveOpenReason: createLiveOpenReason(liveOpenTrigger),
        config: {
          ...getLiveCostControlConfig(),
          responseModalities,
          systemInstruction: freshSystemInstruction,
          // Empty config objects to enable transcription without specifying parameters causing invalid argument errors
          inputAudioTranscription: {},
          outputAudioTranscription: {},
          realtimeInputConfig,
          thinkingConfig: getLiveConversationThinkingConfig(model),
          // Voice configuration for the live conversation
          speechConfig: voiceName ? { voiceConfig: { prebuiltVoiceConfig: { voiceName } } } : undefined,
        },
        callbacks: providerCallbacks
      });

      sessionRef.current = session;
      turnTimingRef.current?.mark('provider.connected');

      // Check if session was invalidated during async connect
      if (currentSessionIdRef.current !== sessionId) {
        if (sessionRef.current === session) sessionRef.current = null;
        try { session.close(); } catch { }
        return;
      }

      const input = createLiveInputCapture(state, { ensureInputCodecWorker, setVadActivity, setLocalSpeechTriggerPhase, emitTurnTranscriptUpdate }, {
        sessionId, speechGateEpoch, speechGateEnabled, observerActivity, localSpeechTrigger, workletNode, inputSource,
      });
      if (speechGateRef.current && localSpeechTrigger) {
        input.replayConfirmedCapture(localSpeechTrigger, speechGateRef.current);
        if (await abortIfInvalidated()) return;
        input.transferCapture(localSpeechTrigger);
      } else {
        input.attachCaptureNode();
      }

      if (stream && stream.active) {
        startVideoFrameLoop(sessionId);
      }

    } catch (e) {
      if (e instanceof Error && e.name === 'AbortError') {
        updateState('idle');
        await cleanup();
        return;
      }
      updateState('error');
      if (logRef.current && !logFinalizedRef.current) {
        logFinalizedRef.current = true;
        logRef.current.error({
          message: e instanceof Error ? e.message : String(e),
          inputTranscript: currentInputTranscriptionRef.current,
          outputTranscript: currentOutputTranscriptionRef.current,
          audioTelemetry: getAudioTelemetrySnapshot(),
        });
      }
      callbacksRef.current.onError?.(e instanceof Error ? e.message : String(e));
      await cleanup();
    }
  };
  return {
    start, stop: lifecycle.stop, updateVideoInput: video.updateVideoInput,
    setCallbacks(next: UseGeminiLiveConversationCallbacks) { state.callbacksRef.current = next; },
    dispose() {
      releaseLocalWhisperClient(state.observerWhisperRef.current);
      state.observerWhisperRef.current = null;
      void cleanup().catch(error => { console.warn('Live cleanup on unmount failed:', error); });
    },
  };
}
