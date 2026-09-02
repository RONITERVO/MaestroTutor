// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  LIVE_OPEN_TRIGGER,
  createLiveOpenReason,
  getLiveOpenOrigin,
  parseLiveOpenReason,
  requireLiveOpenReason,
} from './liveOpenReason';

describe('Gemini Live open reasons', () => {
  it('creates a normalized auditable reason for every supported trigger', () => {
    const now = new Date('2026-09-02T12:34:56.000Z');

    for (const trigger of Object.values(LIVE_OPEN_TRIGGER)) {
      expect(createLiveOpenReason(trigger, {
        requestId: `test-${trigger.replaceAll('.', '-')}`,
        now,
      })).toEqual({
        trigger,
        requestId: `test-${trigger.replaceAll('.', '-')}`,
        requestedAt: now.toISOString(),
      });
    }
  });

  it('derives only the four reviewed origins', () => {
    expect(getLiveOpenOrigin(LIVE_OPEN_TRIGGER.WHISPER_OBSERVER)).toBe('whisper');
    expect(getLiveOpenOrigin(LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE)).toBe('user');
    expect(getLiveOpenOrigin(LIVE_OPEN_TRIGGER.TOOL_AUDIO_NOTE)).toBe('tool');
    expect(getLiveOpenOrigin(LIVE_OPEN_TRIGGER.VOICE_TTS_CLICK)).toBe('voice');
  });

  it('normalizes valid timestamps and rejects unreviewed or malformed input', () => {
    expect(parseLiveOpenReason({
      trigger: LIVE_OPEN_TRIGGER.WHISPER_STT,
      requestId: 'request-1234',
      requestedAt: '2026-09-02T12:34:56+00:00',
    })).toEqual({
      trigger: LIVE_OPEN_TRIGGER.WHISPER_STT,
      requestId: 'request-1234',
      requestedAt: '2026-09-02T12:34:56.000Z',
    });
    expect(parseLiveOpenReason({
      trigger: 'user.some-new-button',
      requestId: 'request-1234',
      requestedAt: '2026-09-02T12:34:56.000Z',
    })).toBeNull();
    expect(parseLiveOpenReason({
      trigger: LIVE_OPEN_TRIGGER.WHISPER_STT,
      requestId: 'short',
      requestedAt: 'not-a-date',
    })).toBeNull();
    expect(parseLiveOpenReason({
      trigger: LIVE_OPEN_TRIGGER.WHISPER_STT,
      requestId: 'request-1234',
      requestedAt: '0',
    })).toBeNull();
    expect(() => requireLiveOpenReason(undefined)).toThrow(/auditable Gemini Live open reason/);
  });

  it('falls back to an independent audit ID when a caller ID is unsafe', () => {
    const reason = createLiveOpenReason(LIVE_OPEN_TRIGGER.TOOL_AUDIO_NOTE, {
      requestId: 'bad',
      now: new Date('2026-09-02T12:34:56.000Z'),
    });

    expect(reason.requestId).toMatch(/^live-/);
    expect(reason.requestId).not.toBe('bad');
  });
});
