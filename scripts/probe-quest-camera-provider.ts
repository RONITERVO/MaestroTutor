// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
import { createHeadlessClient } from '../src/headless/client';
import { generateGeminiResponse } from '../src/core-sdk/gemini/generative';
import { getGeminiModels } from '../src/core-sdk/modelRegistry';
import { resolveLanguagePair } from '../src/core-sdk/chat/language';
import { buildCoreLiveSystemInstruction } from '../src/core-sdk/chat/liveContext';
import { composeMaestroSystemInstruction } from '../src/core/config/prompts';
import { runSyntheticLiveJourney } from '../src/core-sdk/media/syntheticLiveJourney';
import { createSyntheticPcmSource } from '../src/core-sdk/media/pcmInput';
import { LIVE_OPEN_TRIGGER } from '../shared/liveOpenReason';
import { imageOriginContext } from '../shared/prompts/context';
import { beginManagedJourneyBilling, evaluateManagedJourneyBilling, waitForManagedJourneyBillingSettlement } from '../src/headless/managedJourneyBilling';
const directory = resolve(process.argv[2] || '.quest-evidence/virtual-camera');
const origin = process.argv[3] || 'virtual-scene';
assert.ok(origin === 'virtual-scene' || origin === 'mixed-view');
const mixed = origin === 'mixed-view';
const question = mixed
 ? 'Hi! I am just starting Spanish. This is a practice screen-sharing picture. What color is the background behind the arrow, and how do I say that color in Spanish? Is this a shared headset screen or only a raw physical camera image? Please keep it simple.'
 : 'Hi! I am just starting Spanish. What color is the big block in this picture? Is this a virtual scene or a real camera view? Please keep it simple.';
const expectedColor = mixed ? /green|verde/i : /red|rojo/i;
const expectedSource = mixed ? /shar|screen|pantalla|compart|mixt|mixed/i : /virtual/i;
const mode = process.env.MAESTRO_HEADLESS_ACCESS_MODE;
assert.ok(mode === 'managed' || mode === 'byok');
const client = await createHeadlessClient({ profileName: `quest-camera-${mode}`, dataRoot: resolve(directory, 'profiles') });
const image = (await readFile(resolve(directory, mixed ? 'synthetic-mixed-live.jpg' : 'live-frame.jpg'))).toString('base64');
const wave = await readFile(resolve(directory, 'question.wav'));
assert.equal(wave.toString('ascii', 0, 4), 'RIFF');
let samples: Int16Array | undefined;
for (let offset = 12; offset + 8 <= wave.length;) {
 const id = wave.toString('ascii', offset, offset + 4), bytes = wave.readUInt32LE(offset + 4); offset += 8;
 if (id === 'data') { const data = Uint8Array.from(wave.subarray(offset, offset + bytes)); samples = new Int16Array(data.buffer); break; }
 offset += bytes + (bytes % 2);
}
assert.ok(samples?.length);
const pair = resolveLanguagePair({ nativeLanguageCode: 'en-US', targetLanguageCode: 'es-ES' });
const instruction = composeMaestroSystemInstruction(pair.baseSystemPrompt);
const report: Record<string, unknown> = { phase: 'starting', mode, providerUsed: true, origin, boundary: mixed ? 'Real original-app managed/BYOK text and Live providers, synthetic fixture through Chrome with mixed-view provenance and locally synthesized novice speech. No Quest compositor, physical surroundings or device performance exercised.' : 'Real original-app managed/BYOK text and Live providers, recorded native image through the Chrome camera pipeline, locally synthesized novice speech. No live headset/physical camera or device performance claim.' };
const save = () => writeFile(resolve(directory, `${mode}-provider.json`), JSON.stringify(report, null, 2));
const op = client.runtime.ids.create('camera-provider'); const before = mode === 'managed' ? await beginManagedJourneyBilling(client, op) : null;
try {
 report.phase = 'chat'; await save();
 const chat = await generateGeminiResponse(getGeminiModels().text.default, question, [], {
  aiClient: client.ai, systemInstruction: instruction, currentImages: [{ mimeType: 'image/jpeg', data: image, label: imageOriginContext(origin) }],
 });
 report.chat = { text: chat.text, model: chat.modelUsed }; await save();
 assert.match(chat.text, expectedColor); assert.match(chat.text, expectedSource);
 report.phase = 'live'; await save();
 const live = await runSyntheticLiveJourney(client.ai, { liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
  source: createSyntheticPcmSource({ pcm: samples!, sampleRate: 16000, pace: true, runtime: client.runtime }),
  model: getGeminiModels().audio.conversation, systemInstruction: buildCoreLiveSystemInstruction({ basePrompt: instruction, messages: [] }), thinkingMode: 'conversation', gateInputOnSpeech: false,
  manualActivityBoundaries: true, timeoutMs: 90000, requireRealtimeInputPacing: true, playModelAudioRealtime: true,
  videoFrames: [{ dataBase64: image, mimeType: 'image/jpeg', origin }], captureInputMedia: true,
 }, { runtime: client.runtime, operationId: op });
 report.live = { input: live.inputTranscript, output: live.outputTranscript, frames: live.sentVideoFrameCount, modelAudioSamples: live.modelAudioSampleCount, realtime: live.realtimeEvidence, connectedTurns: live.connectedTurnCount }; await save();
 assert.equal(live.sentVideoFrameCount, 1); assert.ok(live.modelAudioSampleCount > 0); assert.match(live.outputTranscript, expectedColor); assert.match(live.outputTranscript, expectedSource);
 assert.equal(live.liveInputMedia?.frames[0].data, image);
 assert.equal(live.liveInputMedia?.frames[0].origin, origin);
 report.phase = 'passed';
} catch (error) { report.phase = 'failed'; report.error = error instanceof Error ? error.message : 'Provider check failed'; throw error; }
finally {
 if (before) { const after = await waitForManagedJourneyBillingSettlement(client, op); report.billing = evaluateManagedJourneyBilling(before, after, { requirePaidUsage: true }); }
 await save();
 if (report.billing && !(report.billing as { passed: boolean }).passed) { report.phase = 'failed'; report.error = 'Managed billing verification failed'; await save(); throw new Error('Managed billing verification failed'); }
}
console.log(JSON.stringify({ mode, phase: report.phase, billing: report.billing }));
