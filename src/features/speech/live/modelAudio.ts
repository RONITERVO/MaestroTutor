// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { extendPlaybackEnd } from '../../../../shared/audio/speechGate';
import { OUTPUT_SAMPLE_RATE, type ModelAudioDecodeJob } from './types';
import { type AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import type { SpeechOutput } from '../../../core-sdk/media/speechOutput';
import type { LiveSessionData } from './state';
import { type ModelAudioDecodeCheckpoint } from './types';

export function createLiveModelAudio(state: Pick<LiveSessionData,
  'speechOutputRef' | 'inputCodecWorkerRef'
  | 'outputCodecWorkerRef' | 'currentSessionIdRef' | 'isCleaningUpRef' | 'turnTimingRef'
  | 'currentModelAudioTurnIdRef' | 'nextModelAudioTurnIdRef' | 'nextModelAudioDecodeJobIdRef'
  | 'pendingModelAudioDecodeJobsRef' | 'playbackTelemetryRef'
  | 'playbackPendingRef' | 'playbackUntilRef' | 'currentModelAudioChunksRef'
  | 'currentModelAudioTotalLengthRef'
>, ports: { createCodecWorker(): AudioCodecWorkerClient; onFailure(message: string, sessionId: number): void }) {
  const {
    speechOutputRef, inputCodecWorkerRef,
    outputCodecWorkerRef, currentSessionIdRef, isCleaningUpRef, turnTimingRef,
    currentModelAudioTurnIdRef, nextModelAudioTurnIdRef, nextModelAudioDecodeJobIdRef,
    pendingModelAudioDecodeJobsRef, playbackTelemetryRef,
    playbackPendingRef, playbackUntilRef, currentModelAudioChunksRef,
    currentModelAudioTotalLengthRef,
  } = state;
  const { createCodecWorker } = ports;
  let commitQueue = Promise.resolve();
  let pendingDrain: {
    checkpoint: ModelAudioDecodeCheckpoint;
    output: SpeechOutput;
    promise: Promise<void>;
  } | null = null;
  const sameCheckpoint = (a: ModelAudioDecodeCheckpoint, b: ModelAudioDecodeCheckpoint) => (
    a.sessionId === b.sessionId && a.turnId === b.turnId && a.lastJobId === b.lastJobId
  );
  const startNextModelAudioTurn = (sessionId: number) => {
    commitQueue = Promise.resolve();
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
    pendingDrain = null;
    playbackPendingRef.current = false;
    playbackUntilRef.current = 0;
    try { speechOutputRef.current?.reset(); } catch { /* Teardown remains authoritative. */ }
  };

  const waitForPlaybackDrain = (): Promise<void> => {
    const output = speechOutputRef.current;
    const decodeCheckpoint = getModelAudioDecodeCheckpoint();
    if (pendingDrain && pendingDrain.output === output
      && sameCheckpoint(pendingDrain.checkpoint, decodeCheckpoint)) {
      return pendingDrain.promise;
    }
    if (!output || !playbackPendingRef.current) return Promise.resolve();

    const drain = { checkpoint: decodeCheckpoint, output, promise: Promise.resolve() };
    pendingDrain = drain;
    const timing = turnTimingRef.current;
    const isCurrent = () => speechOutputRef.current === output
      && currentSessionIdRef.current === decodeCheckpoint.sessionId
      && currentModelAudioTurnIdRef.current === decodeCheckpoint.turnId;
    drain.promise = (async () => {
      const startedAt = Date.now();
      let result: 'drained' | 'cancelled';
      try { result = await output.drain(); }
      catch {
        if (isCurrent()) {
          playbackTelemetryRef.current.queueErrors += 1;
          ports.onFailure('Speech playback stopped before completion.', decodeCheckpoint.sessionId);
        }
        return;
      }
      if (!isCurrent()) return;
      playbackTelemetryRef.current.lastDrainWaitMs = Date.now() - startedAt;
      if (result !== 'drained') {
        playbackTelemetryRef.current.drainCancellations += 1;
        if (pendingDrain === drain && playbackPendingRef.current)
          ports.onFailure('Speech playback stopped before completion.', decodeCheckpoint.sessionId);
        return;
      }
      playbackTelemetryRef.current.drains += 1;
      // A late PCM chunk still owns its own drain, even after this tail finishes.
      if (sameCheckpoint(getModelAudioDecodeCheckpoint(), decodeCheckpoint)) {
        playbackPendingRef.current = false;
        timing?.markLatest('playback.drained');
      }
    })().finally(() => { if (pendingDrain === drain) pendingDrain = null; });
    return drain.promise;
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
    if (currentSessionIdRef.current !== sessionId || isCleaningUpRef.current) return;
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
      && !isCleaningUpRef.current
      && currentSessionIdRef.current === sessionId
      && currentModelAudioTurnIdRef.current === turnId
    );

    // Decode may finish out of order. Commit samples to the renderer and saved
    // transcript in arrival order, with a fresh chain after a cancelled turn.
    let decoding: Promise<ArrayBuffer>;
    try { decoding = ensureOutputCodecWorker().decodeBase64ToPcmBuffer(inlineAudio); }
    catch (error) { decoding = Promise.reject(error); }
    const decoded = decoding
      .then(buffer => ({ buffer, error: undefined }), error => ({ buffer: undefined, error }));
    job.promise = commitQueue.then(async () => {
        const result = await decoded;
        if (!isJobActive()) return;
        if (!result.buffer) throw result.error;

        const pcm16 = new Int16Array(result.buffer);
        if (!pcm16.length) return;

        currentModelAudioChunksRef.current.push(pcm16);
        currentModelAudioTotalLengthRef.current += pcm16.length;

        if (playModelAudio) {
          try {
            if (!speechOutputRef.current) throw new Error('Speech output is unavailable.');
            speechOutputRef.current.write(pcm16);
            playbackPendingRef.current = true;
            playbackUntilRef.current = extendPlaybackEnd(
              playbackUntilRef.current,
              Date.now(),
              pcm16.length,
              OUTPUT_SAMPLE_RATE,
            );
          } catch (error) {
            playbackTelemetryRef.current.queueErrors += 1;
            console.warn('Speech output queue failed', error);
            ports.onFailure('Speech playback stopped before completion.', sessionId);
          }
        }
      })
      .catch((error) => {
        if (!isJobActive()) return;
        playbackTelemetryRef.current.decodeErrors += 1;
        console.warn('Audio decode failed', error);
        ports.onFailure('Speech audio could not be decoded.', sessionId);
      })
      .finally(() => {
        pendingModelAudioDecodeJobsRef.current.delete(jobId);
      });
    commitQueue = job.promise;

    pendingModelAudioDecodeJobsRef.current.set(jobId, job);
  };
  return { enqueueModelAudio, startNextModelAudioTurn, getModelAudioDecodeCheckpoint, cancelModelAudioDecodeJobs, waitForModelAudioDecodeCheckpoint, stopAllAudio, waitForPlaybackDrain, ensureInputCodecWorker, ensureOutputCodecWorker };
}
