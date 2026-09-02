// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { mergeInt16Arrays } from '../../../core-sdk/media/audioProcessing';
import {
  isLikelySpeechTranscript,
  LOCAL_SPEECH_BUFFER_MS,
  LOCAL_SPEECH_PREROLL_MS,
  LOCAL_WHISPER_REQUEST_INTERVAL_MS,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
  SpeechActivityTracker,
} from '../../../core-sdk/media/liveSpeechDetection';
import { RealtimePcmPacketizer } from '../../../core-sdk/media/realtimePcmPacketizer';
import {
  DEFAULT_SPEECH_GATE,
  isSpeechLike,
  SpeechGate,
  measureEnergy,
} from '../../../../shared/audio/speechGate';
import { FLOAT_TO_INT16_PROCESSOR_NAME, FLOAT_TO_INT16_PROCESSOR_URL } from '../worklets';
import type { CaptureWorkletMessage } from './captureWorkletMessaging';
import type { LocalWhisperClient } from './localWhisperClient';

const INPUT_SAMPLE_RATE = 16_000;
const BUFFER_SAMPLES = Math.round(INPUT_SAMPLE_RATE * LOCAL_SPEECH_BUFFER_MS / 1_000);
const PREROLL_SAMPLES = Math.round(INPUT_SAMPLE_RATE * LOCAL_SPEECH_PREROLL_MS / 1_000);

export type LocalSpeechTriggerPhase =
  | 'requesting-microphone'
  | 'whisper-loading'
  | 'vad-listening'
  | 'whisper-checking'
  | 'speech-confirmed';

