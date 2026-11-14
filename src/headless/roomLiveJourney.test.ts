// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { isSpokenRoomHandoff } from './roomLiveJourney';
import { buildRoomTaskCatalogue } from '../../shared/prompts';

describe('spoken room handoff evidence', () => {
  it.each([undefined, '', '```maestro-tool\n{"tool":"agent"}\n```', '{"tool": "agent"}', 'Here is maestro-tool.'])('rejects empty or machine-formatted output %s', text => {
    expect(isSpokenRoomHandoff(text)).toBe(false);
  });
  it('accepts a natural bilingual handoff with ordinary speech tags', () => {
    expect(isSpokenRoomHandoff('[happy] Le pediré al agente que lo cree.\n[EN] I will ask the agent to create it.')).toBe(true);
  });
  it('retains the text tool envelope while giving Live a speech-only instruction', () => {
    const targets = [{ id: 'task1', label: 'Ball', phase: 'running' }];
    expect(buildRoomTaskCatalogue(targets)).toContain('Propose the same {"tool":"agent"}');
    expect(buildRoomTaskCatalogue(targets, 'live')).toContain('Propose the handoff in natural speech only.');
    expect(buildRoomTaskCatalogue(targets, 'live')).toContain(JSON.stringify(targets));
  });
});
