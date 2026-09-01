// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import {
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
