// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  DEFAULT_SPEECH_GATE,
  isSpeechLike,
  measureEnergy,
  type SpeechGate
} from '../../../../shared/audio/speechGate';
import { mergeInt16Arrays } from '../../../core-sdk/media/audioProcessing';
import { type ContinuousLiveTurnBoundary } from '../../../core-sdk/media/continuousLiveTurnBoundary';
import { evaluateFreshSpeechFallback } from '../../../core-sdk/media/liveSpeechDetection';
import {
  isLikelySpeechTranscript,
  OBSERVER_WHISPER_REQUEST_INTERVAL_MS
} from '../../../core-sdk/media/observerSpeechDetection';
import { PcmCaptureRouter } from '../../../core-sdk/media/pcmInput';
import {
  RealtimePcmPacketizer
} from '../../../core-sdk/media/realtimePcmPacketizer';
import { type CaptureWorkletMessage } from '../utils/captureWorkletMessaging';
import {
  type LocalSpeechTriggerResult
} from '../utils/localSpeechTrigger';
import { type createLiveActivity } from './activity';
import { type createLiveModelAudio } from './modelAudio';
import type { LiveSessionData } from './state';
import { type createLiveTranscripts } from './transcripts';
import { INPUT_SAMPLE_RATE, LIVE_INPUT_PACKET_DURATION_MS, LIVE_INPUT_PACKET_MAX_WAIT_MS, OBSERVER_WHISPER_LOAD_GRACE_MS, SPEECH_GATE_REPLAY_CHUNK_SAMPLES } from './types';

export interface LiveInputCapturePorts {
  ensureInputCodecWorker: ReturnType<typeof createLiveModelAudio>['ensureInputCodecWorker'];
  setVadActivity: ReturnType<typeof createLiveActivity>['setVadActivity'];
  setLocalSpeechTriggerPhase: ReturnType<typeof createLiveActivity>['setLocalSpeechTriggerPhase'];
  emitTurnTranscriptUpdate: ReturnType<typeof createLiveTranscripts>['emitTurnTranscriptUpdate'];
}

const toTransferableArrayBuffer = (pcm: Int16Array): ArrayBuffer => {
  if (
    pcm.buffer instanceof ArrayBuffer
    && pcm.byteOffset === 0
    && pcm.byteLength === pcm.buffer.byteLength
  ) {
    return pcm.buffer;
  }
  return pcm.slice().buffer;
};

/** Packet pacing, semantic gate and continuous turn boundaries. Capture handoff
 * remains explicit so the session controller can fence it after asynchronous work. */
