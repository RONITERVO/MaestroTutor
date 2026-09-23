// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { extendPlaybackEnd } from '../../../../shared/audio/speechGate';
import { OUTPUT_SAMPLE_RATE, type ModelAudioDecodeJob } from './types';
import { type AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import {
  getAudioOutputTailDelayMs
} from '../utils/playbackDrain';
import type { LiveTimerPorts } from './ports';
import type { LiveSessionData } from './state';
import { type ModelAudioDecodeCheckpoint } from './types';

export function createLiveModelAudio(state: Pick<LiveSessionData,
  'outputAudioContextRef' | 'playbackNodeRef' | 'inputCodecWorkerRef'
  | 'outputCodecWorkerRef' | 'currentSessionIdRef' | 'turnTimingRef'
  | 'currentModelAudioTurnIdRef' | 'nextModelAudioTurnIdRef' | 'nextModelAudioDecodeJobIdRef'
  | 'pendingModelAudioDecodeJobsRef' | 'playbackTelemetryRef' | 'playbackDrainCoordinatorRef'
  | 'playbackPendingRef' | 'playbackUntilRef' | 'currentModelAudioChunksRef'
  | 'currentModelAudioTotalLengthRef'
>, ports: Pick<LiveTimerPorts, 'setTimeout'> & { createCodecWorker(): AudioCodecWorkerClient }) {
  const {
    outputAudioContextRef, playbackNodeRef, inputCodecWorkerRef,
    outputCodecWorkerRef, currentSessionIdRef, turnTimingRef,
    currentModelAudioTurnIdRef, nextModelAudioTurnIdRef, nextModelAudioDecodeJobIdRef,
    pendingModelAudioDecodeJobsRef, playbackTelemetryRef, playbackDrainCoordinatorRef,
    playbackPendingRef, playbackUntilRef, currentModelAudioChunksRef,
    currentModelAudioTotalLengthRef,
  } = state;
  const { setTimeout, createCodecWorker } = ports;
  const startNextModelAudioTurn = (sessionId: number) => {
    currentModelAudioTurnIdRef.current = sessionId > 0 ? nextModelAudioTurnIdRef.current++ : 0;
  };

  const getModelAudioDecodeCheckpoint = (): ModelAudioDecodeCheckpoint => ({
    sessionId: currentSessionIdRef.current,
    turnId: currentModelAudioTurnIdRef.current,
    lastJobId: nextModelAudioDecodeJobIdRef.current - 1,
  });

  const cancelModelAudioDecodeJobs = (sessionId?: number, turnId?: number) => {
    for (const [jobId, job] of pendingModelAudioDecodeJobsRef.current.entries()) {
      if (
        (sessionId === undefined || job.sessionId === sessionId)
        && (turnId === undefined || job.turnId === turnId)
      ) {
        job.cancelled = true;
        pendingModelAudioDecodeJobsRef.current.delete(jobId);
      }
    }
  };

  const waitForModelAudioDecodeCheckpoint = async (
    checkpoint: ModelAudioDecodeCheckpoint
  ) => {
    if (checkpoint.turnId === 0 || checkpoint.lastJobId <= 0) {
      return;
    }

    const pendingJobs = Array.from(pendingModelAudioDecodeJobsRef.current.values())
      .filter((job) => (
        job.sessionId === checkpoint.sessionId
        && job.turnId === checkpoint.turnId
        && job.jobId <= checkpoint.lastJobId
      ))
      .map((job) => job.promise);

    if (pendingJobs.length > 0) {
      await Promise.allSettled(pendingJobs);
    }
  };

  const stopAllAudio = () => {
    playbackDrainCoordinatorRef.current.cancelAll();
    playbackPendingRef.current = false;
    if (playbackNodeRef.current) {
      try {
        playbackNodeRef.current.port.postMessage({ type: 'reset' });
        // Queued audio was just discarded, so it will not play out.
        playbackUntilRef.current = 0;
      } catch {
        // Ignore reset failures during teardown/interruption.
      }
    }
  };

  const waitForPlaybackDrain = async () => {
    const playbackNode = playbackNodeRef.current;
    const outputContext = outputAudioContextRef.current;
    if (!playbackNode || !outputContext || !playbackPendingRef.current) return;

    const startedAt = Date.now();
    const decodeCheckpoint = getModelAudioDecodeCheckpoint();
    const result = await playbackDrainCoordinatorRef.current.request(playbackNode.port);
    playbackTelemetryRef.current.lastDrainWaitMs = Date.now() - startedAt;
    if (result !== 'drained') {
      playbackTelemetryRef.current.drainCancellations += 1;
      return;
    }

    // An acknowledgement can race with a late PCM chunk. It must not clear
    // that newer chunk's pending flag or the next settlement could skip it.
    const latestCheckpoint = getModelAudioDecodeCheckpoint();
    if (
      latestCheckpoint.sessionId === decodeCheckpoint.sessionId
      && latestCheckpoint.turnId === decodeCheckpoint.turnId
      && latestCheckpoint.lastJobId === decodeCheckpoint.lastJobId
    ) playbackPendingRef.current = false;
    playbackTelemetryRef.current.drains += 1;
    if (outputAudioContextRef.current !== outputContext || outputContext.state === 'closed') return;
    await new Promise(resolve => setTimeout(resolve, getAudioOutputTailDelayMs(outputContext)));
    turnTimingRef.current?.markLatest('playback.drained');
  };

  const ensureInputCodecWorker = () => {
    if (!inputCodecWorkerRef.current) {
      inputCodecWorkerRef.current = createCodecWorker();
    }
    return inputCodecWorkerRef.current;
  };

  const ensureOutputCodecWorker = () => {
    if (!outputCodecWorkerRef.current) {
      outputCodecWorkerRef.current = createCodecWorker();
    }
    return outputCodecWorkerRef.current;
  };
  /** Decode jobs are fenced by both session and turn; cancellation never allows
   * late worker results to reach the playback queue or retained transcript audio. */
  const enqueueModelAudio = (inlineAudio: string, sessionId: number, playModelAudio: boolean) => {
    turnTimingRef.current?.markLatest('response.last-audio-received');
    turnTimingRef.current?.markOnce('response.first-audio-received');
    const turnId = currentModelAudioTurnIdRef.current;
    const jobId = nextModelAudioDecodeJobIdRef.current++;
    const job: ModelAudioDecodeJob = {
      jobId,
      sessionId,
      turnId,
      cancelled: false,
      promise: Promise.resolve(),
    };

    const isJobActive = () => (
      !job.cancelled
      && currentSessionIdRef.current === sessionId
      && currentModelAudioTurnIdRef.current === turnId
    );

    job.promise = ensureOutputCodecWorker().decodeBase64ToPcmBuffer(inlineAudio)
      .then((buffer) => {
        if (!isJobActive()) return;

        const pcm16 = new Int16Array(buffer);
        if (!pcm16.length) return;

        currentModelAudioChunksRef.current.push(pcm16);
        currentModelAudioTotalLengthRef.current += pcm16.length;

        if (playModelAudio && playbackNodeRef.current) {
          try {
            playbackNodeRef.current.port.postMessage({
              type: 'push',
              pcm: pcm16,
              inputSampleRate: OUTPUT_SAMPLE_RATE,
            });
            playbackPendingRef.current = true;
            playbackUntilRef.current = extendPlaybackEnd(
              playbackUntilRef.current,
              Date.now(),
              pcm16.length,
              OUTPUT_SAMPLE_RATE,
            );
          } catch (error) {
            playbackTelemetryRef.current.queueErrors += 1;
            console.warn('Playback worklet queue failed', error);
          }
        }
      })
      .catch((error) => {
        if (!isJobActive()) return;
        playbackTelemetryRef.current.decodeErrors += 1;
        console.warn('Audio decode failed', error);
      })
      .finally(() => {
        pendingModelAudioDecodeJobsRef.current.delete(jobId);
      });

    pendingModelAudioDecodeJobsRef.current.set(jobId, job);
  };
  return { enqueueModelAudio, startNextModelAudioTurn, getModelAudioDecodeCheckpoint, cancelModelAudioDecodeJobs, waitForModelAudioDecodeCheckpoint, stopAllAudio, waitForPlaybackDrain, ensureInputCodecWorker, ensureOutputCodecWorker };
}
