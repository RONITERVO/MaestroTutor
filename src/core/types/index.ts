// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'error' | 'status' | 'system_selection';
  maestroToolKind?: 'image' | 'audio-note' | 'music';
  text?: string;
  recordedUtterance?: RecordedUtterance;
  /** Translation pairs using generic field names to support all language combinations */
  translations?: Array<{
    /** Text in the target language (the language being learned) */
    target: string; 
    /** Romanization (pronunciation in Latin script) for non-Latin scripts, e.g. romaji, pinyin */
    romanization?: string;
    /** Text in the native language (the learner's native language) */
    native: string; 
  }>;
  rawAssistantResponse?: string;
  /** Full raw LLM output before local artifact parsing. */
  llmRawResponse?: string;
  chatSummary?: string;
  replySuggestions?: ReplySuggestion[];
  ttsAudioCache?: TtsAudioCacheEntry[];
  imageUrl?: string;
  imageMimeType?: string;
  /** Original attachment file name if available (e.g. README.md, app.tsx) */
  attachmentName?: string;
  /** Optimized (lower res) image for local storage to reduce DB size */
  storageOptimizedImageUrl?: string;
  /** MIME type of the storage-optimized image */
  storageOptimizedImageMimeType?: string;
  /** Expandable uploaded file variants for model-specific attachment handling. */
  uploadedFileVariants?: UploadedAttachmentVariant[];
  timestamp: number;
  thinking?: boolean;
  /** Model-authored thought summaries only; status text is tracked separately. */
  thinkingTrace?: string[];
  thinkingDraftText?: string;
  /** Human-readable phase label displayed on the upper line of the thinking bubble */
  thinkingPhase?: string;
  /** One-off status line for the lower line until model-authored output arrives. */
  thinkingStatusLine?: string;
  isGeneratingImage?: boolean;
  isGeneratingToolAttachment?: boolean;
  toolAttachmentPhase?: 'pending' | 'streaming' | 'finalizing';
  imageGenError?: string | null;
  imageGenerationStartTime?: number;
  toolAttachmentStartTime?: number;
  isLoadingArtifact?: boolean;
  artifactLoadStartTime?: number;
  tempSelectedNativeLangCode?: string;
  tempSelectedTargetLangCode?: string;
  /** Optional action hint for error messages (e.g. 'quota') to render contextual action buttons */
  errorAction?: string;
}

export type UploadedAttachmentTarget = 'chat' | 'image-generation';

export type UploadedAttachmentSource =
  | 'original'
  | 'video-keyframe'
  | 'office-text'
  | 'svg-source'
  | 'svg-rasterized'
  | 'derived';

export interface UploadedAttachmentVariant {
  /** Stable variant key so callers can upsert a specific surrogate. */
  id: string;
  uri: string;
  mimeType: string;
  targets: UploadedAttachmentTarget[];
  source: UploadedAttachmentSource;
  order?: number;
}

export interface SpeechPart {
  text: string;
  langCode: string;
  cacheKey?: string;
  cachedAudio?: string;
  onAudioCached?: (audioDataUrl: string, details: SpeechCacheDetails) => void;
  context?: SpeechCacheContext;
  voiceName?: string;
}

export interface RecordedUtterance {
  dataUrl: string;
  provider: SttProvider;
  langCode?: string;
  transcript?: string;
  sampleRate?: number;
}

export interface LanguagePair {
  id: string;
  name: string;
  targetLanguageName: string;
  targetLanguageCode: string;
  nativeLanguageName: string;
  nativeLanguageCode: string;
  baseSystemPrompt: string;
  baseReplySuggestionsPrompt: string;
  isDefault?: boolean;
}

/**
 * TTS Provider Options:
 * - 'gemini-live': Gemini Live API as TTS (streaming, queued lines, faster)
 * - 'gemini'/'browser': legacy providers retained for cache compatibility
 */
export type TtsProvider = 'gemini' | 'gemini-live' | 'browser';
export type SttProvider = 'browser' | 'gemini';

export interface TTSSettings {
  provider?: TtsProvider;
  speakNative: boolean;
  voiceName?: string;
}

export interface STTSettings {
  enabled: boolean;
  language: string;
  provider?: SttProvider;
}

export interface SmartReengagementSettings {
  thresholdSeconds: number;
  useVisualContext: boolean;
}

export interface CameraDevice {
  deviceId: string;
  label: string;
  facingMode?: 'user' | 'environment' | 'unknown';
}

export interface AppSettings {
  selectedLanguagePairId: string | null;
  selectedCameraId: string | null;
  sendWithSnapshotEnabled: boolean;
  tts: TTSSettings;
  stt: STTSettings;
  smartReengagement: SmartReengagementSettings;
  enableGoogleSearch: boolean;
  imageFocusedModeEnabled: boolean;
  isSuggestionMode: boolean;
  /** Show romanization (Latin-script pronunciation guide) for non-Latin target languages */
  showRomanization?: boolean;
  historyBookmarkMessageId?: string | null;
  maxVisibleMessages?: number;
  customColors?: Record<string, string>;
  savedThemePresets?: Array<{ name: string; description: string; colors: Record<string, string> }>;
}

export interface GroundingChunk {
  web?: {
    uri: string;
    title: string;
  };
  retrievedContext?: {
    uri: string;
    title: string;
  };
}

export interface ReplySuggestion {
  target: string;
  romanization?: string;
  native: string;
  ttsAudioCache?: TtsAudioCacheEntry[];
}

export interface TtsAudioCacheEntry {
  key: string;
  langCode: string;
  provider: TtsProvider;
  audioDataUrl: string;
  updatedAt: number;
  voiceName?: string;
  voiceId?: string;
}

export type SpeechCacheContext =
  | { source: 'message'; messageId: string }
  | { source: 'suggestion'; messageId: string; suggestionIndex: number; suggestionLang: 'target' | 'native' }
  | { source: 'adHoc' };

export interface SpeechCacheDetails {
  cacheKey?: string;
  provider: TtsProvider;
  langCode: string;
  fromCache: boolean;
}

export type MaestroActivityStage = 'idle' | 'observing_low' | 'observing_medium' | 'observing_high' | 'typing' | 'speaking' | 'listening';

export interface ChatMeta {
  bookmarkMessageId?: string | null;
  profileFingerprint?: string;
  profileLastUpdated?: number;
}

export interface UserProfile {
  lastUpdated: number;
  fingerprint?: string;
  summaryText: string;
  goals?: string[];
  interests?: string[];
  preferredCorrectionStyle?: string;
  levelEstimate?: string;
  weaknesses?: string[];
  likes?: string[];
  dislikes?: string[];
  keyFeatures?: string[];
  schemaVersion?: 1 | 2;
}

declare global {
  interface Window {
    SpeechRecognition: any;
    webkitSpeechRecognition: any;
  }
}