export function createLiveInputCapture(state: Pick<LiveSessionData,
  'sessionRef' | 'inputPacketizerRef' | 'pcmCaptureRouterRef'
  | 'currentSessionIdRef' | 'currentInputTranscriptionRef' | 'localSpeechPendingRef'
  | 'concealedSpeechProgressRef' | 'concealedSpeechSamplesRef' | 'turnTimingRef'
  | 'currentUserAudioChunksRef' | 'currentUserAudioTotalLengthRef' | 'inputAudioTelemetryRef'
  | 'speechGateRef' | 'speechTurnBoundaryRef' | 'semanticSpeechCaptureRef'
  | 'observerWhisperRef' | 'observerWhisperBusyRef' | 'lastWhisperRequestAtRef'
  | 'loadingFallbackOnsetAtRef' | 'whisperFailureWarnedRef' | 'speechGateEpochRef'
  | 'playbackUntilRef' | 'playbackActiveRef' | 'awaitingModelTurnRef'
  | 'inputClosedByServerRef' | 'boundaryClosePromiseRef'
>, ports: LiveInputCapturePorts, session: {
  sessionId: number; speechGateEpoch: number; speechGateEnabled: boolean; observerActivity: boolean;
  localSpeechTrigger: LocalSpeechTriggerResult | null; workletNode: AudioWorkletNode | null;
  inputSource: MediaStreamAudioSourceNode | null;
}) {
  const {
    sessionRef, inputPacketizerRef, pcmCaptureRouterRef,
    currentSessionIdRef, currentInputTranscriptionRef, localSpeechPendingRef,
    concealedSpeechProgressRef, concealedSpeechSamplesRef, turnTimingRef,
    currentUserAudioChunksRef, currentUserAudioTotalLengthRef, inputAudioTelemetryRef,
    speechGateRef, speechTurnBoundaryRef, semanticSpeechCaptureRef,
    observerWhisperRef, observerWhisperBusyRef, lastWhisperRequestAtRef,
    loadingFallbackOnsetAtRef, whisperFailureWarnedRef, speechGateEpochRef,
    playbackUntilRef, playbackActiveRef, awaitingModelTurnRef,
    inputClosedByServerRef, boundaryClosePromiseRef,
  } = state;
  const { ensureInputCodecWorker, setVadActivity, setLocalSpeechTriggerPhase, emitTurnTranscriptUpdate } = ports;
  const { sessionId, speechGateEpoch, speechGateEnabled, observerActivity, localSpeechTrigger, workletNode, inputSource } = session;
  const encodeAndSend = async (pcm: Int16Array) => {
    if (inputClosedByServerRef.current) return;
    // Encoding transfers the packet buffer to a worker. Preserve a copy
    // only for gated audio that was truly sent.
    const retained = speechGateEnabled ? pcm.slice() : null;
    const base64 = await ensureInputCodecWorker().encodePcmToBase64(
      toTransferableArrayBuffer(pcm),
    );
    if (
      currentSessionIdRef.current !== sessionId
      || speechGateEpochRef.current !== speechGateEpoch
    ) return;
    const activeSession = sessionRef.current;
    if (!activeSession || inputClosedByServerRef.current) return;
    activeSession.sendRealtimeInput({
      audio: { data: base64, mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}` },
    });
    turnTimingRef.current?.markOnce('input.first-audio-sent');
    turnTimingRef.current?.markLatest('input.last-audio-sent');
    if (retained) {
      currentUserAudioChunksRef.current.push(retained);
      currentUserAudioTotalLengthRef.current += retained.length;
    }
  };

  inputPacketizerRef.current = new RealtimePcmPacketizer({
    sampleRate: INPUT_SAMPLE_RATE,
    packetDurationMs: LIVE_INPUT_PACKET_DURATION_MS,
    maxWaitMs: LIVE_INPUT_PACKET_MAX_WAIT_MS,
    paceOutput: true,
    onPacket: async (packet) => {
      try {
        if (
          currentSessionIdRef.current !== sessionId
          || speechGateEpochRef.current !== speechGateEpoch
        ) return;
        await encodeAndSend(packet);
      } catch (error) {
        if (currentSessionIdRef.current !== sessionId) return;
        inputAudioTelemetryRef.current.encodeErrors += 1;
        console.warn('Audio encode failed', error);
      }
    },
  });

  const openContinuousTurn = (now: number): boolean => {
    const boundary = speechTurnBoundaryRef.current;
    const activeSession = sessionRef.current;
    if (
      !boundary
      || !activeSession
      || awaitingModelTurnRef.current
      || now < playbackUntilRef.current
      || !boundary.openFromConfirmedSpeech(now)
    ) return false;

    localSpeechPendingRef.current = !currentInputTranscriptionRef.current;
    concealedSpeechProgressRef.current = Math.max(3, concealedSpeechProgressRef.current);
    emitTurnTranscriptUpdate('pending-user');
    activeSession.sendRealtimeInput({ activityStart: {} });
    inputAudioTelemetryRef.current.activityStarts += 1;
    return true;
  };

  const replayConfirmedPrefix = () => {
    const held = semanticSpeechCaptureRef.current?.takeConfirmedPackets() || [];
    const replay = mergeInt16Arrays(held);
    for (let offset = 0; offset < replay.length; offset += SPEECH_GATE_REPLAY_CHUNK_SAMPLES) {
      inputPacketizerRef.current?.push(
        replay.slice(offset, offset + SPEECH_GATE_REPLAY_CHUNK_SAMPLES),
      );
    }
  };

  const closeContinuousTurn = (now: number) => {
    const boundary = speechTurnBoundaryRef.current;
    if (!boundary?.beginClosing(now) || boundaryClosePromiseRef.current) return;

    awaitingModelTurnRef.current = true;
    setVadActivity(false, observerActivity);
    const closingBoundary = boundary;
    const closingPacketizer = inputPacketizerRef.current;
    const closePromise = (async () => {
      turnTimingRef.current?.mark('speech.close-start', { queuedAudioMs: closingPacketizer?.getQueuedAudioMs() ?? 0 });
      await closingPacketizer?.flushPending();
      turnTimingRef.current?.mark('input.queue-drained');
      if (
        currentSessionIdRef.current !== sessionId
        || speechGateEpochRef.current !== speechGateEpoch
        || speechTurnBoundaryRef.current !== closingBoundary
      ) return;

      const activeSession = sessionRef.current;
      if (!activeSession) return;
      activeSession.sendRealtimeInput({ activityEnd: {} });
      turnTimingRef.current?.mark('input.activity-end-sent');
      closingPacketizer?.resetPacingEpoch();
      closingBoundary.finishClosing();
      speechGateRef.current?.rejectSpeech(Date.now());
      semanticSpeechCaptureRef.current?.reset();
      loadingFallbackOnsetAtRef.current = null;
    })().finally(() => {
      if (boundaryClosePromiseRef.current === closePromise) {
        boundaryClosePromiseRef.current = null;
      }
    });
    boundaryClosePromiseRef.current = closePromise;
  };

  const requestWhisperCheck = (
    now: number,
    gate: SpeechGate,
    boundary: ContinuousLiveTurnBoundary,
    fallback: ReturnType<typeof evaluateFreshSpeechFallback> | null,
  ) => {
    const detector = observerWhisperRef.current;
    const capture = semanticSpeechCaptureRef.current;
    if (
      !detector
      || !capture
      || detector.status !== 'ready'
      || observerWhisperBusyRef.current
      || now - lastWhisperRequestAtRef.current < OBSERVER_WHISPER_REQUEST_INTERVAL_MS
    ) return;

    const audio = capture.beginWhisperCheck(INPUT_SAMPLE_RATE);
    if (!audio) return;

    const checkKind = boundary.isOpen ? 'boundary' : 'wake';
    observerWhisperBusyRef.current = true;
    lastWhisperRequestAtRef.current = now;
    inputAudioTelemetryRef.current.whisperChecks += 1;
    if (checkKind === 'boundary') {
      inputAudioTelemetryRef.current.whisperBoundaryChecks += 1;
    } else {
      inputAudioTelemetryRef.current.whisperWakeChecks += 1;
    }

    void detector.transcribe(audio).then((text) => {
      if (
        currentSessionIdRef.current !== sessionId
        || speechGateEpochRef.current !== speechGateEpoch
        || speechGateRef.current !== gate
        || speechTurnBoundaryRef.current !== boundary
        || semanticSpeechCaptureRef.current !== capture
      ) return;

      const resultAt = Date.now();
      if (isLikelySpeechTranscript(text)) {
        inputAudioTelemetryRef.current.whisperAccepted += 1;
        if (checkKind === 'boundary') {
          turnTimingRef.current?.mark('speech.whisper-confirmed');
          boundary.refreshConfirmedSpeech(resultAt);
          return;
        }
        if (
          !awaitingModelTurnRef.current
          && gate.confirmSpeech(resultAt)
          && openContinuousTurn(resultAt)
        ) {
          loadingFallbackOnsetAtRef.current = null;
          replayConfirmedPrefix();
        }
        return;
      }

      inputAudioTelemetryRef.current.whisperRejected += 1;
      // A negative refresh does not cut an already-open stream. The last
      // positive result still owns its full idle plus post-roll boundary.
      if (checkKind === 'boundary') return;
      gate.rejectSpeech(resultAt);
      loadingFallbackOnsetAtRef.current = null;
    }).catch((error) => {
      if (
        currentSessionIdRef.current !== sessionId
        || speechGateEpochRef.current !== speechGateEpoch
        || speechGateRef.current !== gate
        || speechTurnBoundaryRef.current !== boundary
        || semanticSpeechCaptureRef.current !== capture
      ) return;
      inputAudioTelemetryRef.current.whisperErrors += 1;
      if (checkKind === 'wake' && fallback?.action === 'confirm') {
        const fallbackAt = Date.now();
        inputAudioTelemetryRef.current.energyFallbacks += 1;
        if (gate.confirmSpeech(fallbackAt) && openContinuousTurn(fallbackAt)) {
          loadingFallbackOnsetAtRef.current = null;
          replayConfirmedPrefix();
        }
      }
      if (!whisperFailureWarnedRef.current) {
        whisperFailureWarnedRef.current = true;
        console.warn('Local Whisper check failed; using the energy-only fallback.', error);
      }
    }).finally(() => {
      if (semanticSpeechCaptureRef.current === capture) capture.finishWhisperCheck();
      observerWhisperBusyRef.current = false;
    });
  };

  const handleCapturedPcm = (pcm: Int16Array) => {
    if (currentSessionIdRef.current !== sessionId || !pcm.length) return;
    if (awaitingModelTurnRef.current) return;
    inputAudioTelemetryRef.current.capturedSamples += pcm.length;
    const gate = speechGateRef.current;
    const boundary = speechTurnBoundaryRef.current;
    const speechCapture = semanticSpeechCaptureRef.current;
    if (!gate || !boundary) {
      currentUserAudioChunksRef.current.push(pcm);
      currentUserAudioTotalLengthRef.current += pcm.length;
      inputPacketizerRef.current?.push(pcm);
      return;
    }
    if (!speechCapture) return;

    const now = Date.now();
    // Evaluate echo suppression on the capture clock. Old paced packets
    // must never be classified using the speaker state of a later moment.
    const speaking = now < playbackUntilRef.current;
    if (speaking !== playbackActiveRef.current) {
      playbackActiveRef.current = speaking;
      gate.notePlayback(speaking, now);
    }
    if (speaking || awaitingModelTurnRef.current || boundary.isClosing) {
      inputAudioTelemetryRef.current.gatedPackets += 1;
      speechCapture.reset();
      loadingFallbackOnsetAtRef.current = null;
      setVadActivity(false, observerActivity);
      return;
    }

    if (boundary.shouldBeginClosing(now)) {
      inputAudioTelemetryRef.current.gatedPackets += 1;
      closeContinuousTurn(now);
      return;
    }

    const energy = measureEnergy(pcm);
    const packetIsSpeech = isSpeechLike(energy, DEFAULT_SPEECH_GATE);
    if (packetIsSpeech) turnTimingRef.current?.markLatest('speech.last-energy-detected');
    if (!localSpeechTrigger && packetIsSpeech && localSpeechPendingRef.current && !currentInputTranscriptionRef.current) {
      concealedSpeechSamplesRef.current += pcm.length;
      const progress = Math.min(24, 3 + Math.floor(concealedSpeechSamplesRef.current / INPUT_SAMPLE_RATE));
      if (progress !== concealedSpeechProgressRef.current) {
        concealedSpeechProgressRef.current = progress;
        emitTurnTranscriptUpdate('input');
      }
    }
    setVadActivity(packetIsSpeech, observerActivity);
    speechCapture.append(pcm);

    if (boundary.isOpen) {
      // The boundary surrounds a continuous stream. Silence, stutters and
      // quiet syllables inside it are data and must reach Gemini unchanged.
      inputPacketizerRef.current?.push(pcm);
      const detector = observerWhisperRef.current;
      const detectorUnavailable = !detector
        || detector.status === 'failed'
        || detector.status === 'disposed'
        || (
          (detector.status === 'idle' || detector.status === 'loading')
          && detector.loadingStartedAt > 0
          && now - detector.loadingStartedAt >= OBSERVER_WHISPER_LOAD_GRACE_MS
        );
      if (detectorUnavailable) {
        // Once speech was semantically confirmed, energy may safely keep
        // its boundary alive if Whisper later becomes unavailable.
        if (packetIsSpeech) boundary.refreshConfirmedSpeech(now);
      } else {
        requestWhisperCheck(now, gate, boundary, null);
      }
      return;
    }

    inputAudioTelemetryRef.current.gatedPackets += 1;
    const decision = gate.evaluate(energy, now);
    if (decision.send) {
      // The onset gate must never independently outlive the continuous
      // turn it opened. Recover closed rather than leaking unbounded audio.
      gate.forceClose();
      return;
    }
    if (decision.reason === 'playback') {
      speechCapture.reset();
      loadingFallbackOnsetAtRef.current = null;
      return;
    }
    if (decision.reason === 'cooldown') {
      if (!packetIsSpeech) loadingFallbackOnsetAtRef.current = null;
      return;
    }
    if (decision.reason !== 'awaiting-confirmation') return;

    const previousFallbackOnset = loadingFallbackOnsetAtRef.current;
    const fallback = evaluateFreshSpeechFallback(
      energy,
      previousFallbackOnset,
      now,
    );
    loadingFallbackOnsetAtRef.current = fallback.onsetAt;
    const detector = observerWhisperRef.current;
    const detectorUnavailable = !detector
      || detector.status === 'failed'
      || detector.status === 'disposed'
      || (
        (detector.status === 'idle' || detector.status === 'loading')
        && detector.loadingStartedAt > 0
        && now - detector.loadingStartedAt >= OBSERVER_WHISPER_LOAD_GRACE_MS
      );
    if (detectorUnavailable) {
      if (fallback.action === 'expire') {
        gate.rejectSpeech(now);
        loadingFallbackOnsetAtRef.current = null;
      } else if (fallback.action === 'confirm') {
        inputAudioTelemetryRef.current.energyFallbacks += 1;
        if (gate.confirmSpeech(now) && openContinuousTurn(now)) {
          loadingFallbackOnsetAtRef.current = null;
          replayConfirmedPrefix();
        }
      }
      return;
    }
    requestWhisperCheck(now, gate, boundary, fallback);
  };
  pcmCaptureRouterRef.current = new PcmCaptureRouter({
    sink: ({ pcm }) => {
      handleCapturedPcm(pcm);
    },
  });

  const replayConfirmedCapture = (trigger: LocalSpeechTriggerResult, gate: SpeechGate) => {
    const confirmedAt = Date.now();
    if (
      gate.openFromConfirmedTrigger(confirmedAt)
      && openContinuousTurn(confirmedAt)
    ) {
      for (let offset = 0; offset < trigger.pcm.length; offset += SPEECH_GATE_REPLAY_CHUNK_SAMPLES) {
        handleCapturedPcm(
          trigger.pcm.slice(offset, offset + SPEECH_GATE_REPLAY_CHUNK_SAMPLES),
        );
      }
    }

  };
  const transferCapture = (trigger: LocalSpeechTriggerResult) => {
    const handoff = trigger.capture.transferTo((pcm) => {
      void pcmCaptureRouterRef.current?.push(pcm, INPUT_SAMPLE_RATE, 'device');
    });
    inputAudioTelemetryRef.current.connectionHandoffPackets = handoff.bufferedPackets;
    inputAudioTelemetryRef.current.connectionHandoffSamples = handoff.bufferedSamples;
    setLocalSpeechTriggerPhase(null);
  };
  const attachCaptureNode = () => {
    if (!workletNode || !inputSource) return;
    // User-started sessions do not have a pre-connect capture to transfer.
    workletNode.port.onmessage = (event: MessageEvent<CaptureWorkletMessage>) => {
      // CRITICAL: Check session is still valid before processing audio
      if (currentSessionIdRef.current !== sessionId) return;

      const pcm = event.data;
      if (!(pcm instanceof Int16Array) || !pcm.length) return;

      void pcmCaptureRouterRef.current?.push(pcm, INPUT_SAMPLE_RATE, 'device');
    };
    inputSource.connect(workletNode);
  };
  return { replayConfirmedCapture, transferCapture, attachCaptureNode };
}
