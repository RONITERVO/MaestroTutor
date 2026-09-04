// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  ContinuousLiveTurnBoundary,
  LIVE_SPEECH_IDLE_MS,
  LIVE_SPEECH_POST_ROLL_MS,
} from './continuousLiveTurnBoundary';

describe('ContinuousLiveTurnBoundary', () => {
  it('keeps one continuous turn open through the full idle and post-roll tail', () => {
    const boundary = new ContinuousLiveTurnBoundary();
    const openedAt = 1_000;

    expect(boundary.openFromConfirmedSpeech(openedAt)).toBe(true);
    expect(boundary.shouldBeginClosing(
      openedAt + LIVE_SPEECH_IDLE_MS + LIVE_SPEECH_POST_ROLL_MS - 1,
    )).toBe(false);
    expect(boundary.shouldBeginClosing(
      openedAt + LIVE_SPEECH_IDLE_MS + LIVE_SPEECH_POST_ROLL_MS,
    )).toBe(true);
  });

  it('extends the same turn instead of making another boundary for later speech', () => {
    const boundary = new ContinuousLiveTurnBoundary({ idleMs: 2_500, postRollMs: 1_500 });
    boundary.openFromConfirmedSpeech(1_000);
    boundary.refreshConfirmedSpeech(3_000);

    expect(boundary.shouldBeginClosing(5_000)).toBe(false);
    expect(boundary.shouldBeginClosing(7_000)).toBe(true);
    expect(boundary.currentState).toBe('open');
  });

  it('does not reopen until the ordered close has finished', () => {
    const boundary = new ContinuousLiveTurnBoundary({ idleMs: 100, postRollMs: 50 });
    boundary.openFromConfirmedSpeech(0);

    expect(boundary.beginClosing(150)).toBe(true);
    expect(boundary.isClosing).toBe(true);
    expect(boundary.openFromConfirmedSpeech(151)).toBe(false);

    boundary.finishClosing();
    expect(boundary.openFromConfirmedSpeech(152)).toBe(true);
  });
});
