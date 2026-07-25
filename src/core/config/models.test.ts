// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { afterEach, describe, expect, it } from 'vitest';
import {
  getGeminiModels,
  getModelRegistryDefaults,
  setGeminiModels,
} from './models';

const defaults = getModelRegistryDefaults();

afterEach(() => {
  setGeminiModels(defaults);
});

describe('Gemini model registry', () => {
  it('uses Gemini 2.5 for TTS and the latest Live model for STT and conversations', () => {
    expect(defaults.audio).toEqual({
      tts: 'gemini-2.5-flash-native-audio-preview-12-2025',
      stt: 'gemini-3.1-flash-live-preview',
      conversation: 'gemini-3.1-flash-live-preview',
    });
  });

  it('allows each audio purpose to be configured independently', () => {
    setGeminiModels({
      audio: {
        tts: 'custom-tts-model',
        stt: 'custom-stt-model',
        conversation: 'custom-conversation-model',
      },
    });

    expect(getGeminiModels().audio).toEqual({
      tts: 'custom-tts-model',
      stt: 'custom-stt-model',
      conversation: 'custom-conversation-model',
    });
  });

  it('falls back per audio purpose when applying a partial override', () => {
    setGeminiModels({ audio: { tts: 'custom-tts-model' } });

    expect(getGeminiModels().audio).toEqual({
      tts: 'custom-tts-model',
      stt: defaults.audio.stt,
      conversation: defaults.audio.conversation,
    });
  });
});
