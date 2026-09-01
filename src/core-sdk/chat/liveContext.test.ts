// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { buildCoreLiveSystemInstruction } from './liveContext';

describe('live context', () => {
  it('serializes adjacent chat turns into the shared Live instruction', () => {
    const prompt = buildCoreLiveSystemInstruction({
      basePrompt: 'Tutor',
      messages: [
        { id: 'u', role: 'user', text: 'Hola', timestamp: 1 },
        { id: 'a', role: 'assistant', rawAssistantResponse: '¡Hola!', timestamp: 2 },
      ],
    });
    expect(prompt).toContain('User: Hola');
    expect(prompt).toContain('Maestro: ¡Hola!');
  });
});
