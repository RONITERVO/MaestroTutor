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

  const visibleReply = 'Bonjour\n[en] Hello\nMerci\n[en] Thank you';
  const expectedVisibleReply = 'Bonjour\n[EN] Hello\nMerci\n[EN] Thank you';
  const expectedTranslations = [
    { target: 'Bonjour', native: 'Hello' },
    { target: 'Merci', native: 'Thank you' },
  ];

  it.each([
    ['SVG', '<svg viewBox="0 0 100 100">\n<text>Hidden artifact text</text>\n</svg>'],
    ['HTML', '<html>\n<body>\nHidden artifact text\n</body>\n</html>'],
    ['HTML declaration', '<!DOCTYPE html>\n<html><body>Hidden artifact text</body></html>'],
    ['XML declaration', '<?xml version="1.0"?>\n<svg><text>Hidden artifact text</text></svg>'],
    ['comment', '<!--\nHidden artifact text\n-->'],
    ['backtick fence', '```svg\n<svg viewBox="0 0 100 100"><text>Hidden</text></svg>\n```'],
    ['tilde fence', '~~~html\n<div>Hidden artifact text</div>\n~~~'],
    ['code fence', '```javascript\nconst greeting = "hidden";\n```'],
    ['multiline opening tag', '<svg\nviewBox="0 0 100 100"\n>\n<text>Hidden</text>\n</svg>'],
  ])('preserves the last translation throughout an inline %s artifact', (_name, artifact) => {
    for (let end = 0; end <= artifact.length; end++) {
      const response = visibleReply + artifact.slice(0, end);
      // A partial delimiter may briefly remain visible, but neither language
      // line may disappear at any point while the artifact streams in.
      expect(formatStreamingTutorDraftText(response, 'en'), `stream prefix ${end}`)
        .toContain(expectedVisibleReply);
      expect(parseStrictTutorResponseText(response, 'en').visibleText, `completed prefix ${end}`)
        .toContain(expectedVisibleReply);
    }

    const response = visibleReply + artifact;
    expect(formatStreamingTutorDraftText(response, 'en')).toBe(expectedVisibleReply);
    expect(parseStrictTutorResponseText(response, 'en')).toEqual({
      translations: expectedTranslations,
      visibleText: expectedVisibleReply,
      hasSkippedNonLanguageContent: true,
    });
  });

  it('preserves the last pair even when an inline artifact is unfinished', () => {
    const response = visibleReply + '<svg>\n<text>Hidden artifact text</text>';
    expect(formatStreamingTutorDraftText(response, 'en')).toBe(expectedVisibleReply);
    expect(parseStrictTutorResponseText(response, 'en').translations).toEqual(expectedTranslations);
  });

  it('handles whitespace before an inline artifact and Windows line endings', () => {
    const response = visibleReply.replace(/\n/g, '\r\n') + '  <svg>\r\n<text>Hidden</text>\r\n</svg>';
    expect(formatStreamingTutorDraftText(response, 'en')).toBe(expectedVisibleReply);
    expect(parseStrictTutorResponseText(response, 'en').translations).toEqual(expectedTranslations);
  });

  it('resumes language pairs after an inline artifact closes', () => {
    const response = visibleReply + '```javascript\nconst hidden = true;\n```\nAu revoir\n[en] Goodbye';
    const expected = expectedVisibleReply + '\nAu revoir\n[EN] Goodbye';
    expect(formatStreamingTutorDraftText(response, 'en')).toBe(expected);
    expect(parseStrictTutorResponseText(response, 'en').visibleText).toBe(expected);
  });

  it('keeps punctuation, comparisons and inline code in ordinary translations', () => {
    const response = 'Un exemple\n[en] Use `hello`, [brackets], ~waves~ & 1 < 2 or x <alpha!\nMerci\n[en] Thank you';
    expect(formatStreamingTutorDraftText(response, 'en')).toBe(response.replace(/\[en\]/g, '[EN]'));
    expect(parseStrictTutorResponseText(response, 'en').translations).toEqual([
      { target: 'Un exemple', native: 'Use `hello`, [brackets], ~waves~ & 1 < 2 or x <alpha!' },
      { target: 'Merci', native: 'Thank you' },
    ]);
  });
});
