// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { readFileSync } from 'node:fs';

const argument = (name: string): string => {
  const index = process.argv.indexOf(name);
  const value = index >= 0 ? process.argv[index + 1]?.trim() : '';
  if (!value) throw new Error(`Missing ${name}.`);
  return value;
};

const resultPath = argument('--result');
const paramsPath = argument('--params');
const access = argument('--access');
if (access !== 'managed' && access !== 'byok') {
  throw new Error('--access must be managed or byok.');
}

const result = JSON.parse(readFileSync(resultPath, 'utf8')) as any;
const params = JSON.parse(readFileSync(paramsPath, 'utf8')) as any;
const expectedPairs = Number(params?.fixture?.expectedTranslationPairs || 5);
const inputDurationSeconds = Number(params?.fixture?.inputDurationSeconds || 0);
const outputTranscript = String(result?.outputTranscript || '');
const taggedTranslationPairs = (outputTranscript.match(/\[(?:FI|fi-FI)\]/g) || []).length;
const parsedTranslationPairs = Array.isArray(result?.assistantMessage?.translations)
  ? result.assistantMessage.translations.length
  : 0;
const translatedPairs = Math.max(taggedTranslationPairs, parsedTranslationPairs);
const spokenLineCount = outputTranscript.trim().split(/\n+/).filter(Boolean).length;
const outputAudioRatio = Number(result?.timing?.modelAudioDurationMs || 0)
  / Math.max(1, inputDurationSeconds * 1_000);
const payerPassed = access === 'managed'
  ? result?.managedBillingEvidence?.passed === true
  : result?.managedBillingEvidence?.payer === 'byok-api-key-owner';
const passed = result?.transcriptEvidence?.passed === true
  && payerPassed
  && result?.connectedTurnCount === 1
  && result?.sentVideoFrameCount === 1
  && translatedPairs >= expectedPairs
  && spokenLineCount >= expectedPairs * 2
  && result?.turns?.[0]?.playbackCompletedAfterLastByte === true
  && outputAudioRatio >= 1.5
  && outputAudioRatio <= 5;

const evidence = {
  transcript: result?.transcriptEvidence,
  billing: result?.managedBillingEvidence,
  translatedPairs,
  taggedTranslationPairs,
  parsedTranslationPairs,
  expectedPairs,
  spokenLineCount,
  outputAudioRatio,
  timing: result?.timing,
  turn: result?.turns?.[0],
};
if (!passed) throw new Error(`Incomplete ${access} long Live proof: ${JSON.stringify(evidence)}`);
console.log(`${access} long Live proof`, JSON.stringify(evidence));
