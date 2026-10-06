// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import InputArea from './InputArea';
import { useMaestroStore } from '../../../store';
import { initialSettings } from '../../../store/slices/settingsSlice';
import { safeSaveChatHistoryDB } from '../services/chatHistory';

// Keep the real composer, language-selection controller and store. Only storage
// and the unrelated media/session controls are outside this interaction test.
vi.mock('../../session/services/settings', () => ({ getAppSettingsDB: vi.fn(), setAppSettingsDB: vi.fn().mockResolvedValue(undefined) }));
vi.mock('../services/chatHistory', async importOriginal => ({ ...await importOriginal<typeof import('../services/chatHistory')>(), safeSaveChatHistoryDB: vi.fn().mockResolvedValue(true) }));
vi.mock('./input/MediaAttachments', () => ({ default: () => null }));
vi.mock('./input/AudioControls', () => ({ default: () => null }));
vi.mock('./input/CameraControls', () => ({ default: () => null }));
vi.mock('../../session/components/SessionControls', () => ({ default: () => null }));
vi.mock('./PdfViewer', () => ({ default: () => null, renderPdfPageToImage: vi.fn(), getPdfPageCount: vi.fn() }));

const onSendMessage = vi.fn().mockResolvedValue(true);
const renderComposer = () => render(<InputArea
  onSttToggle={vi.fn()} onSendMessage={onSendMessage} onUserInputActivity={vi.fn()}
  onStartLiveSession={vi.fn()} onStopLiveSession={vi.fn()} onStopSilentObserver={vi.fn()}
  onToggleSuggestionMode={vi.fn()} onCreateSuggestion={vi.fn()}
  onToggleSendWithSnapshot={vi.fn()} onToggleUseVisualContextForReengagement={vi.fn()}
  suggestionPracticeTarget={null} onEndSuggestionPractice={vi.fn()}
/>);
const chooseLanguages = () => act(() => {
  const store = useMaestroStore.getState();
  store.setIsLanguageSelectionOpen(true);
  store.setTempNativeLangCode('en-US');
  store.setTempTargetLangCode('es-ES');
});

beforeEach(() => {
  vi.clearAllMocks();
  useMaestroStore.setState({ ...useMaestroStore.getInitialState(), settings: structuredClone(initialSettings), isSettingsLoaded: true }, true);
});
afterEach(() => { cleanup(); });

describe('Composer language confirmation', () => {
  it('immediately accepts a first learner\'s chosen pair without waiting for idle confirmation or sending a message', async () => {
    renderComposer();
    chooseLanguages();
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Confirm language selection' })); });
    const state = useMaestroStore.getState();
    expect(state.settings.selectedLanguagePairId).toBe('es-ES-en-US');
    expect(state.settings.stt.language).toBe('es-ES');
    expect(state.isLanguageSelectionOpen).toBe(false);
    expect(onSendMessage).not.toHaveBeenCalled();
    expect(safeSaveChatHistoryDB).not.toHaveBeenCalled();
  });

  it('saves the previous conversation and confirms new choices instead of restoring the old pair', async () => {
    useMaestroStore.setState({ settings: { ...structuredClone(initialSettings), selectedLanguagePairId: 'fr-FR-en-US' } });
    renderComposer();
    chooseLanguages();
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Confirm language selection' })); });
    expect(safeSaveChatHistoryDB).toHaveBeenCalledExactlyOnceWith('fr-FR-en-US', []);
    expect(useMaestroStore.getState().settings.selectedLanguagePairId).toBe('es-ES-en-US');
    expect(useMaestroStore.getState().isLanguageSelectionOpen).toBe(false);
    expect(onSendMessage).not.toHaveBeenCalled();
  });
});
