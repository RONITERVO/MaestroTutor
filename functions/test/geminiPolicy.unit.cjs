// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const assert = require('node:assert/strict');
const test = require('node:test');
const {
  prepareManagedGenerationConfig,
  buildManagedPromptTokenCountInputs,
  collectGeminiFileUris,
  requireAllowedManagedModel,
  requireManagedLiveOpenReason,
  requirePricedManagedGenerationModel,
  requireSafeManagedGenerationConfig,
  requireSafeManagedLiveConfig,
  resolvePinnedManagedGenerationModel,
  resolveManagedContentOperation,
  usesManagedGoogleSearch,
} = require('../lib/functions/src/geminiPolicy.js');
const {
  googleSearchQueriesToUsd,
  usageMetadataToUsd,
} = require('../lib/functions/src/pricing.js');
const { appConfig } = require('../lib/functions/src/config.js');

test('managed generation defaults pin provider-stable model ids', () => {
  assert.deepEqual(
    [...appConfig.managedAllowedGeminiModels],
    ['gemini-3.7-flash', 'gemini-3.5-flash-lite', 'gemini-2.5-flash-image'],
  );
});

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
    requirePricedManagedGenerationModel('gemini-3.7-flash'),
    'gemini-3.7-flash',
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
  assert.equal(prepareManagedGenerationConfig({ maxOutputTokens: 50_000 }), undefined);
  assert.deepEqual(
    prepareManagedGenerationConfig({ maxOutputTokens: 512, responseMimeType: 'text/plain' }),
    { responseMimeType: 'text/plain' },
  );
  for (const unsupported of ['candidateCount', 'temperature', 'topP', 'topK']) {
    assert.throws(
      () => prepareManagedGenerationConfig({ [unsupported]: 1 }),
      (error) => error.status === 400 && error.message.includes(unsupported),
    );
  }
  assert.throws(
    () => requireSafeManagedGenerationConfig({ httpOptions: { baseUrl: 'https://example.test' } }),
    (error) => error.status === 400 && /httpOptions/.test(error.message),
  );
});

test('prompt token counting separates Developer API-incompatible config', () => {
  const contents = [{ role: 'user', parts: [{ text: 'hello' }] }];
  const inputs = buildManagedPromptTokenCountInputs(contents, {
    systemInstruction: 'Teach Finnish.',
    tools: [{ googleSearch: {} }],
  });
  assert.equal(inputs[0], contents);
  assert.equal(inputs[1], 'Teach Finnish.');
  assert.match(inputs[2], /googleSearch/);
  assert.doesNotMatch(inputs[2], /maxOutputTokens/);
});

test('managed Live config is scopeable but cannot mint tool-enabled tokens', () => {
  const liveConfig = { responseModalities: ['AUDIO'], systemInstruction: 'Be concise.' };
  assert.equal(requireSafeManagedLiveConfig(liveConfig), liveConfig);
  assert.throws(
    () => requireSafeManagedLiveConfig({ tools: [{ googleSearch: {} }] }),
    (error) => error.status === 400 && /tools/.test(error.message),
  );
});

test('managed Live tokens require a reviewed auditable open reason', () => {
  const reason = {
    trigger: 'whisper.observer',
    requestId: 'observer-request-1234',
    requestedAt: '2026-09-02T12:34:56+00:00',
  };
  assert.deepEqual(requireManagedLiveOpenReason(reason), {
    ...reason,
    requestedAt: '2026-09-02T12:34:56.000Z',
  });
  for (const invalid of [
    undefined,
    { ...reason, trigger: 'user.unreviewed-control' },
    { ...reason, requestId: 'short' },
  ]) {
    assert.throws(
      () => requireManagedLiveOpenReason(invalid),
      (error) => error.status === 400 && /auditable Gemini Live open reason/.test(error.message),
    );
  }
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

test('settlement prices the provider-resolved model version', () => {
  const billedUsd = usageMetadataToUsd(
    'gemini-3.5-flash-lite',
    { promptTokenCount: 1_000_000, candidatesTokenCount: 1_000_000 },
    'generateContent',
    0,
    0,
    'gemini-3.7-flash',
  );
  assert.equal(billedUsd, 4.5);
});

test('managed models are allowlisted, including SDK-qualified names', () => {
  const allowed = new Set(['gemini-3.7-flash', 'gemini-3.5-flash-lite', 'lyria-realtime-exp']);
  assert.equal(resolvePinnedManagedGenerationModel('gemini-flash-latest'), 'gemini-3.7-flash');
  assert.equal(resolvePinnedManagedGenerationModel('models/gemini-flash-lite-latest'), 'gemini-3.5-flash-lite');
  assert.equal(
    requireAllowedManagedModel(
      resolvePinnedManagedGenerationModel('gemini-flash-latest'),
      allowed,
      'generation',
    ),
    'gemini-3.7-flash',
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
