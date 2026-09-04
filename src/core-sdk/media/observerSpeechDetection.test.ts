// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  isLikelySpeechTranscript,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
  SemanticSpeechCapture,
  SpeechActivityTracker,
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

  it('allows a short utterance once the intact audio window is long enough', () => {
    const silence = new Int16Array(RATE);
    const shortSpeech = new Int16Array(RATE / 5).fill(10_000);
    const audio = pcmPacketsToWhisperWindow([silence, shortSpeech], RATE);

    expect(audio).not.toBeNull();
    expect(audio).toHaveLength(RATE * 1.2);
    expect(audio?.slice(0, RATE).every(sample => sample === 0)).toBe(true);
  });
});

describe('semantic speech capture', () => {
  it('protects the complete candidate and inference tail until accepted', () => {
    const capture = new SemanticSpeechCapture({ sampleRate: 1_000, preRollMs: 1_200 });
    capture.append(new Int16Array(1_000));
    capture.append(new Int16Array(200).fill(8_000));

    expect(capture.beginWhisperCheck(1_000)).not.toBeNull();
    capture.append(new Int16Array(2_000).fill(9_000));

    const confirmed = capture.takeConfirmedPackets().flatMap(packet => [...packet]);
    expect(confirmed).toHaveLength(3_200);
    expect(confirmed.slice(-2_000).every(sample => sample === 9_000)).toBe(true);
    expect(capture.sampleCount).toBe(0);
    expect(capture.isWhisperCheckPending).toBe(false);
  });

  it('returns to bounded rolling pre-roll after a rejected check', () => {
    const capture = new SemanticSpeechCapture({ sampleRate: 1_000, preRollMs: 1_200 });
    capture.append(new Int16Array(1_200).fill(8_000));
    expect(capture.beginWhisperCheck(1_000)).not.toBeNull();
    capture.append(new Int16Array(2_000).fill(9_000));

    capture.finishWhisperCheck();

    expect(capture.sampleCount).toBe(1_200);
    expect(capture.takeConfirmedPackets().flatMap(packet => [...packet])
      .every(sample => sample === 9_000)).toBe(true);
  });
});

describe('minimum live speech duration', () => {
  const RATE = 16_000;

  it('counts VAD-active samples instead of silence in the Whisper window', () => {
    const tracker = new SpeechActivityTracker({ sampleRate: RATE });

    for (let index = 0; index < 11; index += 1) {
      expect(tracker.observe(1_600, true, index * 100).hasMinimumSpeech).toBe(false);
    }
    expect(tracker.observe(16_000, false, 1_150).hasMinimumSpeech).toBe(false);
    expect(tracker.observe(1_600, true, 1_200).hasMinimumSpeech).toBe(true);
  });

  it('expires a short candidate instead of combining separate utterances', () => {
    const tracker = new SpeechActivityTracker({ sampleRate: RATE });
    tracker.observe(4_800, true, 100);

    const reset = tracker.observe(1_600, false, 1_700);
    expect(reset.candidateReset).toBe(true);
    expect(reset.hasMinimumSpeech).toBe(false);

    for (let index = 0; index < 9; index += 1) {
      expect(tracker.observe(1_600, true, 1_800 + index * 100).hasMinimumSpeech).toBe(false);
    }
  });
});
