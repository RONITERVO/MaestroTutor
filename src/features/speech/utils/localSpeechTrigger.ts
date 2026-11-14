// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { mergeInt16Arrays } from '../../../core-sdk/media/audioProcessing';
import {
  isLikelySpeechTranscript,
  LOCAL_WHISPER_REQUEST_INTERVAL_MS,
  SemanticSpeechCapture,
} from '../../../core-sdk/media/liveSpeechDetection';
import { RealtimePcmPacketizer } from '../../../core-sdk/media/realtimePcmPacketizer';
import {
  PcmCaptureHandoff,
  type PcmCaptureHandoffStats,
} from '../../../core-sdk/media/pcmInput';
import {
  DEFAULT_SPEECH_GATE,
  isSpeechLike,
  SpeechGate,
  measureEnergy,
} from '../../../../shared/audio/speechGate';
import type { OpenPcmCapture, PcmCapture, PcmCaptureOwner } from '../../../core-sdk/media/pcmCapture';
import { openBrowserPcmCapture } from './browserPcmCapture';
import type { LocalWhisperClient } from './localWhisperClient';

const INPUT_SAMPLE_RATE = 16_000;

export type LocalSpeechTriggerPhase =
  | 'requesting-microphone'
  | 'whisper-loading'
  | 'vad-listening'
  | 'whisper-checking'
  | 'speech-confirmed';

export interface LocalSpeechTriggerResult {
  pcm: Int16Array;
  transcript: string;
  /**
   * The exact capture owner that detected the utterance. It stays
   * alive while the paid Live transport connects, then transfers every sample
   * captured in that interval to the normal session pipeline without changing
   * microphones or processing backends.
   */
  capture: LocalSpeechCaptureHandoff;
}

export type LocalSpeechCaptureHandoffStats = PcmCaptureHandoffStats;

export interface LocalSpeechCaptureHandoff extends PcmCaptureOwner {
  /** Route buffered continuation audio first, then all future capture. */
  transferTo(sink: (pcm: Int16Array) => void): LocalSpeechCaptureHandoffStats;
}

const abortError = () => {
  const error = new Error('Local speech trigger was stopped.');
  error.name = 'AbortError';
  return error;
};

/**
 * Wait for local VAD plus Whisper to find real words before a paid Live socket
 * may be opened. The accepted microphone stream and bounded pre-roll transfer
 * to the caller so the triggering utterance is not clipped.
 */
