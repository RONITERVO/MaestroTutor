// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { formatStreamingTutorDraftText, parseStrictTutorResponseText } from './tutorResponse';

describe('tutor response parsing', () => {
  it('parses paired target and native-language lines', () => {
    expect(parseStrictTutorResponseText('Bonjour\n[en] Hello\nMerci\n[en] Thank you', 'en')).toEqual({
      translations: [
        { target: 'Bonjour', native: 'Hello' },
        { target: 'Merci', native: 'Thank you' },
      ],
      visibleText: 'Bonjour\n[EN] Hello\nMerci\n[EN] Thank you',
      hasSkippedNonLanguageContent: false,
    });
  });

  it('removes fenced artifact output instead of leaking it into the conversation', () => {
    const parsed = parseStrictTutorResponseText(
      'Bonjour\n[en] Hello\n```html\n<div>hidden</div>\n```',
      'en',
    );
    expect(parsed.translations).toEqual([{ target: 'Bonjour', native: 'Hello' }]);
    expect(parsed.visibleText).toBe('Bonjour\n[EN] Hello');
    expect(parsed.hasSkippedNonLanguageContent).toBe(true);
  });

  it('shows an incomplete target line while streaming without inventing a translation', () => {
    expect(formatStreamingTutorDraftText('Bonjour', 'en')).toBe('Bonjour');
    expect(formatStreamingTutorDraftText('Bonjour\n[en] Hello', 'en')).toBe('Bonjour\n[EN] Hello');
  });
});
