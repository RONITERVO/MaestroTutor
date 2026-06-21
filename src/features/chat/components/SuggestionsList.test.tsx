// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SuggestionsList from './SuggestionsList';

const mockStoreState = vi.hoisted(() => ({
  replySuggestions: [{ target: 'Hola', native: 'Hello' }],
  suggestionsLoadingStreamText: '',
  microphoneApiAvailable: false,
  activityTokens: new Set<string>(),
  settings: { isSuggestionMode: false },
}));

vi.mock('../../../store', () => ({
  useMaestroStore: (selector: (state: typeof mockStoreState) => unknown) => selector(mockStoreState),
}));
vi.mock('../../../store/slices/chatSlice', () => ({
  selectReplySuggestions: (state: typeof mockStoreState) => state.replySuggestions,
  selectSuggestionsLoadingStreamText: (state: typeof mockStoreState) => state.suggestionsLoadingStreamText,
}));
vi.mock('../../../store/slices/settingsSlice', () => ({
  selectSettings: (state: typeof mockStoreState) => state.settings,
}));
vi.mock('../../../store/slices/uiSlice', () => ({
  selectIsLoadingSuggestions: () => false,
  selectIsCreatingSuggestion: () => false,
  selectIsSpeaking: () => false,
}));

const t = (key: string, replacements?: Record<string, string | number>): string => {
  if (key === 'chat.suggestion.speakOrPractice') {
    return `Speak or practice ${replacements?.suggestion ?? ''}`;
  }
  return key;
};

beforeEach(() => {
  vi.useFakeTimers();
  Object.defineProperties(HTMLElement.prototype, {
    setPointerCapture: { configurable: true, value: vi.fn() },
    hasPointerCapture: { configurable: true, value: vi.fn(() => true) },
    releasePointerCapture: { configurable: true, value: vi.fn() },
  });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

const renderSuggestions = () => {
  const onPracticeSuggestion = vi.fn();
  const onSuggestionClick = vi.fn();
  render(
    <SuggestionsList
      t={t}
      onToggleSuggestionMode={vi.fn()}
      onSuggestionClick={onSuggestionClick}
      stopSpeaking={vi.fn()}
      onToggleSpeakNativeLang={vi.fn()}
      speakNativeLang={false}
      onPracticeSuggestion={onPracticeSuggestion}
      isPracticeDisabled={false}
    />
  );
  return { onPracticeSuggestion, onSuggestionClick };
};

describe('SuggestionsList practice gesture', () => {
  it('starts practice after a stationary long press without also speaking', () => {
    const { onPracticeSuggestion, onSuggestionClick } = renderSuggestions();
    const button = screen.getByRole('button', { name: 'Speak or practice Hola' });

    fireEvent.pointerDown(button, {
      pointerId: 1,
      pointerType: 'touch',
      isPrimary: true,
      clientX: 20,
      clientY: 20,
    });
    act(() => vi.advanceTimersByTime(550));
    fireEvent.pointerUp(button, { pointerId: 1, pointerType: 'touch', isPrimary: true });
    fireEvent.click(button);

    expect(onPracticeSuggestion).toHaveBeenCalledTimes(1);
    expect(onPracticeSuggestion).toHaveBeenCalledWith({ target: 'Hola', native: 'Hello' });
    expect(onSuggestionClick).not.toHaveBeenCalled();
  });

  it('provides Shift+Enter as a keyboard equivalent', () => {
    const { onPracticeSuggestion, onSuggestionClick } = renderSuggestions();
    const button = screen.getByRole('button', { name: 'Speak or practice Hola' });

    fireEvent.keyDown(button, { key: 'Enter', shiftKey: true });

    expect(onPracticeSuggestion).toHaveBeenCalledTimes(1);
    expect(onSuggestionClick).not.toHaveBeenCalled();
  });

  it('cancels the gesture when the pointer moves, so scrolling does not activate it', () => {
    const { onPracticeSuggestion, onSuggestionClick } = renderSuggestions();
    const button = screen.getByRole('button', { name: 'Speak or practice Hola' });

    fireEvent.pointerDown(button, {
      pointerId: 2,
      pointerType: 'touch',
      isPrimary: true,
      clientX: 20,
      clientY: 20,
    });
    fireEvent.pointerMove(button, {
      pointerId: 2,
      pointerType: 'touch',
      isPrimary: true,
      clientX: 50,
      clientY: 20,
    });
    act(() => vi.advanceTimersByTime(550));
    fireEvent.pointerUp(button, { pointerId: 2, pointerType: 'touch', isPrimary: true });
    fireEvent.click(button);

    expect(onPracticeSuggestion).not.toHaveBeenCalled();
    expect(onSuggestionClick).not.toHaveBeenCalled();
  });
});
