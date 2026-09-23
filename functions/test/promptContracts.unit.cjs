// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const test = require('node:test');
const {
  prepareManagedGenerationConfig,
  managedGenerationOutputLimit,
  requireSafeManagedLiveConfig,
} = require('../lib/functions/src/geminiPolicy.js');
const {
  buildMusicPrompt,
  REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
  buildLiveSttSystemInstruction,
} = require('../lib/shared/prompts/index.js');

test('compiled Functions catalogue retains the shipped music suffix byte for byte', () => {
  assert.equal(buildMusicPrompt('A calm scale exercise'),
    'A calm scale exercise. Instrumental only. No vocals, no lyrics, no copyrighted melodies. Original educational backing track.');
});

test('managed text policy preserves instructions, structured output and tools', () => {
  const config = {
    systemInstruction: 'Exact words.\n\n[FI] Ääkköset — 日本語',
    responseJsonSchema: REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
    responseMimeType: 'application/json',
    tools: [{ googleSearch: {} }],
    thinkingConfig: { thinkingLevel: 'HIGH' },
  };
  const original = structuredClone(config);
  assert.deepEqual(prepareManagedGenerationConfig(config, 'gemini-3.8-flash'), {
    ...original, maxOutputTokens: managedGenerationOutputLimit('gemini-3.8-flash'),
  });
  assert.deepEqual(config, original);
});

test('managed Live policy preserves system parts, transcription and speech settings', () => {
  const config = {
    systemInstruction: { parts: [{ text: buildLiveSttSystemInstruction({ lastAssistantMessage: 'Hola', replySuggestions: ['Sí'] }) }] },
    responseModalities: ['AUDIO'], inputAudioTranscription: {}, outputAudioTranscription: {},
    speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: 'Kore' } } },
    thinkingConfig: { thinkingBudget: 0 },
  };
  const original = structuredClone(config);
  const result = requireSafeManagedLiveConfig(config);
  for (const key of Object.keys(original)) assert.deepEqual(result[key], original[key], key);
  assert.deepEqual(config, original);
});
