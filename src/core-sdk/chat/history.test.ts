// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { deriveHistoryForApi, sanitizeHistoryWithVerifiedMedia, type DerivedHistoryItem } from './history';
import { ART_STYLE_REFERENCE_MAX_CHARS, ART_STYLE_REFERENCE_TEXT } from './artStyleReference';
import type { ChatMessage } from '../../core/types';

describe('verified media history', () => {
  it('keeps active files and strips deleted, inactive and expired avatar references', async () => {
    const history: DerivedHistoryItem[] = [{
      role: 'user',
      text: 'Remember the available media as text context.',
      fileParts: [
        { fileUri: 'files/active', mimeType: 'image/png' },
        { fileUri: 'files/deleted', mimeType: 'image/png' },
        { fileUri: 'files/processing', mimeType: 'video/mp4' },
      ],
      avatarFileUri: 'files/avatar-expired',
      avatarMimeType: 'image/png',
    }];
    const onStrip = vi.fn();

    const result = await sanitizeHistoryWithVerifiedMedia(history, async uris => {
      expect(uris).toEqual([
        'files/active',
        'files/deleted',
        'files/processing',
        'files/avatar-expired',
      ]);
      return {
        'files/active': { deleted: false, active: true },
        'files/deleted': { deleted: true, active: false },
        'files/processing': { deleted: false, active: false },
        'files/avatar-expired': { deleted: true, active: false },
      };
    }, onStrip);

    expect(result).toEqual([{
      role: 'user',
      text: 'Remember the available media as text context.',
      fileParts: [{ fileUri: 'files/active', mimeType: 'image/png' }],
    }]);
    expect(onStrip).toHaveBeenCalledTimes(3);
  });

  it('does not call the provider for text-only history', async () => {
    const resolver = vi.fn();
    const history: DerivedHistoryItem[] = [{ role: 'assistant', text: 'Text only' }];
    await expect(sanitizeHistoryWithVerifiedMedia(history, resolver)).resolves.toBe(history);
    expect(resolver).not.toHaveBeenCalled();
  });
});

describe('art style reference context', () => {
  const userMessage = { id: 'm1', role: 'user', text: 'Opeta minulle 你好.' } as ChatMessage;

  it('lands in the first user turn behind the profile and summary', () => {
    const [first] = deriveHistoryForApi([userMessage], {
      globalProfileText: 'Likes music.',
      contextSummary: 'Talked about greetings.',
      artStyleReferenceText: ART_STYLE_REFERENCE_TEXT,
    });

    const profileAt = first.text?.indexOf('Learner Profile (global):') ?? -1;
    const summaryAt = first.text?.indexOf('Conversation Summary:') ?? -1;
    const styleAt = first.text?.indexOf('Sketchbook reference') ?? -1;
    expect(profileAt).toBeGreaterThanOrEqual(0);
    expect(summaryAt).toBeGreaterThan(profileAt);
    expect(styleAt).toBeGreaterThan(summaryAt);
    expect(first.text?.endsWith('Opeta minulle 你好.')).toBe(true);
  });

  it('carries no label of its own, so it reads as reference rather than as a rule', () => {
    const [first] = deriveHistoryForApi([userMessage], { artStyleReferenceText: '  Paper and pencil.  ' });
    expect(first.text).toBe(`Paper and pencil.

Opeta minulle 你好.`);
  });

  it('caps an oversized reference and stays absent when none is passed', () => {
    const [capped] = deriveHistoryForApi([userMessage], { artStyleReferenceText: 'x'.repeat(ART_STYLE_REFERENCE_MAX_CHARS + 500) });
    expect(capped.text).toBe(`${'x'.repeat(ART_STYLE_REFERENCE_MAX_CHARS)}

Opeta minulle 你好.`);

    const [untouched] = deriveHistoryForApi([userMessage], {});
    expect(untouched.text).toBe('Opeta minulle 你好.');
  });

  it('keeps the shipped reference within its own budget and free of emoji', () => {
    expect(ART_STYLE_REFERENCE_TEXT.length).toBeLessThanOrEqual(ART_STYLE_REFERENCE_MAX_CHARS);
    expect(ART_STYLE_REFERENCE_TEXT).not.toMatch(/\p{Extended_Pictographic}/u);
  });
});
