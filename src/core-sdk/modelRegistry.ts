// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import {
  clonePricingRegistry,
  DEFAULT_GEMINI_PRICING,
  GeminiPricingRegistry,
  isGeminiPricingRegistry,
} from '../core/config/pricing';

export interface GeminiModelRegistry {
  text: {
    default: string;
    aux: string;
    translation: string;
    fallback: string;
  };
  image: {
    generation: string;
  };
  audio: {
    tts: string;
    stt: string;
    /** Shared by interactive Live sessions and silent-observer re-engagement. */
    conversation: string;
  };
  music: {
    generation: string;
  };
  pricing: GeminiPricingRegistry;
}

export interface GeminiModelRegistryInput {
  text?: {
    default?: string;
    aux?: string;
    translation?: string;
    fallback?: string;
  };
  image?: {
    generation?: string;
  };
  audio?: {
    tts?: string;
    stt?: string;
    /** Shared by interactive Live sessions and silent-observer re-engagement. */
    conversation?: string;
  };
  music?: {
    generation?: string;
  };
  pricing?: GeminiPricingRegistry;
}

const DEFAULT_TEXT_FALLBACK_MODEL = 'gemini-3.5-flash-lite';
const DEFAULT_TTS_LIVE_MODEL = 'gemini-2.5-flash-native-audio-preview-12-2025';
const DEFAULT_GENERAL_LIVE_MODEL = 'gemini-2.5-flash-native-audio-preview-12-2025';

const DEFAULT_GEMINI_MODELS: GeminiModelRegistry = {
  text: {
    default: 'gemini-3.8-flash',
    aux: 'gemini-3.8-flash',
    translation: 'gemini-3.5-flash-lite',
    fallback: DEFAULT_TEXT_FALLBACK_MODEL,
  },
  image: {
    generation: 'gemini-2.5-flash-image',
  },
  audio: {
    tts: DEFAULT_TTS_LIVE_MODEL,
    stt: DEFAULT_GENERAL_LIVE_MODEL,
    conversation: DEFAULT_GENERAL_LIVE_MODEL,
  },
  music: {
    generation: 'lyria-realtime-exp',
  },
  pricing: clonePricingRegistry(DEFAULT_GEMINI_PRICING),
};

let currentModels: GeminiModelRegistry = { ...DEFAULT_GEMINI_MODELS };

const isNonEmptyString = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0;

export const isValidRegistry = (value: any): value is {
  text: { default: string; aux: string; translation: string; fallback?: string };
  image: { generation: string };
  audio: { tts: string; stt: string; conversation: string };
  music: { generation: string };
  pricing?: GeminiPricingRegistry;
} => {
  if (!value || typeof value !== 'object') return false;
  return (
    isNonEmptyString(value?.text?.default) &&
    isNonEmptyString(value?.text?.aux) &&
    isNonEmptyString(value?.text?.translation) &&
    (value?.text?.fallback === undefined || isNonEmptyString(value?.text?.fallback)) &&
    isNonEmptyString(value?.image?.generation) &&
    isNonEmptyString(value?.audio?.tts) &&
    isNonEmptyString(value?.audio?.stt) &&
    isNonEmptyString(value?.audio?.conversation) &&
    isNonEmptyString(value?.music?.generation) &&
    (value?.pricing === undefined || isGeminiPricingRegistry(value.pricing))
  );
};

const mergeWithDefaults = (value: GeminiModelRegistryInput): GeminiModelRegistry => ({
  text: {
    default: value?.text?.default ?? DEFAULT_GEMINI_MODELS.text.default,
    aux: value?.text?.aux ?? DEFAULT_GEMINI_MODELS.text.aux,
    translation: value?.text?.translation ?? DEFAULT_GEMINI_MODELS.text.translation,
    fallback: value?.text?.fallback ?? DEFAULT_GEMINI_MODELS.text.fallback,
  },
  image: {
    generation: value?.image?.generation ?? DEFAULT_GEMINI_MODELS.image.generation,
  },
  audio: {
    tts: value?.audio?.tts ?? DEFAULT_GEMINI_MODELS.audio.tts,
    stt: value?.audio?.stt ?? DEFAULT_GEMINI_MODELS.audio.stt,
    conversation: value?.audio?.conversation ?? DEFAULT_GEMINI_MODELS.audio.conversation,
  },
  music: {
    generation: value?.music?.generation ?? DEFAULT_GEMINI_MODELS.music.generation,
  },
  pricing: value?.pricing
    ? clonePricingRegistry(value.pricing)
    : clonePricingRegistry(DEFAULT_GEMINI_MODELS.pricing),
});

export const getGeminiModels = (): GeminiModelRegistry => currentModels;

export const setGeminiModels = (value: GeminiModelRegistryInput) => {
  currentModels = mergeWithDefaults(value);
};

export const getModelRegistryDefaults = (): GeminiModelRegistry => ({
  ...DEFAULT_GEMINI_MODELS,
  text: { ...DEFAULT_GEMINI_MODELS.text },
  image: { ...DEFAULT_GEMINI_MODELS.image },
  audio: { ...DEFAULT_GEMINI_MODELS.audio },
  music: { ...DEFAULT_GEMINI_MODELS.music },
  pricing: clonePricingRegistry(DEFAULT_GEMINI_MODELS.pricing),
});
