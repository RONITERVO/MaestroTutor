// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Auditable reasons that are allowed to open a Gemini Live transport.
 *
 * Keep this allowlist deliberately small. Adding a value authorizes a new way
 * to start a paid realtime session and therefore requires a product and billing
 * review, not just a new call site.
 */
export const LIVE_OPEN_TRIGGER = {
  WHISPER_OBSERVER: 'whisper.observer',
  WHISPER_STT: 'whisper.stt',
  USER_CAMERA_LIVE: 'user.camera-live',
  USER_HEADLESS_LIVE: 'user.headless-live',
  TOOL_AUDIO_NOTE: 'tool.audio-note',
  VOICE_TTS_CLICK: 'voice.tts-click',
  VOICE_TTS_AUTO_MESSAGE: 'voice.tts-auto-message',
} as const;

export type LiveOpenTrigger = typeof LIVE_OPEN_TRIGGER[keyof typeof LIVE_OPEN_TRIGGER];
export type LiveOpenOrigin = 'whisper' | 'user' | 'tool' | 'voice';

/** Narrow surface contracts keep a valid trigger from being used at the wrong call site. */
export type ConversationLiveOpenTrigger =
  | typeof LIVE_OPEN_TRIGGER.WHISPER_OBSERVER
  | typeof LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE;
export type TtsLiveOpenTrigger =
  | typeof LIVE_OPEN_TRIGGER.VOICE_TTS_CLICK
  | typeof LIVE_OPEN_TRIGGER.VOICE_TTS_AUTO_MESSAGE;
export type AudioNoteLiveOpenTrigger =
  | typeof LIVE_OPEN_TRIGGER.TOOL_AUDIO_NOTE
  | typeof LIVE_OPEN_TRIGGER.VOICE_TTS_CLICK;
export type HeadlessLiveOpenTrigger = typeof LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE;

export interface LiveOpenReason {
  trigger: LiveOpenTrigger;
  requestId: string;
  requestedAt: string;
}

const LIVE_OPEN_TRIGGER_SET = new Set<string>(Object.values(LIVE_OPEN_TRIGGER));
const SAFE_REQUEST_ID = /^[A-Za-z0-9][A-Za-z0-9._:-]{7,127}$/;

export const isLiveOpenTrigger = (value: unknown): value is LiveOpenTrigger => (
  typeof value === 'string' && LIVE_OPEN_TRIGGER_SET.has(value)
);

export const getLiveOpenOrigin = (trigger: LiveOpenTrigger): LiveOpenOrigin => (
  trigger.slice(0, trigger.indexOf('.')) as LiveOpenOrigin
);

export const parseLiveOpenReason = (value: unknown): LiveOpenReason | null => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
  const candidate = value as Record<string, unknown>;
  if (!isLiveOpenTrigger(candidate.trigger)) return null;
  if (typeof candidate.requestId !== 'string' || !SAFE_REQUEST_ID.test(candidate.requestId)) return null;
  if (typeof candidate.requestedAt !== 'string') return null;
  const requestedAtMs = Date.parse(candidate.requestedAt);
  if (!Number.isFinite(requestedAtMs)) return null;
  return {
    trigger: candidate.trigger,
    requestId: candidate.requestId,
    requestedAt: new Date(requestedAtMs).toISOString(),
  };
};

let fallbackRequestSequence = 0;

export const createLiveOpenReason = (
  trigger: LiveOpenTrigger,
  options: { requestId?: string; now?: Date } = {},
): LiveOpenReason => {
  const randomId = typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : `fallback-${Date.now()}-${++fallbackRequestSequence}`;
  const reason = parseLiveOpenReason({
    trigger,
    requestId: options.requestId || `live-${randomId}`,
    requestedAt: (options.now || new Date()).toISOString(),
  });
  if (!reason) throw new Error('Could not create a valid Gemini Live open reason.');
  return reason;
};

export const requireLiveOpenReason = (value: unknown): LiveOpenReason => {
  const reason = parseLiveOpenReason(value);
  if (!reason) {
    throw new Error('A valid, auditable Gemini Live open reason is required.');
  }
  return reason;
};