export interface LocalSpeechTriggerResult {
  microphoneStream: MediaStream;
  pcm: Int16Array;
  transcript: string;
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
  signal?: AbortSignal;
  onPhaseChange?: (phase: LocalSpeechTriggerPhase) => void;
  onVadActivityChange?: (active: boolean) => void;
}): Promise<LocalSpeechTriggerResult> => {
  if (options.signal?.aborted) throw abortError();
  const setPhase = (phase: LocalSpeechTriggerPhase) => options.onPhaseChange?.(phase);
  let microphoneStream: MediaStream | null = null;
  let context: AudioContext | null = null;
  let worklet: AudioWorkletNode | null = null;
  let packetizer: RealtimePcmPacketizer | null = null;
  let settled = false;
  let microphoneStopped = false;
  let vadActive = false;

  const setVadActive = (active: boolean) => {
    if (vadActive === active) return;
    vadActive = active;
    options.onVadActivityChange?.(active);
  };

  const stopCapture = async (keepStream: boolean) => {
    setVadActive(false);
    if (worklet) {
      worklet.port.onmessage = null;
      try { worklet.disconnect(); } catch { /* already disconnected */ }
      worklet = null;
    }
    packetizer?.dispose();
    packetizer = null;
    if (context && context.state !== 'closed') {
      try { await context.close(); } catch { /* teardown is best effort */ }
    }
    context = null;
    if (!keepStream && !microphoneStopped) {
      microphoneStopped = true;
      microphoneStream?.getTracks().forEach(track => {
        try { track.stop(); } catch { /* already stopped */ }
      });
    }
  };

  let rejectAbort: ((error: Error) => void) | null = null;
  const abortPromise = new Promise<never>((_, reject) => {
    rejectAbort = reject;
  });
  const onAbort = () => {
    if (settled) return;
    settled = true;
    void stopCapture(false).finally(() => rejectAbort?.(abortError()));
  };
  const raceAbort = <T>(promise: Promise<T>): Promise<T> => (
    options.signal ? Promise.race([promise, abortPromise]) : promise
  );
  options.signal?.addEventListener('abort', onAbort, { once: true });

  try {
    setPhase('requesting-microphone');
    const microphoneRequest = navigator.mediaDevices.getUserMedia({
      audio: {
        sampleRate: INPUT_SAMPLE_RATE,
        channelCount: 1,
        echoCancellation: true,
        noiseSuppression: true,
      },
    });
    // getUserMedia itself is not abortable. If it resolves after cancellation,
    // stop the late stream instead of leaking a microphone track.
    void microphoneRequest.then(stream => {
      if (settled) stream.getTracks().forEach(track => track.stop());
    }, () => undefined);
    microphoneStream = await raceAbort(microphoneRequest);

    setPhase('whisper-loading');
    await raceAbort(options.detector.initialize());

    const AudioContextCtor: typeof AudioContext = window.AudioContext || (window as any).webkitAudioContext;
    context = new AudioContextCtor({ sampleRate: INPUT_SAMPLE_RATE });
    if (!context.audioWorklet) throw new Error('AudioWorklet is required for local speech detection.');
    await raceAbort(context.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL));
    if (context.state === 'suspended') {
      await raceAbort(context.resume());
      if (context.state === 'suspended') {
        throw new Error('Could not start the local speech AudioContext.');
      }
    }

    const gate = new SpeechGate({ requireConfirmation: true });
    const speechActivity = new SpeechActivityTracker({ sampleRate: INPUT_SAMPLE_RATE });
    let bufferedPackets: Int16Array[] = [];
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
          const activity = speechActivity.observe(packet.length, packetIsSpeech, now);
          if (activity.candidateReset && gate.isAwaitingConfirmation) {
            gate.rejectSpeech(now);
            bufferedPackets = [];
          }
          const decision = gate.evaluate(energy, now);
          if (decision.send) return;
          if (decision.reason === 'playback') {
            bufferedPackets = [];
            speechActivity.reset();
            return;
          }
          if (decision.reason === 'cooldown') {
            if (!packetIsSpeech) {
              bufferedPackets = [];
              speechActivity.reset();
              return;
            }
            // A person can begin a new turn during the short anti-chatter
            // cooldown. Retain those first samples even though they cannot yet
            // reopen the gate.
            bufferedPackets.push(packet);
            bufferedPackets = recentPcmPackets(bufferedPackets, BUFFER_SAMPLES);
            return;
          }
          bufferedPackets.push(packet);
          bufferedPackets = recentPcmPackets(bufferedPackets, BUFFER_SAMPLES);
          if (decision.reason !== 'awaiting-confirmation') return;
          if (!activity.hasMinimumSpeech) return;
          if (whisperBusy || now - lastWhisperRequestAt < LOCAL_WHISPER_REQUEST_INTERVAL_MS) return;

          const audio = pcmPacketsToWhisperWindow(bufferedPackets, INPUT_SAMPLE_RATE);
          if (!audio) return;
          whisperBusy = true;
          lastWhisperRequestAt = now;
          setPhase('whisper-checking');
          try {
            const transcript = (await options.detector.transcribe(audio)).trim();
            if (settled) return;
            const resultAt = Date.now();
            if (!isLikelySpeechTranscript(transcript)) {
              gate.rejectSpeech(resultAt);
              bufferedPackets = [];
              speechActivity.reset();
              setPhase('vad-listening');
              return;
            }
            if (!gate.confirmSpeech(resultAt)) {
              setPhase('vad-listening');
              return;
            }
            settled = true;
            speechActivity.reset();
            setPhase('speech-confirmed');
            const pcm = mergeInt16Arrays(recentPcmPackets(bufferedPackets, PREROLL_SAMPLES));
            await stopCapture(true);
            resolve({ microphoneStream: microphoneStream!, pcm, transcript });
          } catch (error) {
            rejectOnce(error instanceof Error ? error : new Error(String(error)));
          } finally {
            whisperBusy = false;
          }
        },
      });

      worklet = new AudioWorkletNode(context!, FLOAT_TO_INT16_PROCESSOR_NAME, {
        numberOfInputs: 1,
        numberOfOutputs: 0,
      });
      worklet.port.onmessage = (event: MessageEvent<CaptureWorkletMessage>) => {
        const pcm = event.data;
        if (pcm instanceof Int16Array && pcm.length > 0) packetizer?.push(pcm);
      };
      const source = context!.createMediaStreamSource(microphoneStream!);
      source.connect(worklet);
      setPhase('vad-listening');
    });

    return await raceAbort(result);
  } catch (error) {
    settled = true;
    await stopCapture(false);
    throw error;
  } finally {
    options.signal?.removeEventListener('abort', onAbort);
  }
};
