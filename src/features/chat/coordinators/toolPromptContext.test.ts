// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { truncateForToolPrompt } from './toolPromptContext';

describe('tool prompt context boundaries', () => {
  it('keeps established whitespace and ASCII truncation', () => {
    expect(truncateForToolPrompt('  hello\nworld  ')).toBe('hello world');
    expect(truncateForToolPrompt('hello world', 8)).toBe('hello w…');
    expect(truncateForToolPrompt('hello 🎵', 8)).toBe('hello 🎵');
  });
  it('does not split a surrogate pair at the truncation boundary', () => {
    expect(truncateForToolPrompt('hello 🎵 world', 8)).toBe('hello …');
    expect(truncateForToolPrompt('hello 🎵 world', 9)).toBe('hello 🎵…');
  });
});
