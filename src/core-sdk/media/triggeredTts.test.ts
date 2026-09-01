// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { buildTriggeredTtsSystemInstruction } from './triggeredTts';

describe('triggered TTS instruction', () => {
  it('preserves the Play trigger and exact text contract', () => {
    const instruction = buildTriggeredTtsSystemInstruction([{ text: 'Hola', langCode: 'es' }]);
    expect(instruction).toContain('when the user says "Play"');
    expect(instruction).toContain('[es] Hola');
  });
});
