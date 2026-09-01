// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import type { AudioEnergy } from '../../../shared/audio/speechGate';
import { evaluateFreshSpeechFallback } from './liveSpeechDetection';

const SILENCE: AudioEnergy = { rms: 0, peak: 0, activeRatio: 0 };
const SPEECH: AudioEnergy = { rms: 0.05, peak: 0.4, activeRatio: 0.6 };

describe('energy fallback after Whisper load grace', () => {
  it('expires a candidate that became silent while the model was loading', () => {
    expect(evaluateFreshSpeechFallback(SILENCE, 1_000, 13_000)).toEqual({
      action: 'expire',
      onsetAt: null,
    });
  });

  it('requires a fresh sustained onset before confirming current speech', () => {
    const first = evaluateFreshSpeechFallback(SPEECH, null, 13_000);
    expect(first).toEqual({ action: 'wait', onsetAt: 13_000 });
    expect(evaluateFreshSpeechFallback(SPEECH, first.onsetAt, 13_100).action).toBe('wait');
    expect(evaluateFreshSpeechFallback(SPEECH, first.onsetAt, 13_200)).toEqual({
      action: 'confirm',
      onsetAt: 13_000,
    });
  });

  it('expires a fresh onset if speech stops before it is sustained', () => {
    const first = evaluateFreshSpeechFallback(SPEECH, null, 13_000);
    expect(evaluateFreshSpeechFallback(SILENCE, first.onsetAt, 13_100).action).toBe('expire');
  });
});
