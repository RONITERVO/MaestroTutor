// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { ThinkingLevel } from '@google/genai';
import {
  createLiveAudioInput,
  createLiveVideoInput,
  getLiveMaxThinkingConfig,
  getLiveMinimalThinkingConfig,
  usesLegacyGemini25LiveApi,
} from './liveModelCompatibility';

const legacyModel = 'gemini-2.5-flash-native-audio-preview-12-2025';
const currentModel = 'gemini-3.1-flash-live-preview';

describe('Live model compatibility', () => {
  it('recognizes Gemini 2.5 model names, including qualified names', () => {
    expect(usesLegacyGemini25LiveApi(legacyModel)).toBe(true);
    expect(usesLegacyGemini25LiveApi(`models/${legacyModel}`)).toBe(true);
    expect(usesLegacyGemini25LiveApi(currentModel)).toBe(false);
    expect(usesLegacyGemini25LiveApi('gemini-live-latest')).toBe(false);
  });

  it('disables 2.5 thinking and keeps current Live thinking minimal for TTS', () => {
    expect(getLiveMinimalThinkingConfig(legacyModel)).toEqual({
      thinkingBudget: 0,
      includeThoughts: false,
    });
    expect(getLiveMinimalThinkingConfig(currentModel)).toEqual({
      thinkingLevel: ThinkingLevel.MINIMAL,
      includeThoughts: false,
    });
  });

  it('uses each model family thinking contract for full Live conversations', () => {
    expect(getLiveMaxThinkingConfig(legacyModel)).toEqual({
      thinkingBudget: -1,
      includeThoughts: true,
    });
    expect(getLiveMaxThinkingConfig(currentModel)).toEqual({
      thinkingLevel: ThinkingLevel.HIGH,
      includeThoughts: true,
    });
  });

  it('routes media through legacy and dedicated realtime input fields', () => {
    const audio = { data: 'audio-data', mimeType: 'audio/pcm;rate=16000' };
    const video = { data: 'video-data', mimeType: 'image/jpeg' };

    expect(createLiveAudioInput(legacyModel, audio)).toEqual({ media: audio });
    expect(createLiveAudioInput(currentModel, audio)).toEqual({ audio });
    expect(createLiveVideoInput(legacyModel, video)).toEqual({ media: video });
    expect(createLiveVideoInput(currentModel, video)).toEqual({ video });
  });
});
