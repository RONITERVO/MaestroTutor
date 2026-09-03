// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { resolveSttTurnDestination } from './sttTurnRouting';

describe('STT turn routing', () => {
  it('keeps an utterance bound to translation after async transcription', () => {
    expect(resolveSttTurnDestination('translation', false)).toBe('translation');
  });

  it('routes to translation when the Translate view opened after listening started', () => {
    expect(resolveSttTurnDestination('message', true)).toBe('translation');
  });

  it('uses normal chat only when neither signal selects translation', () => {
    expect(resolveSttTurnDestination('message', false)).toBe('message');
    expect(resolveSttTurnDestination(undefined, false)).toBe('message');
  });
});
