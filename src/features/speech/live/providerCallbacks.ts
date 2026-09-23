// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  type LiveServerMessage
} from '@google/genai';
import { mergeInt16Arrays } from '../../../core-sdk/media/audioProcessing';
import { LiveTurnFinalizer } from '../utils/liveTurnFinalizer';
import { countTranscriptNewlines } from '../utils/transcriptParsing';
import { type createLiveActivity } from './activity';
import { type createLiveLifecycle } from './lifecycle';
import { type createLiveModelAudio } from './modelAudio';
import type { LiveRuntimePorts } from './ports';
import type { LiveSessionData } from './state';
import { type createLiveTelemetry } from './telemetry';
import { type createLiveTranscripts } from './transcripts';
import { type LiveTurnTranscriptUpdateReason } from './types';

export interface LiveProviderPorts {
  activity: Pick<ReturnType<typeof createLiveActivity>, 'setVadActivity' | 'updateState'>;
  audio: ReturnType<typeof createLiveModelAudio>;
  transcripts: Pick<ReturnType<typeof createLiveTranscripts>, 'getTranscriptLinkedUserAudio' | 'emitTurnTranscriptUpdate'>;
  cleanup: ReturnType<typeof createLiveLifecycle>['cleanup'];
  getAudioTelemetrySnapshot: ReturnType<typeof createLiveTelemetry>['getAudioTelemetrySnapshot'];
  debugLogService: LiveRuntimePorts['debugLogService'];
}

/** Ordered provider callbacks, turn finalization and transport terminal handling.
 * A close/error waits for decode and playback; stale sessions retain only their
 * original accounting flush behavior. */
