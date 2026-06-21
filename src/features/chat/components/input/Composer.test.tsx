// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import React from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import Composer from './Composer';

afterEach(cleanup);

const t = (key: string, replacements?: Record<string, string | number>): string => {
  const values: Record<string, string> = {
    'chat.messageInputAriaLabel': 'Message input',
    'chat.suggestion.practiceActive': `Practicing ${replacements?.suggestion ?? ''}`,
    'chat.suggestion.practiceComplete': 'Practice complete',
    'chat.suggestion.practiceHint': 'Practice hint',
    'chat.suggestion.cancelPractice': 'Cancel practice',
  };
  return values[key] ?? key;
};

const renderComposer = (inputText: string) => render(
  <Composer
    t={t}
    inputText={inputText}
    suggestionPracticeTarget="Hello"
    placeholder="Write a message"
    isDisabled={false}
    isDrawDisabled={false}
    onChange={vi.fn()}
    onKeyDown={vi.fn()}
    onPaste={vi.fn()}
    onOpenDrawCanvas={vi.fn()}
    onCancelSuggestionPractice={vi.fn()}
    bubbleTextAreaRef={React.createRef<HTMLTextAreaElement>()}
    prepDisplay={null}
    drawCanvasLabel="Trace next character"
    drawButtonClassName=""
  />
);

describe('Composer suggestion practice', () => {
  it('renders an accessible composer with the matching suggestion remainder', () => {
    const { container } = renderComposer('He');

    const input = screen.getByRole('textbox', { name: 'Message input' });
    expect(input).toHaveProperty('value', 'He');
    expect(input.getAttribute('aria-describedby')).toBe('composer-suggestion-practice-hint');
    expect(screen.getByRole('button', { name: 'Cancel practice' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Trace next character' })).toBeTruthy();
    expect(container.querySelector('.opacity-35')?.textContent).toBe('llo');
  });

  it('hides the ghost remainder after the user intentionally diverges', () => {
    const { container } = renderComposer('Help me');

    expect(container.querySelector('.opacity-35')).toBeNull();
    expect(screen.getByRole('button', { name: 'Cancel practice' })).toBeTruthy();
  });
});
