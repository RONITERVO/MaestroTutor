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
const onStartLiveSession = vi.fn();
const onStopLiveSession = vi.fn();
const renderComposer = () => render(<InputArea
  onSttToggle={vi.fn()} onSendMessage={onSendMessage} onUserInputActivity={vi.fn()}
  onStartLiveSession={onStartLiveSession} onStopLiveSession={onStopLiveSession} onStopSilentObserver={vi.fn()}
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


describe('Camera-independent Live controls', () => {
  beforeEach(() => {
    useMaestroStore.setState({ settings: { ...structuredClone(initialSettings), selectedLanguagePairId: 'es-ES-en-US' }, isLanguageSelectionOpen: false });
  });
  it('starts Live without a preview and keeps the learner draft', async () => {
    renderComposer();
    fireEvent.change(screen.getByRole('textbox', { name: 'Message input' }), { target: { value: 'My draft' } });
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Start Live' })); });
    expect(onStartLiveSession).toHaveBeenCalledOnce();
    expect(onSendMessage).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().liveVideoStream).toBeNull();
    expect((screen.getByRole('textbox', { name: 'Message input' }) as HTMLTextAreaElement).value).toBe('My draft');
  });

  it.each(['active', 'armed'] as const)('keeps Stop available for an audio-only %s call', async liveSessionState => {
    useMaestroStore.setState({ liveSessionState, liveVideoStream: null });
    renderComposer();
    expect(screen.getByText('The live call is active. Maestro can hear you.')).toBeTruthy();
    expect(screen.queryByText(/Maestro can see and hear you/)).toBeNull();
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Stop Live' })); });
    expect(onStopLiveSession).toHaveBeenCalledOnce();
    expect(onStartLiveSession).not.toHaveBeenCalled();
  });

  it('prevents duplicate starts while connecting and exposes a camera-free retry error', async () => {
    useMaestroStore.setState({ liveSessionState: 'connecting' });
    renderComposer();
    const start = screen.getByRole('button', { name: 'Start Live' }) as HTMLButtonElement;
    expect(start.disabled).toBe(true);
    fireEvent.click(start);
    expect(onStartLiveSession).not.toHaveBeenCalled();
    act(() => useMaestroStore.setState({ liveSessionState: 'error', liveSessionError: 'Microphone access was denied.' }));
    expect(screen.getByRole('alert').textContent).toBe('Microphone access was denied.');
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Retry Live' })); });
    expect(onStartLiveSession).toHaveBeenCalledOnce();
  });
});