export const waitForLocalSpeechTrigger = async (options: {
  detector: LocalWhisperClient;
  openCapture?: OpenPcmCapture;
  signal?: AbortSignal;
  onPhaseChange?: (phase: LocalSpeechTriggerPhase) => void;
  onVadActivityChange?: (active: boolean) => void;
  onPendingSpeechSamples?: (samples: number) => void;
  onCaptureStarted?: () => void;
}): Promise<LocalSpeechTriggerResult> => {
  if (options.signal?.aborted) throw abortError();
  const setPhase = (phase: LocalSpeechTriggerPhase) => options.onPhaseChange?.(phase);
  let capture: PcmCapture | null = null;
  let captureClosed = false;
  let packetizer: RealtimePcmPacketizer | null = null;
  let settled = false;
  let vadActive = false;
  let inferenceTailPackets: Int16Array[] = [];
  const continuationCapture = new PcmCaptureHandoff();

  const setVadActive = (active: boolean) => {
    if (vadActive === active) return;
    vadActive = active;
    options.onVadActivityChange?.(active);
  };

  const stopCapture = async () => {
    captureClosed = true;
    setVadActive(false);
    packetizer?.dispose();
    packetizer = null;
    inferenceTailPackets = [];
    continuationCapture.close();
    await capture?.close();
  };

  let rejectAbort: ((error: Error) => void) | null = null;
  const abortPromise = new Promise<never>((_, reject) => {
    rejectAbort = reject;
  });
  const onAbort = () => {
    if (settled) return;
    settled = true;
    void stopCapture().finally(() => rejectAbort?.(abortError()));
  };
  const raceAbort = <T>(promise: Promise<T>): Promise<T> => (
    options.signal ? Promise.race([promise, abortPromise]) : promise
  );
  options.signal?.addEventListener('abort', onAbort, { once: true });

  try {
    setPhase('requesting-microphone');
    const acquisition = (options.openCapture ?? openBrowserPcmCapture)({ signal: options.signal });
    // A platform adapter may finish after cancellation. Claim or close its
    // exact owner before exposing it to the trigger; never resurrect a trigger.
    const accepted = acquisition.then(async owner => {
      if (captureClosed) { await owner.close(); throw abortError(); }
      capture = owner;
    });
    await raceAbort(accepted);
    setPhase('whisper-loading');
    await raceAbort(options.detector.initialize());

    const gate = new SpeechGate({ requireConfirmation: true });
    const pendingSpeech = new SemanticSpeechCapture({ sampleRate: INPUT_SAMPLE_RATE });
    let whisperBusy = false;
    let lastWhisperRequestAt = 0;

    const result = new Promise<LocalSpeechTriggerResult>((resolve, reject) => {
      const rejectOnce = (error: Error) => {
        if (settled) return;
        settled = true;
        reject(error);
      };

      packetizer = new RealtimePcmPacketizer({
        sampleRate: INPUT_SAMPLE_RATE,
        packetDurationMs: 100,
        maxWaitMs: 120,
        onPacket: async packet => {
          if (settled) return;
          const now = Date.now();
          const energy = measureEnergy(packet);
          const packetIsSpeech = isSpeechLike(energy, DEFAULT_SPEECH_GATE);
          setVadActive(packetIsSpeech);
          pendingSpeech.append(packet);
          const decision = gate.evaluate(energy, now);
          if (decision.send) return;
          if (decision.reason === 'playback' || decision.reason === 'cooldown') return;
          if (decision.reason !== 'awaiting-confirmation') return;
          if (whisperBusy || now - lastWhisperRequestAt < LOCAL_WHISPER_REQUEST_INTERVAL_MS) return;

          // The minimum is elapsed audio in this intact window, not 1.2 seconds
          // of packets individually selected by VAD. This is the same boundary
          // Ariadne uses and lets short sentences reach semantic detection.
          const audio = pendingSpeech.beginWhisperCheck(INPUT_SAMPLE_RATE);
          if (!audio) return;
          whisperBusy = true;
          inferenceTailPackets = [];
          lastWhisperRequestAt = now;
          setPhase('whisper-checking');
          try {
            const transcript = (await options.detector.transcribe(audio)).trim();
            if (settled) return;
            const resultAt = Date.now();
            if (!isLikelySpeechTranscript(transcript)) {
              gate.rejectSpeech(resultAt);
              pendingSpeech.finishWhisperCheck();
              inferenceTailPackets = [];
              setPhase('vad-listening');
              return;
            }
            if (!gate.confirmSpeech(resultAt)) {
              pendingSpeech.finishWhisperCheck();
              inferenceTailPackets = [];
              setPhase('vad-listening');
              return;
            }
            settled = true;
            setPhase('speech-confirmed');
            setVadActive(false);
            packetizer?.dispose();
            packetizer = null;
            const pcm = mergeInt16Arrays([
              ...pendingSpeech.takeConfirmedPackets(),
              ...inferenceTailPackets,
            ]);
            inferenceTailPackets = [];
            resolve({
              pcm,
              transcript,
              capture: {
                transferTo: sink => continuationCapture.transferTo(sink),
                flush: () => capture!.flush(),
                close: stopCapture,
              },
            });
          } catch (error) {
            rejectOnce(error instanceof Error ? error : new Error(String(error)));
          } finally {
            whisperBusy = false;
          }
        },
      });

      capture!.start(pcm => {
        if (captureClosed) return;
        if (settled) {
          if (isSpeechLike(measureEnergy(pcm), DEFAULT_SPEECH_GATE)) {
            options.onPendingSpeechSamples?.(pcm.length);
          }
          continuationCapture.push(pcm);
          return;
        }
        // Whisper inference is asynchronous. Preserve everything spoken while
        // it is deciding so the recognized prefix cannot consume the suffix.
        if (whisperBusy) inferenceTailPackets.push(pcm.slice());
        packetizer?.push(pcm);
      });
      options.onCaptureStarted?.();
      setPhase('vad-listening');
    });

    return await raceAbort(result);
  } catch (error) {
    settled = true;
    await stopCapture();
    throw error;
  } finally {
    options.signal?.removeEventListener('abort', onAbort);
  }
};