export function createLiveProviderCallbacks(state: Pick<LiveSessionData,
  'sessionRef' | 'logRef' | 'logFinalizedRef'
  | 'modelRef' | 'pendingUserTurnRef' | 'serverMessageQueueRef'
  | 'inputPacketizerRef' | 'currentSessionIdRef' | 'currentInputTranscriptionRef'
  | 'localSpeechPendingRef' | 'turnTimingRef' | 'currentOutputTranscriptionRef'
  | 'currentUserAudioChunksRef' | 'currentModelAudioChunksRef' | 'currentUserAudioTotalLengthRef'
  | 'currentUserTranscriptAudioLengthRef' | 'currentModelThinkingTraceRef' | 'currentModelThinkingPhaseRef'
  | 'currentModelThinkingStatusLineRef' | 'currentTurnWaitingForInputRef' | 'currentModelAudioTotalLengthRef'
  | 'modelAudioSplitPointsRef' | 'lastNewlineCountRef' | 'lastTranscriptUpdateRef'
  | 'currentModelAudioTurnIdRef' | 'speechTurnBoundaryRef' | 'semanticSpeechCaptureRef'
  | 'awaitingModelTurnRef' | 'inputClosedByServerRef' | 'callbacksRef'
>, ports: LiveProviderPorts, session: {
  sessionId: number; playModelAudio: boolean; emitTurns: boolean; observerActivity: boolean;
  usageTracker: ReturnType<LiveRuntimePorts['createLiveUsageTracker']>;
}) {
  const {
    sessionRef, logRef, logFinalizedRef,
    modelRef, pendingUserTurnRef, serverMessageQueueRef,
    inputPacketizerRef, currentSessionIdRef, currentInputTranscriptionRef,
    localSpeechPendingRef, turnTimingRef, currentOutputTranscriptionRef,
    currentUserAudioChunksRef, currentModelAudioChunksRef, currentUserAudioTotalLengthRef,
    currentUserTranscriptAudioLengthRef, currentModelThinkingTraceRef, currentModelThinkingPhaseRef,
    currentModelThinkingStatusLineRef, currentTurnWaitingForInputRef, currentModelAudioTotalLengthRef,
    modelAudioSplitPointsRef, lastNewlineCountRef, lastTranscriptUpdateRef,
    currentModelAudioTurnIdRef, speechTurnBoundaryRef, semanticSpeechCaptureRef,
    awaitingModelTurnRef, inputClosedByServerRef, callbacksRef,
  } = state; const { setVadActivity, updateState } = ports.activity;
  const {
    enqueueModelAudio, cancelModelAudioDecodeJobs, getModelAudioDecodeCheckpoint,
    waitForModelAudioDecodeCheckpoint, waitForPlaybackDrain, startNextModelAudioTurn,
    stopAllAudio,
  } = ports.audio;
  const { getTranscriptLinkedUserAudio, emitTurnTranscriptUpdate } = ports.transcripts;
  const { cleanup, getAudioTelemetrySnapshot, debugLogService } = ports;
  const { sessionId, playModelAudio, emitTurns, observerActivity, usageTracker } = session;
  let transportTerminalHandled = false;
  const finishTransportLifecycle = async (
    terminalState: 'idle' | 'error',
    errorMessage?: string,
  ) => {
    if (transportTerminalHandled || currentSessionIdRef.current !== sessionId) return;
    transportTerminalHandled = true;
    const terminalSession = sessionRef.current;
    sessionRef.current = null;
    try { terminalSession?.close?.(); } catch { }

    // onclose/onerror can overtake worker decoding. First seal the ordered
    // server-message queue, then let the audio thread render every PCM chunk.
    await serverMessageQueueRef.current.catch(() => undefined);
    const checkpoint = getModelAudioDecodeCheckpoint();
    await waitForModelAudioDecodeCheckpoint(checkpoint);
    if (currentSessionIdRef.current !== sessionId) return;
    await waitForPlaybackDrain();
    if (currentSessionIdRef.current !== sessionId) return;
    // One response owns one usage snapshot, including late accounting
    // callbacks. Flush only after the ordered queue and playback settle.
    usageTracker.flush();

    if (logRef.current && !logFinalizedRef.current) {
      logFinalizedRef.current = true;
      const details = {
        inputTranscript: currentInputTranscriptionRef.current,
        outputTranscript: currentOutputTranscriptionRef.current,
        audioTelemetry: getAudioTelemetrySnapshot(),
      };
      if (terminalState === 'error') {
        logRef.current.error({ message: errorMessage || 'Connection error', ...details });
      } else {
        logRef.current.complete({ status: 'closed-after-playback-drain', ...details });
      }
    }
    if (terminalState === 'error') {
      callbacksRef.current.onError?.(errorMessage || 'Connection error');
    }
    await cleanup();
    updateState(terminalState);
  };
  const turnFinalizer = new LiveTurnFinalizer(async () => {
    await serverMessageQueueRef.current.catch(() => undefined);
    await waitForModelAudioDecodeCheckpoint(getModelAudioDecodeCheckpoint());
    await waitForPlaybackDrain();
  });
  const finalizeProviderTurn = async () => {
    if (currentSessionIdRef.current !== sessionId) return;
    const modelAudioCheckpoint = getModelAudioDecodeCheckpoint();
    await waitForModelAudioDecodeCheckpoint(modelAudioCheckpoint);
    if (
      currentSessionIdRef.current !== sessionId
      || currentModelAudioTurnIdRef.current !== modelAudioCheckpoint.turnId
    ) {
      return;
    }

    const completedTurnId = modelAudioCheckpoint.turnId;
    const userText = currentInputTranscriptionRef.current.trim();
    const modelText = currentOutputTranscriptionRef.current.trim();
    const userAudioFull = getTranscriptLinkedUserAudio();
    const modelAudioFull = mergeInt16Arrays(currentModelAudioChunksRef.current);
    const modelAudioLines: Int16Array[] = [];

    if (modelAudioSplitPointsRef.current.length > 0 && modelAudioFull.length > 0) {
      let startSample = 0;
      const points = modelAudioSplitPointsRef.current
        .slice()
        .sort((a, b) => a - b)
        .filter((point) => point < modelAudioFull.length);

      for (const point of points) {
        if (point > startSample) {
          modelAudioLines.push(modelAudioFull.slice(startSample, point));
          startSample = point;
        } else {
          modelAudioLines.push(new Int16Array(0));
        }
      }

      if (startSample < modelAudioFull.length) {
        modelAudioLines.push(modelAudioFull.slice(startSample));
      }
    } else if (modelAudioFull.length > 0) {
      modelAudioLines.push(modelAudioFull);
    }

    const inputTranscript = currentInputTranscriptionRef.current;
    const outputTranscript = currentOutputTranscriptionRef.current;
    const completionReason: LiveTurnTranscriptUpdateReason = currentTurnWaitingForInputRef.current
      ? 'waiting-for-input'
      : 'no-model-response';

    if (!modelText) {
      if (userText || userAudioFull.length > 0 || inputTranscript) {
        if (pendingUserTurnRef.current) {
          const prev = pendingUserTurnRef.current;
          const mergedText = [prev.text, userText].filter(Boolean).join('\n').trim();
          const mergedTranscript = [prev.transcript, inputTranscript].filter(Boolean).join(' ').trim();
          const mergedAudio = mergeInt16Arrays([prev.audio, userAudioFull]);
          pendingUserTurnRef.current = { text: mergedText, transcript: mergedTranscript, audio: mergedAudio };
        } else {
          pendingUserTurnRef.current = {
            text: userText,
            transcript: inputTranscript,
            audio: userAudioFull,
          };
        }
      }
      if (logRef.current && !logFinalizedRef.current) {
        logRef.current.complete({
          status: completionReason,
          userText,
          inputTranscript,
          modelAudioLinesCount: modelAudioLines.length,
          userAudioSamples: userAudioFull.length,
          audioTelemetry: getAudioTelemetrySnapshot(),
        });
      }
    } else {
      let finalUserText = userText;
      let finalUserAudio = userAudioFull;
      let finalInputTranscript = inputTranscript;
      const pending = pendingUserTurnRef.current;
      if (pending) {
        finalUserText = pending.text || userText;
        finalInputTranscript = pending.transcript || inputTranscript;
        if (pending.audio.length > 0 && userAudioFull.length > 0) {
          finalUserAudio = mergeInt16Arrays([pending.audio, userAudioFull]);
        } else if (pending.audio.length > 0) {
          finalUserAudio = pending.audio;
        }
        pendingUserTurnRef.current = null;
      }

      if (emitTurns) {
        try {
          const callbackResult = callbacksRef.current.onTurnComplete?.(
            finalUserText,
            modelText,
            finalUserAudio,
            modelAudioLines
          );
          if (callbackResult instanceof Promise) {
            await callbackResult.catch((error) => {
              console.error('Live turn completion callback rejected:', error);
            });
          }
        } catch (error) {
          console.error('Live turn completion callback failed:', error);
        }
      }
      if (logRef.current && !logFinalizedRef.current) {
        logRef.current.complete({
          status: 'turn-complete',
          userText: finalUserText,
          modelText,
          inputTranscript: finalInputTranscript,
          outputTranscript,
          modelAudioLinesCount: modelAudioLines.length,
          userAudioSamples: finalUserAudio.length,
          audioTelemetry: getAudioTelemetrySnapshot(),
        });
      }
      const turnLog = debugLogService.logRequest('useGeminiLiveConversation.turn', modelRef.current || 'gemini-live', {
        inputTranscript: finalInputTranscript,
        outputTranscript,
      });
      turnLog.complete({
        status: 'turn-complete',
        userText: finalUserText,
        modelText,
        inputTranscript: finalInputTranscript,
        outputTranscript,
        modelAudioLinesCount: modelAudioLines.length,
        userAudioSamples: finalUserAudio.length,
        audioTelemetry: getAudioTelemetrySnapshot(),
      });
    }

    cancelModelAudioDecodeJobs(sessionId, completedTurnId);
    currentInputTranscriptionRef.current = '';
    localSpeechPendingRef.current = false;
    currentOutputTranscriptionRef.current = '';
    currentUserAudioChunksRef.current = [];
    currentUserAudioTotalLengthRef.current = 0;
    currentUserTranscriptAudioLengthRef.current = 0;
    currentModelAudioChunksRef.current = [];
    currentModelAudioTotalLengthRef.current = 0;
    modelAudioSplitPointsRef.current = [];
    lastNewlineCountRef.current = 0;
    lastTranscriptUpdateRef.current = null;
    startNextModelAudioTurn(sessionId);
    awaitingModelTurnRef.current = false;
    currentModelThinkingTraceRef.current = [];
    currentModelThinkingPhaseRef.current = undefined;
    currentModelThinkingStatusLineRef.current = undefined;
    currentTurnWaitingForInputRef.current = false;
    if (!modelText) {
      emitTurnTranscriptUpdate(completionReason);
    }
  };
  let turnFinalizationStarted = false;
  const enqueueTurnFinalization = () => {
    if (currentSessionIdRef.current !== sessionId || turnFinalizationStarted) return;
    turnFinalizationStarted = true;
    serverMessageQueueRef.current = serverMessageQueueRef.current
      .catch(() => undefined)
      .then(finalizeProviderTurn)
      .catch((error) => {
        if (currentSessionIdRef.current !== sessionId) return;
        console.warn('Live turn finalization failed', error);
      });
    // Never await lifecycle teardown inside the message queue: it drains
    // that queue itself. One connection owns exactly one response.
    void serverMessageQueueRef.current.then(() => finishTransportLifecycle('idle'));
  };

  return {
    oninputturnended: () => {
      if (currentSessionIdRef.current !== sessionId) return;
      inputClosedByServerRef.current = true;
      awaitingModelTurnRef.current = true;
      speechTurnBoundaryRef.current?.reset();
      inputPacketizerRef.current?.dispose();
      inputPacketizerRef.current = null;
      semanticSpeechCaptureRef.current?.reset();
      setVadActivity(false, observerActivity);
      currentTurnWaitingForInputRef.current = false;
      turnTimingRef.current?.mark('input.server-ended-turn');
    },
    onopen: () => {
      // Check session is still valid before updating state
      if (currentSessionIdRef.current !== sessionId) return;
      updateState('active');
    },
    onmessage: (msg: LiveServerMessage) => {
      turnFinalizer.touch();
      serverMessageQueueRef.current = serverMessageQueueRef.current
        .catch(() => undefined)
        .then(async () => {
          // Check session is still valid before processing message
          if (currentSessionIdRef.current !== sessionId) return;

          if (msg.usageMetadata) {
            usageTracker.trackSnapshot(msg.usageMetadata);
          }
          if (msg.goAway) {
            callbacksRef.current.onGoAway?.(msg.goAway);
          }
          if (msg.sessionResumptionUpdate) {
            callbacksRef.current.onSessionResumptionUpdate?.(msg.sessionResumptionUpdate);
          }

          // 1. Handle Audio Output
          const modelTurnParts = msg.serverContent?.modelTurn?.parts ?? [];
          const thoughtTexts = modelTurnParts
            .map((part) => (
              part?.thought && typeof part.text === 'string'
                ? part.text.trim()
                : ''
            ))
            .filter((text): text is string => text.length > 0);

          if (thoughtTexts.length > 0) {
            currentTurnWaitingForInputRef.current = false;
            let thoughtTraceChanged = false;
            for (const thoughtText of thoughtTexts) {
              const previousThought = currentModelThinkingTraceRef.current[
                currentModelThinkingTraceRef.current.length - 1
              ];
              if (previousThought === thoughtText) {
                continue;
              }
              currentModelThinkingTraceRef.current = [
                ...currentModelThinkingTraceRef.current,
                thoughtText,
              ].slice(-8);
              thoughtTraceChanged = true;
            }
            currentModelThinkingPhaseRef.current = 'Thinking';
            currentModelThinkingStatusLineRef.current = undefined;
            if (thoughtTraceChanged) {
              emitTurnTranscriptUpdate('output');
            }
          }

          const inlineAudioParts = msg.serverContent?.modelTurn?.parts
            ?.map((part) => part.inlineData?.data)
            .filter((data): data is string => typeof data === 'string' && data.length > 0)
            ?? [];

          for (const inlineAudio of inlineAudioParts) {
            enqueueModelAudio(inlineAudio, sessionId, playModelAudio);
          }

          // 2. Handle Transcript Accumulation & Split Point Detection
          if (msg.serverContent?.inputTranscription?.text) {
            turnTimingRef.current?.markOnce('input.first-provider-transcript');
            localSpeechPendingRef.current = false;
            currentInputTranscriptionRef.current += msg.serverContent.inputTranscription.text;
            currentUserTranscriptAudioLengthRef.current = currentUserAudioTotalLengthRef.current;
            emitTurnTranscriptUpdate('input');
          }
          if (msg.serverContent?.waitingForInput === true) {
            currentTurnWaitingForInputRef.current = true;
          }
          if (msg.serverContent?.outputTranscription?.text) {
            turnTimingRef.current?.markOnce('response.first-provider-transcript');
            const textPart = msg.serverContent.outputTranscription.text;
            currentOutputTranscriptionRef.current += textPart;
            currentTurnWaitingForInputRef.current = false;
            currentModelThinkingPhaseRef.current = 'Final response';
            currentModelThinkingStatusLineRef.current = undefined;

            const currentText = currentOutputTranscriptionRef.current;
            const newlineCount = countTranscriptNewlines(currentText);

            if (newlineCount > lastNewlineCountRef.current) {
              const decodeCheckpoint = getModelAudioDecodeCheckpoint();
              await waitForModelAudioDecodeCheckpoint(decodeCheckpoint);
              if (
                currentSessionIdRef.current !== sessionId
                || currentModelAudioTurnIdRef.current !== decodeCheckpoint.turnId
              ) {
                return;
              }

              const committedNewlineCount = lastNewlineCountRef.current;
              if (newlineCount > committedNewlineCount) {
                const diff = newlineCount - committedNewlineCount;
                for (let i = 0; i < diff; i++) {
                  modelAudioSplitPointsRef.current.push(currentModelAudioTotalLengthRef.current);
                }
                lastNewlineCountRef.current = newlineCount;
              }
            }
            emitTurnTranscriptUpdate('output');
          }

          // 3. Finalize only after already-dispatched provider callbacks settle.
          if (msg.serverContent?.turnComplete) {
            turnFinalizer.schedule(enqueueTurnFinalization);
          }
          // 4. Handle Interruption
          if (msg.serverContent?.interrupted) {
            turnFinalizer.cancel();
            const interruptedTurnId = currentModelAudioTurnIdRef.current;
            cancelModelAudioDecodeJobs(sessionId, interruptedTurnId);
            if (playModelAudio) {
              stopAllAudio();
            }
            currentOutputTranscriptionRef.current = '';
            currentModelThinkingTraceRef.current = [];
            currentModelThinkingPhaseRef.current = undefined;
            currentModelThinkingStatusLineRef.current = undefined;
            currentModelAudioChunksRef.current = [];
            currentModelAudioTotalLengthRef.current = 0;
            modelAudioSplitPointsRef.current = [];
            lastNewlineCountRef.current = 0;
            currentTurnWaitingForInputRef.current = false;
            startNextModelAudioTurn(sessionId);
            awaitingModelTurnRef.current = false;
            emitTurnTranscriptUpdate('interrupted');
          }
        })
        .catch((error) => {
          if (currentSessionIdRef.current !== sessionId) return;
          console.warn('Live server message processing failed', error);
        });
    },
    onclose: () => {
      if (currentSessionIdRef.current !== sessionId) {
        usageTracker.flush();
        return;
      }
      turnFinalizer.flush();
      void finishTransportLifecycle('idle').catch((error) => {
        console.warn('Live playback drain after close failed:', error);
      });
    },
    onerror: (err: any) => {
      if (currentSessionIdRef.current !== sessionId) {
        usageTracker.flush();
        return;
      }
      turnFinalizer.flush();
      // Check session is still valid before updating state
      if (currentSessionIdRef.current !== sessionId) return;
      let message = "Connection error";
      try {
        if (err instanceof Error) message = err.message;
        else if (typeof err === 'string') message = err;
        else if (err && typeof err === 'object') {
          if (err.type === 'error' && !err.message) message = "Connection Failed: Network or API Error";
          else if (err.message) message = String(err.message);
          else message = JSON.stringify(err);
        }
      } catch {
        message = "Unknown Connection Error";
      }
      void finishTransportLifecycle('error', message).catch((error) => {
        console.warn('Live playback drain after error failed:', error);
      });
    }
  };
}
