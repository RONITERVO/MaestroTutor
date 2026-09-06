// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// One bounded, real-provider headless turn. Never sends a client end signal.
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { createHeadlessClient } from '../src/headless/client';
import { getGeminiModels } from '../src/core/config/models';
import { createLiveOpenReason, LIVE_OPEN_TRIGGER } from '../shared/liveOpenReason';
import { captureManagedJourneyBilling, evaluateManagedJourneyBilling, waitForManagedJourneyBillingSettlement } from '../src/headless/managedJourneyBilling';

const sleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));
const client = await createHeadlessClient({ accessMode: 'managed', profileName: 'live-turn-limit-canary' });
const requestId = randomUUID();
const before = await captureManagedJourneyBilling(client, requestId);
if (before.account.account.billingSummary.reservedCredits !== 0) throw new Error('Another request is still reserved.');
const wav = readFileSync('test-fixtures/audio/long-live-generated.wav');
let pcm = Buffer.alloc(0);
for (let offset = 12; offset + 8 <= wav.length;) {
  const length = wav.readUInt32LE(offset + 4);
  if (wav.toString('ascii', offset, offset + 4) === 'fmt ' &&
    (wav.readUInt16LE(offset + 8) !== 1 || wav.readUInt16LE(offset + 10) !== 1 || wav.readUInt32LE(offset + 12) !== 16000 || wav.readUInt16LE(offset + 22) !== 16)) {
    throw new Error('Fixture must be 16 kHz mono PCM16.');
  }
  if (wav.toString('ascii', offset, offset + 4) === 'data') pcm = wav.subarray(offset + 8, offset + 8 + length);
  offset += 8 + length + length % 2;
}
if (!pcm.length) throw new Error('Missing WAV data.');
let handoffAt = 0;
let handoff: unknown;
let complete = false;
let outputTranscript = '';
let outputBytes = 0;
let lastProviderAt = 0;
let providerError = '';
let start = 0;
let sentBytes = 0;
const session = await client.ai.live.connect({
  model: getGeminiModels().audio.conversation,
  liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE, { requestId }),
  config: {
    responseModalities: ['AUDIO'], outputAudioTranscription: {},
    realtimeInputConfig: { automaticActivityDetection: { disabled: true } },
    systemInstruction: 'Listen until the user turn ends. Then respond with exactly: I heard you. This confirms your speaking turn ended. Do not repeat the user speech.',
  },
  callbacks: {
    oninputturnended: (event: unknown) => { handoffAt = Date.now(); handoff = event; },
    onmessage: (value: unknown) => {
      const message = value as any;
      lastProviderAt = Date.now();
      outputTranscript += message.serverContent?.outputTranscription?.text || '';
      for (const part of message.serverContent?.modelTurn?.parts || []) {
        if (part.inlineData?.mimeType?.startsWith('audio/')) outputBytes += Buffer.from(part.inlineData.data, 'base64').length;
      }
      if (message.serverContent?.turnComplete) complete = true;
    },
    onerror: (error: unknown) => { providerError = String(error); },
    onclose: () => { if (!complete) providerError ||= 'Closed before model completion'; },
  },
});
try {
  start = Date.now();
  session.sendRealtimeInput({ activityStart: {} });
  // Offer 65 seconds at microphone pace, including late packets after handoff.
  for (let index = 0; index < 650 && !providerError; index += 1) {
    await sleep(Math.max(0, start + index * 100 - Date.now()));
    const offset = (index * 3200) % pcm.length;
    const frame = Buffer.concat([pcm.subarray(offset), pcm]).subarray(0, 3200);
    session.sendRealtimeInput({ audio: { data: frame.toString('base64'), mimeType: 'audio/pcm;rate=16000' } });
    sentBytes += frame.length;
  }
  while ((!complete || Date.now() - lastProviderAt < 1500) && Date.now() - start < 115000 && !providerError) await sleep(100);
} finally { session.close(); }
const after = await waitForManagedJourneyBillingSettlement(client, requestId, 40, 500);
const billing = evaluateManagedJourneyBilling(before, after);
const rows = after.usageEntries.filter(entry => entry.metadata?.liveOpenRequestId === requestId);
const inputBytes = Number(rows[0]?.metadata?.inputAudioBytes || 0);
const handoffMs = handoffAt - start;
const passed = !providerError && complete && !!outputTranscript && outputBytes > 0
  && handoffMs >= 59000 && handoffMs <= 62000 && inputBytes > 0 && inputBytes <= 60 * 32000
  && rows.length === 1 && Number(rows[0]?.metadata?.shortfallCredits || 0) === 0 && billing.passed;
console.log(JSON.stringify({ passed, requestId, handoffMs, handoff, offeredAudioSeconds: sentBytes / 32000,
  forwardedAudioSeconds: inputBytes / 32000, complete, outputTranscript, outputBytes, providerError, billing }, null, 2));
if (!passed) process.exitCode = 1;
