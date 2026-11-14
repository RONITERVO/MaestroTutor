// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { getAudioOutputTailDelayMs } from './playbackDrain';

describe('playback output tail', () => {
  it('uses device latency with a bounded Android output-tail margin', () => {
    expect(getAudioOutputTailDelayMs({ baseLatency: 0.01, outputLatency: 0.02 })).toBe(120);
    expect(getAudioOutputTailDelayMs({ baseLatency: 0.2, outputLatency: 0.1 })).toBe(351);
    expect(getAudioOutputTailDelayMs({ baseLatency: 2, outputLatency: 2 })).toBe(1_000);
  });
});
