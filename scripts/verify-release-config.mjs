// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { readFile } from 'node:fs/promises';

const read = path => readFile(new URL(`../${path}`, import.meta.url), 'utf8');
const failures = [];
const requireText = (condition, message) => {
  if (!condition) failures.push(message);
};
const envValue = (text, name) => (
  text.match(new RegExp(`^${name}=(.*)$`, 'm'))?.[1]?.trim() || ''
);

const [
  appPackage,
  functionsPackage,
  functionsIndex,
  functionsGemini,
  androidBuild,
  mainActivity,
  stagingEnv,
  functionsExample,
  headlessClient,
  managedGeminiClient,
] = await Promise.all([
  read('package.json'),
  read('functions/package.json'),
  read('functions/src/index.ts'),
  read('functions/src/gemini.ts'),
  read('android/app/build.gradle'),
  read('android/app/src/main/java/com/ronitervo/maestrotutor/MainActivity.java'),
  read('.env.staging'),
  read('functions/.env.example'),
  read('src/headless/client.ts'),
  read('src/core-sdk/managedGeminiClient.ts'),
]);

const app = JSON.parse(appPackage);
const functions = JSON.parse(functionsPackage);
requireText(app.scripts?.['maestro:rpc'], 'package.json must expose the JSON-RPC harness.');
requireText(!functions.dependencies?.googleapis, 'Functions must not restore the retired Google Play verifier dependency.');
requireText(!functionsIndex.includes('/billing/google-play/verify'), 'Functions must not expose a second purchase grant route.');
requireText(functionsIndex.includes('/gemini/generate-music'), 'Functions must expose managed music through the authenticated backend.');
requireText(functionsGemini.includes("apiVersion: 'v1alpha'"), 'The Lyria backend adapter must use its supported v1alpha WebSocket endpoint.');
requireText(managedGeminiClient.includes('Managed music generation must use the authenticated backend music route.'), 'Managed clients must not mint unsupported ephemeral Lyria tokens.');
requireText(!androidBuild.includes('com.android.billingclient'), 'Android must not ship a second purchase SDK.');
requireText(!mainActivity.includes('ManagedBillingPlugin'), 'Android must not register the retired billing plugin.');

const clientPacks = envValue(stagingEnv, 'VITE_MANAGED_CREDIT_PACK_IDS').split(',').filter(Boolean).sort();
const backendPacks = envValue(functionsExample, 'MANAGED_CREDIT_PACKS')
  .split(',')
  .filter(Boolean)
  .map(entry => entry.split(':')[0])
  .sort();
requireText(clientPacks.length > 0, 'Staging must advertise at least one managed credit pack.');
requireText(JSON.stringify(clientPacks) === JSON.stringify(backendPacks), 'Client and backend example pack ids must match exactly.');
for (const method of [
  'billing.checkout.completeTest',
  'chat.attachment.turn',
  'media.audioNote.generate',
  'media.music.generate',
  'speech.synthetic.live',
]) {
  requireText(headlessClient.includes(`'${method}'`), `Headless contract is missing ${method}.`);
}

if (failures.length) {
  for (const failure of failures) process.stderr.write(`release-config: ${failure}\n`);
  process.exitCode = 1;
} else {
  process.stdout.write(JSON.stringify({
    ok: true,
    activePurchaseProvider: 'stripe',
    creditPackIds: clientPacks,
    headlessCoverage: ['billing', 'chat', 'attachments', 'image', 'audio-note', 'music', 'speech-live'],
  }, null, 2) + '\n');
}
