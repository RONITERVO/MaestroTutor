// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it, vi } from 'vitest';
import { isSilentObserverActivityToken } from '../../../core/config/activityTokens';
import { createLiveActivity } from './activity';
import { createLiveSessionState } from './state';

it.each([true, false])('assigns preconnect speech phases to their owning mode: observer=%s', observer => {
  const tokens = new Set<string>();
  const activity = createLiveActivity(createLiveSessionState({}), {
    setState: vi.fn(), addActivityToken: (category, subtype) => { const token = `${category}:${subtype}`; tokens.add(token); return token; },
    removeActivityToken: token => { tokens.delete(token); },
  });
  for (const phase of ['whisper-loading', 'whisper-checking', 'speech-confirmed', 'vad-listening'] as const) {
    activity.setLocalSpeechTriggerPhase(phase, observer);
    expect(tokens.size).toBe(1);
    expect(isSilentObserverActivityToken([...tokens][0])).toBe(observer);
  }
  activity.setLocalSpeechTriggerPhase(null);
  expect(tokens.size).toBe(0);
});
