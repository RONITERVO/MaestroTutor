// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import {
  isLikelySpeechTranscript,
  OBSERVER_SPEECH_BUFFER_MS,
  OBSERVER_SPEECH_PREROLL_MS,
  OBSERVER_WHISPER_MODEL,
  OBSERVER_WHISPER_REQUEST_INTERVAL_MS,
  OBSERVER_WHISPER_WINDOW_MS,
  OBSERVER_WHISPER_MIN_AUDIO_MS,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
} from './observerSpeechDetection';

/** Shared names for every Gemini Live microphone path that uses the detector. */
export const LOCAL_WHISPER_MODEL = OBSERVER_WHISPER_MODEL;
export const LOCAL_WHISPER_WINDOW_MS = OBSERVER_WHISPER_WINDOW_MS;
export const LOCAL_WHISPER_MIN_AUDIO_MS = OBSERVER_WHISPER_MIN_AUDIO_MS;
export const LOCAL_WHISPER_REQUEST_INTERVAL_MS = OBSERVER_WHISPER_REQUEST_INTERVAL_MS;
export const LOCAL_SPEECH_BUFFER_MS = OBSERVER_SPEECH_BUFFER_MS;
export const LOCAL_SPEECH_PREROLL_MS = OBSERVER_SPEECH_PREROLL_MS;

export {
  isLikelySpeechTranscript,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
};
