// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

// -----------------------------------------------------------------------------
// TOKEN CATEGORIES & SUBTYPES
// -----------------------------------------------------------------------------

export const TOKEN_CATEGORY = {
  TTS: 'tts',
  STT: 'stt',
  GEN: 'gen',
  LIVE: 'live',
  VAD: 'vad',
  WHISPER: 'whisper',
  UI: 'ui',
} as const;

export type TokenCategory = typeof TOKEN_CATEGORY[keyof typeof TOKEN_CATEGORY];

export const TOKEN_SUBTYPE = {
  // TTS
  SPEAK: 'speak',

  // STT
  LISTEN: 'listen',

  // GEN
  RESPONSE: 'response',
  SUGGESTIONS: 'suggestions',
  CREATE_SUGGESTION: 'create-suggestion',

  // LIVE
  SESSION: 'session',
  CONNECTING: 'connecting',
  OBSERVER_SESSION: 'observer-session',
  OBSERVER_CONNECTING: 'observer-connecting',

  // Local speech trigger
  VAD_LISTEN: 'listen',
  VAD_ACTIVE: 'active',
  VAD_OBSERVER_LISTEN: 'observer-listen',
  VAD_OBSERVER_ACTIVE: 'observer-active',
  WHISPER_LOADING: 'loading',
  WHISPER_CHECKING: 'checking',
  WHISPER_TRIGGERED: 'triggered',
  WHISPER_OBSERVER_LOADING: 'observer-loading',
  WHISPER_OBSERVER_CHECKING: 'observer-checking',
  WHISPER_OBSERVER_TRIGGERED: 'observer-triggered',

  // UI - User interactions
  HOLD: 'hold',
  VIDEO_PLAY: 'video-play',
  VIEWING_ABOVE: 'viewing-above',
  BUBBLE_ANNOTATE: 'bubble-annotate',
  COMPOSER_ANNOTATE: 'composer-annotate',
  VIDEO_RECORD: 'video-record',
  AUDIO_NOTE: 'audio-note',
  SAVE_POPUP: 'save-popup',
  LOAD_POPUP: 'load-popup',
  MAESTRO_AVATAR: 'maestro-avatar',
  ATTACH_FILE: 'attach-file',

  // UI - Reengagement (internal)
  REENGAGE_WAIT: 'reengage-wait',
  REENGAGE_COUNTDOWN: 'reengage-countdown',
} as const;

export type TokenSubtype = typeof TOKEN_SUBTYPE[keyof typeof TOKEN_SUBTYPE];

// -----------------------------------------------------------------------------
// TOKEN UTILITIES
// -----------------------------------------------------------------------------

/** Build a token string from category and subtype. */
export const buildToken = (category: TokenCategory, subtype: string): string =>
  `${category}:${subtype}`;

/** Extract category from a token string. */
export const getTokenCategory = (token: string): string =>
  token.split(':')[0];

/** Extract subtype from a token string. */
export const getTokenSubtype = (token: string): string =>
  token.split(':')[1] || '';

/** Check if token is a reengagement token (should not block activity). */
export const isReengagementToken = (token: string): boolean =>
  token.startsWith(`${TOKEN_CATEGORY.UI}:${TOKEN_SUBTYPE.REENGAGE_WAIT}`) ||
  token.startsWith(`${TOKEN_CATEGORY.UI}:${TOKEN_SUBTYPE.REENGAGE_COUNTDOWN}`);

/** Activity owned by the passive speech observer itself. */
export const isSilentObserverActivityToken = (token: string): boolean =>
  token === buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_LISTEN)
  || token === buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_ACTIVE)
  || token === buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_LOADING)
  || token === buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_CHECKING)
  || token === buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_TRIGGERED)
  || token === buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.OBSERVER_CONNECTING)
  || token === buildToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.OBSERVER_SESSION);

// -----------------------------------------------------------------------------
// DISPLAY CONFIGURATION
// -----------------------------------------------------------------------------

export interface TokenDisplayConfig {
  /** Icon component name from Icons.tsx (string for type safety). */
  icon:
    | 'IconHandRaised'
    | 'IconCamera'
    | 'IconPlay'
    | 'IconBookOpen'
    | 'IconPencil'
    | 'IconMicrophone'
    | 'IconSave'
    | 'IconFolderOpen'
    | 'IconSend'
    | 'IconSpeaker'
    | 'IconKeyboard'
    | 'IconSparkles'
    | 'IconEyeOpen'
    | 'IconSleepingZzz'
    | 'IconPaperclip'
    | 'IconWaveform';
  /** Translation key for display text. */
  textKey: string;
  /** Translation key for tooltip/title. */
  titleKey: string;
  /** Whether icon should animate. */
  animate?: boolean;
  /** Priority for display (lower = higher priority). */
  priority: number;
  /** Status badge color config. */
  color?: { bg: string; border: string; text: string };
}

/**
 * Display configuration for UI tokens.
 * Keyed by subtype for O(1) lookup.
 */
