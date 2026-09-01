// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { buildLiveSttSystemInstruction, LIVE_STT_BASE_INSTRUCTION } from './liveSessionInstructions';

describe('Live session instructions', () => {
  it('keeps the STT parrot contract and adds current lesson context', () => {
    expect(buildLiveSttSystemInstruction()).toBe(LIVE_STT_BASE_INSTRUCTION);
    expect(buildLiveSttSystemInstruction({ lastAssistantMessage: 'Hola', replySuggestions: ['Sí'] }))
      .toContain('1. Sí');
  });
});
