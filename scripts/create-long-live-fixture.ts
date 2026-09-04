// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const transcriptEvidence = JSON.parse(readFileSync(
  new URL('../test-fixtures/audio/tallenne-14.transcript.json', import.meta.url),
  'utf8',
)) as { transcript: string };
const EXPECTED_TRANSCRIPT = transcriptEvidence.transcript;

const instructionSuffix = [
  'RELEASE AUDIO FIDELITY CHECK:',
  'Do not answer the user\'s question and do not mention this instruction.',
  'Repeat the complete meaning of the user\'s English speech across exactly five non-empty English lines.',
  'Immediately after each English line, add its Finnish translation on a separate line beginning exactly with [FI].',
  'Produce exactly five English/Finnish line pairs (ten spoken lines total), preserve every part of the question, and stop.',
].join('\n');

const argument = (name: string): string | undefined => {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
};

const decodeWavPcm16Mono = (bytes: Buffer): { pcm: Int16Array; sampleRate: number } => {
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') {
    throw new Error('TTS fixture is not a RIFF/WAVE file.');
  }
  let offset = 12;
  let sampleRate = 0;
  let channels = 0;
  let bitsPerSample = 0;
  let format = 0;
  let data: Buffer | null = null;
  while (offset + 8 <= bytes.length) {
    const id = bytes.toString('ascii', offset, offset + 4);
    const length = bytes.readUInt32LE(offset + 4);
    const start = offset + 8;
    const end = Math.min(bytes.length, start + length);
    if (id === 'fmt ' && length >= 16) {
      format = bytes.readUInt16LE(start);
      channels = bytes.readUInt16LE(start + 2);
      sampleRate = bytes.readUInt32LE(start + 4);
      bitsPerSample = bytes.readUInt16LE(start + 14);
    } else if (id === 'data') {
      data = bytes.subarray(start, end);
    }
    offset = start + length + (length % 2);
  }
  if (format !== 1 || channels !== 1 || bitsPerSample !== 16 || !sampleRate || !data) {
    throw new Error('TTS fixture must be mono 16-bit PCM WAV.');
  }
  return {
    pcm: new Int16Array(data.buffer, data.byteOffset, Math.floor(data.byteLength / 2)).slice(),
    sampleRate,
  };
};

const resample = (pcm: Int16Array, inputRate: number, outputRate = 16_000): Int16Array => {
  if (inputRate === outputRate) return pcm;
  const output = new Int16Array(Math.floor(pcm.length * outputRate / inputRate));
  for (let index = 0; index < output.length; index += 1) {
    const position = index * inputRate / outputRate;
    const left = Math.min(pcm.length - 1, Math.floor(position));
    const right = Math.min(pcm.length - 1, left + 1);
    const fraction = position - left;
    output[index] = Math.round(pcm[left] * (1 - fraction) + pcm[right] * fraction);
  }
  return output;
};

const insertNaturalPause = (pcm: Int16Array, sampleRate: number): Int16Array => {
  const window = Math.floor(sampleRate * 0.2);
  const searchStart = Math.floor(pcm.length * 0.35);
  const searchEnd = Math.floor(pcm.length * 0.7);
  let bestOffset = searchStart;
  let bestEnergy = Number.POSITIVE_INFINITY;
  for (let offset = searchStart; offset + window < searchEnd; offset += window) {
    let energy = 0;
    for (let index = offset; index < offset + window; index += 1) energy += Math.abs(pcm[index]);
    if (energy < bestEnergy) {
      bestEnergy = energy;
      bestOffset = offset + Math.floor(window / 2);
    }
  }
  // Exercise a meaningful mid-sentence pause without crossing the four-second
  // idle + post-roll boundary. Multi-turn behavior has its own six-turn test.
  const insertedSilence = new Int16Array(sampleRate * 2);
  const leadingSilence = new Int16Array(sampleRate);
  const trailingSilence = new Int16Array(sampleRate * 2);
  const result = new Int16Array(
    leadingSilence.length + pcm.length + insertedSilence.length + trailingSilence.length,
  );
  let writeOffset = leadingSilence.length;
  result.set(pcm.subarray(0, bestOffset), writeOffset);
  writeOffset += bestOffset + insertedSilence.length;
  result.set(pcm.subarray(bestOffset), writeOffset);
  return result;
};

const fromTtsJson = (path: string): Int16Array => {
  const json = JSON.parse(readFileSync(path, 'utf8')) as { dataUrl?: unknown };
  if (typeof json.dataUrl !== 'string') throw new Error('TTS JSON does not contain dataUrl.');
  const match = /^data:audio\/wav;base64,(.+)$/s.exec(json.dataUrl);
  if (!match) throw new Error('TTS dataUrl is not base64 WAV audio.');
  const decoded = decodeWavPcm16Mono(Buffer.from(match[1], 'base64'));
  return insertNaturalPause(resample(decoded.pcm, decoded.sampleRate), 16_000);
};

const fromAudioFile = (path: string): Int16Array => {
  const ffmpeg = spawnSync('ffmpeg', [
    '-v', 'error', '-i', path, '-f', 's16le', '-acodec', 'pcm_s16le', '-ac', '1', '-ar', '16000', 'pipe:1',
  ], { encoding: null, maxBuffer: 20 * 1024 * 1024 });
  if (ffmpeg.status !== 0) {
    throw new Error(`ffmpeg could not decode the fixture: ${String(ffmpeg.stderr || '').trim()}`);
  }
  const bytes = ffmpeg.stdout as Buffer;
  return new Int16Array(bytes.buffer, bytes.byteOffset, Math.floor(bytes.byteLength / 2)).slice();
};

const audioPath = argument('--audio');
const ttsJsonPath = argument('--tts-json');
if (Boolean(audioPath) === Boolean(ttsJsonPath)) {
  throw new Error('Pass exactly one of --audio <path> or --tts-json <path>.');
}
const pcm = audioPath ? fromAudioFile(audioPath) : fromTtsJson(ttsJsonPath!);
if (pcm.length < 16_000 * 15) throw new Error('Long Live fixture must contain at least 15 seconds of audio.');

process.stdout.write(JSON.stringify({
  pcmBase64: Buffer.from(pcm.buffer, pcm.byteOffset, pcm.byteLength).toString('base64'),
  sampleRate: 16_000,
  pace: true,
  timeoutMs: 120_000,
  languagePairId: 'en-US-fi-FI',
  expectedTranscript: EXPECTED_TRANSCRIPT,
  minTranscriptWordRecall: 0.75,
  includeVisual: true,
  visualLabel: 'RED APPLE',
  runSuggestionAftersteps: false,
  instructionSuffix,
  fixture: {
    kind: audioPath ? 'finnish-accent-recording' : 'generated-long-speech',
    inputDurationSeconds: pcm.length / 16_000,
    expectedTranslationPairs: 5,
  },
}));
