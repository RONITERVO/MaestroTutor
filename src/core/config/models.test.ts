// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { afterEach, describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
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
  it('pins production text traffic to stable model ids', () => {
    expect(defaults.text).toEqual({
      default: 'gemini-3.7-flash',
      aux: 'gemini-3.5-flash-lite',
      translation: 'gemini-3.5-flash-lite',
      fallback: 'gemini-3.5-flash-lite',
    });
  });

  it('keeps the hosted registry identical to the compiled defaults', () => {
    const hosted = JSON.parse(readFileSync(
      new URL('../../../public/gemini-models.json', import.meta.url),
      'utf8',
    ));
    expect(hosted).toEqual(defaults);
  });

  it('keeps the Functions deployment template on the same stable text pins', () => {
    const template = readFileSync(
      new URL('../../../functions/.env.example', import.meta.url),
      'utf8',
    );
    const allowlist = template
      .split(/\r?\n/)
      .find(line => line.startsWith('MANAGED_ALLOWED_GEMINI_MODELS='));
    expect(allowlist).toBe(
      'MANAGED_ALLOWED_GEMINI_MODELS=gemini-3.7-flash,gemini-3.5-flash-lite,gemini-2.5-flash-image',
    );
  });

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
