// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const assert = require('node:assert/strict');
const test = require('node:test');
const {
  applyManagedGenerationLimits,
  collectGeminiFileUris,
  requireAllowedManagedModel,
  requirePricedManagedGenerationModel,
  requireSafeManagedGenerationConfig,
  requireSafeManagedLiveConfig,
  resolveManagedContentOperation,
  usesManagedGoogleSearch,
} = require('../lib/functions/src/geminiPolicy.js');
const {
  googleSearchQueriesToUsd,
  usageMetadataToUsd,
} = require('../lib/functions/src/pricing.js');

test('billing operation is derived from server-visible request shape', () => {
  assert.equal(resolveManagedContentOperation(undefined, false), 'generateContent');
  assert.equal(resolveManagedContentOperation(undefined, true), 'streamContent');
  assert.equal(
    resolveManagedContentOperation({ responseModalities: ['TEXT', 'image'] }, true),
    'generateImage',
  );
  assert.equal(
    resolveManagedContentOperation(undefined, false, 'gemini-2.5-flash-image'),
    'generateImage',
  );
});

test('prepaid generation rejects allowlisted models without a rate', () => {
  assert.equal(
    requirePricedManagedGenerationModel('gemini-flash-latest'),
    'gemini-flash-latest',
  );
  assert.throws(
    () => requirePricedManagedGenerationModel('unpriced-preview'),
    (error) => error.status === 500 && /no billable pricing rule/.test(error.message),
  );
});

test('managed config allows only server-priced tools and no transport overrides', () => {
  const searchConfig = { tools: [{ googleSearch: {} }], responseMimeType: 'application/json' };
  assert.equal(requireSafeManagedGenerationConfig(searchConfig), searchConfig);
  assert.equal(usesManagedGoogleSearch(searchConfig), true);
  assert.throws(
    () => requireSafeManagedGenerationConfig({ tools: [{ codeExecution: {} }] }),
    (error) => error.status === 400 && /Google Search/.test(error.message),
  );
  assert.deepEqual(
    applyManagedGenerationLimits({ maxOutputTokens: 50_000 }, 8_192),
    { maxOutputTokens: 8_192 },
  );
  assert.deepEqual(
    applyManagedGenerationLimits({ maxOutputTokens: 512 }, 8_192),
    { maxOutputTokens: 512 },
  );
  assert.throws(
    () => applyManagedGenerationLimits({ candidateCount: 2 }, 8_192),
    (error) => error.status === 400 && /one response candidate/.test(error.message),
  );
  assert.throws(
    () => requireSafeManagedGenerationConfig({ httpOptions: { baseUrl: 'https://example.test' } }),
    (error) => error.status === 400 && /httpOptions/.test(error.message),
  );
});

test('managed Live config is scopeable but cannot mint tool-enabled tokens', () => {
  const liveConfig = { responseModalities: ['AUDIO'], systemInstruction: 'Be concise.' };
  assert.equal(requireSafeManagedLiveConfig(liveConfig), liveConfig);
  assert.throws(
    () => requireSafeManagedLiveConfig({ tools: [{ googleSearch: {} }] }),
    (error) => error.status === 400 && /tools/.test(error.message),
  );
});

test('Google Search queries are charged at the registry list price', () => {
  assert.equal(googleSearchQueriesToUsd(0), 0);
  assert.equal(googleSearchQueriesToUsd(1), 0.014);
  assert.equal(googleSearchQueriesToUsd(10), 0.14);
});

test('an image request is charged for images actually produced', () => {
  const usage = { promptTokenCount: 100, candidatesTokenCount: 0 };
  const withoutImage = usageMetadataToUsd(
    'gemini-2.5-flash-image',
    usage,
    'generateImage',
    0,
  );
  const withImage = usageMetadataToUsd(
    'gemini-2.5-flash-image',
    usage,
    'generateImage',
    1,
  );
  assert.ok(withoutImage < 0.001);
  assert.ok(withImage >= 0.039);
});

test('managed models are allowlisted, including SDK-qualified names', () => {
  const allowed = new Set(['gemini-flash-latest', 'lyria-realtime-exp']);
  assert.equal(
    requireAllowedManagedModel('gemini-flash-latest', allowed, 'generation'),
    'gemini-flash-latest',
  );
  assert.equal(
    requireAllowedManagedModel('models/lyria-realtime-exp', allowed, 'music'),
    'models/lyria-realtime-exp',
  );
  assert.throws(
    () => requireAllowedManagedModel('gemini-unpriced-preview', allowed, 'generation'),
    (error) => error.status === 400 && /not enabled/.test(error.message),
  );
});

test('every nested file URI is discovered once', () => {
  const payload = {
    contents: [{ parts: [
      { fileData: { fileUri: 'https://generativelanguage.googleapis.com/v1beta/files/one' } },
      { fileData: { fileUri: 'files/two' } },
    ] }],
    config: {
      systemInstruction: { parts: [{ fileUri: 'files/two' }] },
    },
  };

  assert.deepEqual(
    new Set(collectGeminiFileUris(payload)),
    new Set([
      'https://generativelanguage.googleapis.com/v1beta/files/one',
      'files/two',
    ]),
  );
});
