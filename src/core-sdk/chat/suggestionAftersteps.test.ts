// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import {
  hasAgentHandoffProposal,
  executeSuggestionToolRequest,
  normalizeSuggestionCreatorArtifact,
  normalizeSuggestionCreatorToolRequest,
} from './suggestionAftersteps';

describe('suggestion creator aftersteps', () => {
  it('normalizes safe artifacts and rejects binary image text', () => {
    expect(normalizeSuggestionCreatorArtifact({
      mimeType: 'text/markdown',
      fileName: 'lesson.md',
      content: '# Lesson',
    })).toMatchObject({ mimeType: 'text/markdown', fileName: 'lesson.md' });
    expect(normalizeSuggestionCreatorArtifact({ mimeType: 'image/png', content: 'not pixels' })).toBeNull();
  });

  it('normalizes all supported tool requests and clamps music duration', () => {
    expect(normalizeSuggestionCreatorToolRequest({ tool: 'image', prompt: 'card' }, 'fallback'))
      .toEqual({ tool: 'image', prompt: 'card' });
    expect(normalizeSuggestionCreatorToolRequest({ tool: 'audio-note', text: 'listen' }, 'fallback'))
      .toEqual({ tool: 'audio-note', text: 'listen' });
    expect(normalizeSuggestionCreatorToolRequest({ tool: 'music', prompt: 'scale', durationSeconds: 99 }, 'fallback'))
      .toEqual({ tool: 'music', prompt: 'scale', durationSeconds: 20 });
  });

  it('dispatches through the shared afterstep boundary', async () => {
    const handlers = {
      image: vi.fn(async () => 'image'),
      audioNote: vi.fn(async () => 'audio'),
      music: vi.fn(async () => 'music'),
    };
    await expect(executeSuggestionToolRequest({ tool: 'music', prompt: 'scale' }, handlers)).resolves.toBe('music');
    expect(handlers.music).toHaveBeenCalledOnce();
  });
});

it('keeps the handoff capability-gated and never accepts a rewritten request', async () => {
  expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent' }, 'short fallback')).toBeNull();
  expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent' }, 'short fallback', { allowAgent: true })).toEqual({ tool: 'agent' });
  expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent', prompt: 'different request' }, '', { allowAgent: true })).toBeNull();
  expect(hasAgentHandoffProposal('Reply.\n```maestro-tool {"tool":"agent"}```')).toBe(true);
  expect(hasAgentHandoffProposal('```maestro-tool {"tool":"agent","alreadyUsed":true}```')).toBe(false);
  const handlers = { image: vi.fn(), audioNote: vi.fn(), music: vi.fn() };
  await expect(executeSuggestionToolRequest({ tool: 'agent' }, handlers)).rejects.toThrow('unavailable');
  expect(handlers.music).not.toHaveBeenCalled();
});

it('accepts only host-listed task control and rejects rewritten prompts or stale targets', () => {
  const target = { id: 'task', phase: 'working' as const, requestPreview: 'Make a robot', replyPreview: '', running: true };
  const options = { allowAgent: true, agentTargets: [target] };
  expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent', task: { action: 'stop', taskId: 'task' } }, '', options)).toEqual({ tool: 'agent', task: { action: 'stop', taskId: 'task' } });
  for (const task of [{ action: 'stop', taskId: 'other' }, { action: 'continue', taskId: 'task' }, { action: 'revise', taskId: 'task', prompt: 'rewritten' }])
    expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent', task }, '', options)).toBeNull();
  expect(normalizeSuggestionCreatorToolRequest({ tool: 'agent', task: { action: 'stop', taskId: 'task' } }, '', { allowAgent: true })).toBeNull();
});
