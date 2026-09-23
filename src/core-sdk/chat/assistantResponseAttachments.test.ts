// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { parseAssistantResponseForAttachment } from './assistantResponseAttachments';

describe('inline artifact fences', () => {
  it.each(['```', '~~~', '````'])(
    'extracts a %s fence without consuming surrounding language pairs',
    fence => {
      const before = 'Bonjour\n[en] Hello';
      const after = 'Merci\n[en] Thank you';
      const body = '<svg viewBox="0 0 100 100"><text>Hidden artifact text</text></svg>';
      const parsed = parseAssistantResponseForAttachment(`${before}${fence}svg\n${body}\n${fence}\n${after}`);
      expect(parsed.cleanedText).toBe(`${before}\n\n${after}`);
      expect(parsed.attachment?.mimeType).toBe('image/svg+xml');
      expect(atob(parsed.attachment!.dataUrl.split(',')[1])).toBe(body);
    },
  );

  it('does not treat fences inside the artifact body as another opening fence', () => {
    const body = 'const example = "```nested";';
    const parsed = parseAssistantResponseForAttachment(`Bonjour\n[en] Hello\`\`\`javascript\n${body}\n\`\`\``);
    expect(parsed.cleanedText).toBe('Bonjour\n[en] Hello');
    expect(atob(parsed.attachment!.dataUrl.split(',')[1])).toBe(body);
  });

  it('keeps backticks inside raw HTML scripts in their HTML attachment', () => {
    const body = '<html><body><script>\nconst example = "```nested";\n</script></body></html>';
    const parsed = parseAssistantResponseForAttachment(`Bonjour\n[en] Hello\n${body}`);
    expect(parsed.cleanedText).toBe('Bonjour\n[en] Hello');
    expect(parsed.attachment?.mimeType).toBe('text/html');
    expect(atob(parsed.attachment!.dataUrl.split(',')[1])).toContain('const example = "```nested";');
  });
});
