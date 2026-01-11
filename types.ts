
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'error' | 'status' | 'system_selection';
  text?: string;
  recordedUtterance?: RecordedUtterance;
  translations?: Array<{
    spanish: string; 
    english: string; 
  }>;
  rawAssistantResponse?: string;
  chatSummary?: string;
  replySuggestions?: ReplySuggestion[];
  ttsAudioCache?: TtsAudioCacheEntry[];
  imageUrl?: string;
  imageMimeType?: string;
  imageFileUri?: string;
  llmImageUrl?: string;
  llmImageMimeType?: string;
  llmFileUri?: string;
  llmFileMimeType?: string;
  timestamp: number;
  thinking?: boolean;
  isGeneratingImage?: boolean;
  imageGenError?: string | null;
  imageGenerationStartTime?: number;
  tempSelectedNativeLangCode?: string;
  tempSelectedTargetLangCode?: string;
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

export type TtsProvider = 'gemini' | 'browser';
export type SttProvider = 'browser' | 'gemini';

export interface TTSSettings {
  provider?: TtsProvider;
  speakNative: boolean;
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
  imageGenerationModeEnabled: boolean;
  imageFocusedModeEnabled: boolean;
  isSuggestionMode: boolean;
  historyBookmarkMessageId?: string | null;
  maxVisibleMessages?: number;
  loadingGifs?: string[] | null;
  mediaOptimizationEnabled: boolean;
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
  _likeCounts?: Record<string, number>;
  _dislikeCounts?: Record<string, number>;
  _featureCounts?: Record<string, number>;
  schemaVersion?: 1 | 2;
}

declare global {
  interface Window {
    SpeechRecognition: any;
    webkitSpeechRecognition: any;
  }
}