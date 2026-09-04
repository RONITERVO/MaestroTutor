// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { decodePcm16LeBase64 } from '../src/core-sdk/media/pcmInput';
import { TRIGGER_AUDIO_PCM_24K } from '../src/core-sdk/media/triggerAudioAsset';

const source = decodePcm16LeBase64(TRIGGER_AUDIO_PCM_24K);
const speech = new Int16Array(Math.floor(source.length * 2 / 3));
for (let index = 0; index < speech.length; index += 1) {
  speech[index] = source[Math.min(source.length - 1, Math.floor(index * 3 / 2))];
}

// The 625 ms prefix starts the second turn outside the speaker-settle window.
// The one-second suffix gives Live an unambiguous boundary while the spoken
// content itself stays the short, bundled "Play" recording.
const pcm = new Int16Array(10_000 + speech.length + 16_000);
pcm.set(speech, 10_000);

process.stdout.write(JSON.stringify({
  pcmBase64: Buffer.from(pcm.buffer, pcm.byteOffset, pcm.byteLength).toString('base64'),
  sampleRate: 16_000,
  pace: true,
  connectedTurns: 2,
  gateInputOnSpeech: true,
  semanticSpeech: true,
  simulateUiSpeechHandoff: true,
  requireRealtimeInputPacing: true,
  playModelAudioRealtime: true,
  timeoutMs: 60_000,
  systemInstruction: 'Reply briefly to every user turn. The user says the single word Play.',
}));
