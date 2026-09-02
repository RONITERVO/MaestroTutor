// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import {
  isLikelySpeechTranscript,
  OBSERVER_SPEECH_BUFFER_MS,
  OBSERVER_SPEECH_MIN_ACTIVE_MS,
  OBSERVER_SPEECH_PREROLL_MS,
  OBSERVER_WHISPER_MODEL,
  OBSERVER_WHISPER_REQUEST_INTERVAL_MS,
  OBSERVER_WHISPER_WINDOW_MS,
  OBSERVER_WHISPER_MIN_AUDIO_MS,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
  SpeechActivityTracker,
} from './observerSpeechDetection';
import {
  DEFAULT_SPEECH_GATE,
  isSpeechLike,
  type AudioEnergy,
} from '../../../shared/audio/speechGate';

/** Shared names for every Gemini Live microphone path that uses the detector. */
export const LOCAL_WHISPER_MODEL = OBSERVER_WHISPER_MODEL;
export const LOCAL_WHISPER_WINDOW_MS = OBSERVER_WHISPER_WINDOW_MS;
export const LOCAL_WHISPER_MIN_AUDIO_MS = OBSERVER_WHISPER_MIN_AUDIO_MS;
export const LOCAL_WHISPER_REQUEST_INTERVAL_MS = OBSERVER_WHISPER_REQUEST_INTERVAL_MS;
export const LOCAL_SPEECH_BUFFER_MS = OBSERVER_SPEECH_BUFFER_MS;
export const LOCAL_SPEECH_MIN_ACTIVE_MS = OBSERVER_SPEECH_MIN_ACTIVE_MS;
export const LOCAL_SPEECH_PREROLL_MS = OBSERVER_SPEECH_PREROLL_MS;

export interface FreshSpeechFallbackDecision {
  action: 'wait' | 'confirm' | 'expire';
  onsetAt: number | null;
}

/**
 * When Whisper is still loading after its grace period, do not open an old
 * candidate merely because it is still marked as awaiting confirmation.
 * Silence expires it; current speech must establish a fresh sustained onset.
 */
export const evaluateFreshSpeechFallback = (
  energy: AudioEnergy,
  onsetAt: number | null,
  now: number,
  openAfterMs = DEFAULT_SPEECH_GATE.openAfterMs,
): FreshSpeechFallbackDecision => {
  if (!isSpeechLike(energy, DEFAULT_SPEECH_GATE)) {
    return { action: 'expire', onsetAt: null };
  }

  const nextOnsetAt = onsetAt ?? now;
  if (now - nextOnsetAt < openAfterMs) {
    return { action: 'wait', onsetAt: nextOnsetAt };
  }
  return { action: 'confirm', onsetAt: nextOnsetAt };
};

export {
  isLikelySpeechTranscript,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
  SpeechActivityTracker,
};