export const UI_TOKEN_DISPLAY: Record<string, TokenDisplayConfig> = {
  [TOKEN_SUBTYPE.HOLD]: {
    icon: 'IconHandRaised',
    textKey: 'chat.maestro.holding',
    titleKey: 'chat.maestro.title.holding',
    animate: true,
    priority: 0,
    color: { bg: 'bg-flag-hold-bg', border: 'border-flag-hold-border', text: 'text-flag-hold-text' },
  },
  [TOKEN_SUBTYPE.VIDEO_PLAY]: {
    icon: 'IconPlay',
    textKey: 'chat.header.watchingVideo',
    titleKey: 'chat.header.watchingVideo',
    priority: 10,
  },
  [TOKEN_SUBTYPE.VIEWING_ABOVE]: {
    icon: 'IconBookOpen',
    textKey: 'chat.header.viewingAbove',
    titleKey: 'chat.header.viewingAbove',
    priority: 20,
  },
  [TOKEN_SUBTYPE.BUBBLE_ANNOTATE]: {
    icon: 'IconPencil',
    textKey: 'chat.header.annotating',
    titleKey: 'chat.header.annotating',
    priority: 15,
  },
  [TOKEN_SUBTYPE.COMPOSER_ANNOTATE]: {
    icon: 'IconPencil',
    textKey: 'chat.header.annotating',
    titleKey: 'chat.header.annotating',
    priority: 15,
  },
  [TOKEN_SUBTYPE.VIDEO_RECORD]: {
    icon: 'IconCamera',
    textKey: 'chat.header.recordingVideo',
    titleKey: 'chat.header.recordingVideo',
    priority: 5,
  },
  [TOKEN_SUBTYPE.AUDIO_NOTE]: {
    icon: 'IconMicrophone',
    textKey: 'chat.header.recordingAudio',
    titleKey: 'chat.header.recordingAudio',
    priority: 5,
  },
  [TOKEN_SUBTYPE.SAVE_POPUP]: {
    icon: 'IconSave',
    textKey: 'chat.header.savePopup',
    titleKey: 'chat.header.savePopup',
    priority: 25,
  },
  [TOKEN_SUBTYPE.LOAD_POPUP]: {
    icon: 'IconFolderOpen',
    textKey: 'chat.header.loadPopup',
    titleKey: 'chat.header.loadPopup',
    priority: 25,
  },
  [TOKEN_SUBTYPE.MAESTRO_AVATAR]: {
    icon: 'IconSend',
    textKey: 'chat.header.maestroAvatar',
    titleKey: 'chat.header.maestroAvatar',
    priority: 30,
  },
  [TOKEN_SUBTYPE.ATTACH_FILE]: {
    icon: 'IconPaperclip',
    textKey: 'chat.attachImageFromFile',
    titleKey: 'chat.attachImageFromFile',
    priority: 35,
  },
};

/** Display configuration for explicit Live transport phases. */
export const LIVE_TOKEN_DISPLAY: Record<string, TokenDisplayConfig> = {
  [TOKEN_SUBTYPE.CONNECTING]: {
    icon: 'IconSparkles',
    textKey: 'chat.maestro.liveConnecting',
    titleKey: 'chat.maestro.title.liveConnecting',
    animate: true,
    priority: 3,
  },
  [TOKEN_SUBTYPE.SESSION]: {
    icon: 'IconCamera',
    textKey: 'chat.header.liveSession',
    titleKey: 'chat.header.liveSession',
    priority: 3,
  },
  [TOKEN_SUBTYPE.OBSERVER_CONNECTING]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.liveConnecting',
    titleKey: 'chat.maestro.title.liveConnecting',
    animate: true,
    priority: 3,
  },
  [TOKEN_SUBTYPE.OBSERVER_SESSION]: {
    icon: 'IconMicrophone',
    textKey: 'chat.maestro.liveActive',
    titleKey: 'chat.maestro.title.liveActive',
    priority: 3,
  },
};

/** Display configuration for local speech gating and detailed Live phases. */
export const SPEECH_GATE_TOKEN_DISPLAY: Record<string, TokenDisplayConfig> = {
  [buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_LISTEN)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.localListening',
    titleKey: 'chat.maestro.title.localListening',
    priority: 4,
  },
  [buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_ACTIVE)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.localListening',
    titleKey: 'chat.maestro.title.localListening',
    animate: true,
    priority: 0,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_LOADING)]: {
    icon: 'IconSparkles',
    textKey: 'chat.maestro.whisperLoading',
    titleKey: 'chat.maestro.title.whisperLoading',
    animate: true,
    priority: 2,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_CHECKING)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.whisperChecking',
    titleKey: 'chat.maestro.title.whisperChecking',
    animate: true,
    priority: 1,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_TRIGGERED)]: {
    icon: 'IconMicrophone',
    textKey: 'chat.maestro.whisperTriggered',
    titleKey: 'chat.maestro.title.whisperTriggered',
    priority: 0,
  },
  [buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_LISTEN)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.localListening',
    titleKey: 'chat.maestro.title.localListening',
    priority: 4,
  },
  [buildToken(TOKEN_CATEGORY.VAD, TOKEN_SUBTYPE.VAD_OBSERVER_ACTIVE)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.localListening',
    titleKey: 'chat.maestro.title.localListening',
    animate: true,
    priority: 0,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_LOADING)]: {
    icon: 'IconSparkles',
    textKey: 'chat.maestro.whisperLoading',
    titleKey: 'chat.maestro.title.whisperLoading',
    animate: true,
    priority: 2,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_CHECKING)]: {
    icon: 'IconWaveform',
    textKey: 'chat.maestro.whisperChecking',
    titleKey: 'chat.maestro.title.whisperChecking',
    animate: true,
    priority: 1,
  },
  [buildToken(TOKEN_CATEGORY.WHISPER, TOKEN_SUBTYPE.WHISPER_OBSERVER_TRIGGERED)]: {
    icon: 'IconMicrophone',
    textKey: 'chat.maestro.whisperTriggered',
    titleKey: 'chat.maestro.title.whisperTriggered',
    priority: 0,
  },
};
