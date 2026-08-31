// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  isLikelySpeechTranscript,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
} from './observerSpeechDetection';

describe('Whisper trigger transcript filtering', () => {
  it.each([
    '',
    ' ',
    '[MUSIC]',
    '(silence)',
    '[BLANK_AUDIO]',
    'You.',
    'Thank you!',
    'Thanks for watching.',
    'Subtitles',
  ])('rejects stock non-speech output %j', (text) => {
    expect(isLikelySpeechTranscript(text)).toBe(false);
  });

  it.each([
    'Can you help me?',
    'Kiitos paljon',
    '¿Cómo estás?',
    'ありがとう',
    'music is my favorite word',
  ])('accepts a real utterance %j', (text) => {
    expect(isLikelySpeechTranscript(text)).toBe(true);
  });
});

describe('Whisper audio windows', () => {
  const RATE = 16_000;

  it('keeps only the newest requested samples without reordering them', () => {
    const packets = [
      new Int16Array([1, 2, 3]),
      new Int16Array([4, 5, 6]),
      new Int16Array([7, 8, 9]),
    ];
    expect(recentPcmPackets(packets, 5).flatMap(packet => [...packet])).toEqual([5, 6, 7, 8, 9]);
  });

  it('does not invoke Whisper before the minimum window exists', () => {
    const tooShort = new Int16Array(RATE);
    tooShort.fill(10_000);
    expect(pcmPacketsToWhisperWindow([tooShort], RATE)).toBeNull();
  });

  it('does not invoke Whisper for energetic-looking digital silence', () => {
    const silence = new Int16Array(RATE * 2);
    expect(pcmPacketsToWhisperWindow([silence], RATE)).toBeNull();
  });

  it('normalizes a speech-like PCM window for the worker', () => {
    const speech = new Int16Array(RATE * 2);
    for (let index = 0; index < speech.length; index += 1) {
      speech[index] = Math.round(Math.sin(index / 20) * 10_000);
    }
    const audio = pcmPacketsToWhisperWindow([speech], RATE);
    expect(audio).not.toBeNull();
    expect(audio?.length).toBe(speech.length);
    expect(Math.max(...audio!.slice(0, 100))).toBeLessThanOrEqual(1);
  });
});
