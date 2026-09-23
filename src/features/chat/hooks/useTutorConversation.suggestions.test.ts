// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ChatMessage } from '../../../core/types';

const ports = vi.hoisted(() => ({
  runSuggestions: vi.fn(), getProfile: vi.fn(), saveProfile: vi.fn(),
  saveHistory: vi.fn(), saveSettings: vi.fn(), usage: vi.fn(),
  audioNote: vi.fn(), upload: vi.fn(), optimize: vi.fn(),
}));
vi.mock('../../../api/gemini/journeys', () => ({
  runReplySuggestions: ports.runSuggestions, runTutorTextTurn: vi.fn(), runMaestroImageGeneration: vi.fn(),
}));
vi.mock('../../../api/gemini/client', () => ({ ApiError: class extends Error {} }));
vi.mock('../../../api/gemini/generative', () => ({ translateText: vi.fn() }));
vi.mock('../../../api/gemini/files', () => ({
  uploadMediaToFiles: ports.upload, checkFileStatuses: vi.fn(), sanitizeHistoryWithVerifiedUris: vi.fn(),
}));
vi.mock('../../../api/gemini/music', () => ({ generateMusic: vi.fn() }));
vi.mock('../../../api/gemini/maestroAvatarEnsure', () => ({ ensureMaestroAvatarUris: vi.fn(), invalidateMaestroAvatarCache: vi.fn() }));
vi.mock('../../session', () => ({
  getGlobalProfileDB: ports.getProfile, setGlobalProfileDB: ports.saveProfile,
  setAppSettingsDB: ports.saveSettings, getAppSettingsDB: vi.fn(),
}));
vi.mock('..', () => ({
  safeSaveChatHistoryDB: ports.saveHistory, deriveHistoryForApi: vi.fn(), INLINE_CAP_AUDIO: 1024,
  getChatHistoryDB: vi.fn(), getChatMetaDB: vi.fn(), upsertTtsCacheEntries: vi.fn(),
}));
vi.mock('../../vision', () => ({ processMediaForUpload: ports.optimize, createKeyframeFromVideoDataUrl: vi.fn() }));
vi.mock('../../speech/services/geminiLiveAudioNote', () => ({ synthesizeGeminiAudioNote: ports.audioNote }));
vi.mock('../../../shared/utils/costTracker', () => ({ trackGeminiUsage: ports.usage, hasCostWarningShown: vi.fn(), setCostWarningShown: vi.fn() }));

import { useMaestroStore, initialSettings, allGeneratedLanguagePairs } from '../../../store';
import { selectIsLoadingSuggestions } from '../../../store/slices/uiSlice';
import { useTutorConversation, type UseTutorConversationConfig } from './useTutorConversation';

const suggestion = { target: 'Hola', native: 'Hello' };
const message = (id: string, extra: Partial<ChatMessage> = {}): ChatMessage => ({
  id, timestamp: 1, role: 'assistant', text: 'Hola', isLoadingArtifact: true,
  artifactLoadStartTime: 123, ...extra,
});
let events: string[];
let savedHistory: ChatMessage[];
const pair = allGeneratedLanguagePairs[0];

function harness(messages: ChatMessage[]) {
  useMaestroStore.setState({ messages });
  const state = useMaestroStore.getState();
  const config: UseTutorConversationConfig = {
    t: key => key, setSettings: state.setSettings, setMessages: state.setMessages,
    addMessage: value => { events.push('add'); return state.addMessage(value); },
    updateMessage: (id, update) => { events.push(`update:${Object.keys(update).join(',')}`); state.updateMessage(id, update); },
    getHistoryRespectingBookmark: history => history.slice(1), computeMaxMessagesForArray: () => undefined,
    captureSnapshot: vi.fn().mockResolvedValue(null), speakMessage: vi.fn(), isSpeechSynthesisSupported: false,
    stopListening: vi.fn().mockResolvedValue(undefined), startListening: vi.fn(), clearTranscript: vi.fn(),
    hasPendingQueueItems: () => false, claimRecordedUtterance: () => null,
    scheduleReengagementRef: { current: vi.fn() }, cancelReengagementRef: { current: vi.fn() },
    transcript: '', currentSystemPromptText: '', currentReplySuggestionsPromptText: '',
    setReplySuggestions: state.setReplySuggestions, handleToggleSuggestionModeRef: { current: vi.fn() },
    maestroAvatarUriRef: { current: null }, maestroAvatarMimeTypeRef: { current: null },
  };
  const hook = renderHook(() => useTutorConversation(config));
  return {
    ...hook,
    fetch: (id = 'a', text = 'Hola', source?: 'chat' | 'live') => act(async () => {
      await hook.result.current.fetchAndSetReplySuggestions(id, text, messages, { responseSource: source });
    }),
  };
}

beforeEach(() => {
  vi.resetAllMocks();
  events = []; savedHistory = [];
  useMaestroStore.setState({
    settings: { ...initialSettings, selectedLanguagePairId: pair.id }, selectedLanguagePair: pair,
    messages: [], activityTokens: new Set(), replySuggestions: [], suggestionsLoadingStreamText: '',
    lastFetchedSuggestionsFor: null, imageLoadDurations: [],
  });
  ports.getProfile.mockResolvedValue({ text: 'Existing profile' });
  ports.runSuggestions.mockResolvedValue({ suggestions: [suggestion] });
  ports.saveHistory.mockImplementation(async (_pair, history) => { events.push('save-history'); savedHistory = structuredClone(history); });
  ports.saveProfile.mockImplementation(async () => { events.push('save-profile'); });
  ports.saveSettings.mockResolvedValue(undefined);
  ports.usage.mockImplementation(() => { events.push('usage'); });
  ports.audioNote.mockImplementation(async () => {
    events.push(`audio:${selectIsLoadingSuggestions(useMaestroStore.getState())}`);
    return { dataUrl: 'data:audio/wav;base64,AQID', mimeType: 'audio/wav' };
  });
  ports.optimize.mockResolvedValue({ dataUrl: 'data:audio/wav;base64,AQ==', mimeType: 'audio/wav' });
  ports.upload.mockResolvedValue({ uri: 'https://files/audio', mimeType: 'audio/wav' });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe('actual tutor hook suggestion contract (captured before coordinator extraction)', () => {
  it.each([{}, { llmRawResponse: 'Hola\nstructured tail', uploadedFileVariants: [{ id: 'x', uri: 'uri', mimeType: 'image/png', targets: ['text'] }] }])(
    'reuses target suggestions and clears pending artifact state without another request: %j', async extra => {
      const h = harness([message('a', { replySuggestions: [suggestion], ...extra } as Partial<ChatMessage>)]);
      await h.fetch();
      expect(ports.runSuggestions).not.toHaveBeenCalled();
      expect(useMaestroStore.getState()).toMatchObject({ replySuggestions: [suggestion], lastFetchedSuggestionsFor: 'a' });
      expect(useMaestroStore.getState().messages[0]).toMatchObject({ isLoadingArtifact: false, artifactLoadStartTime: undefined });
    },
  );

  it('reuses the last sibling in the assistant-only block, never crossing a user turn', async () => {
    const h = harness([
      message('old', { replySuggestions: [{ target: 'Old', native: 'Old' }] }), message('u', { role: 'user' }),
      message('a'), message('sibling', { replySuggestions: [suggestion] }), message('next', { role: 'user' }),
      message('future', { replySuggestions: [{ target: 'Future', native: 'Future' }] }),
    ]);
    await h.fetch();
    expect(ports.runSuggestions).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().lastFetchedSuggestionsFor).toBe('sibling');
    expect(useMaestroStore.getState().replySuggestions).toEqual([suggestion]);
  });

  it('regenerates a structured tail without an attachment and preserves provider input and save order', async () => {
    const input = [message('u', { role: 'user' }), message('a', { replySuggestions: [suggestion], llmRawResponse: 'Hola\nstructured tail' })];
    ports.runSuggestions.mockResolvedValue({ suggestions: [suggestion], chatSummary: ' Summary ', globalProfile: ' New profile ', reengagementSeconds: 17 });
    const onProfile = () => events.push('profile-event');
    window.addEventListener('globalProfileUpdated', onProfile);
    try {
      const h = harness(input);
      await h.fetch();
      expect(ports.runSuggestions.mock.calls[0][0]).toEqual({ assistantMessageId: 'a', lastTutorMessage: 'Hola', history: input.slice(1), languagePair: pair, existingGlobalProfile: 'Existing profile', responseSource: undefined });
      expect(events).toEqual(['usage', 'update:replySuggestions', 'save-history', 'update:chatSummary', 'save-profile', 'profile-event', 'update:isLoadingArtifact,artifactLoadStartTime']);
      expect(savedHistory[1].replySuggestions).toEqual([suggestion]);
      expect(savedHistory[1].chatSummary).toBeUndefined();
      expect(useMaestroStore.getState().settings.smartReengagement.thresholdSeconds).toBe(17);
      expect(ports.saveProfile).toHaveBeenCalledWith('New profile');
      expect(selectIsLoadingSuggestions(useMaestroStore.getState())).toBe(false);
    } finally { window.removeEventListener('globalProfileUpdated', onProfile); }
  });

  it('keeps suggestion loading until completion, condenses stream status, and releases it on provider failure', async () => {
    const errorLog = vi.spyOn(console, 'error').mockImplementation(() => {});
    ports.runSuggestions.mockImplementation(async (_input, options) => {
      expect(selectIsLoadingSuggestions(useMaestroStore.getState())).toBe(true);
      options.lifecycleHooks.onThoughtDelta('', '  Some\nthought  ');
      expect(useMaestroStore.getState().suggestionsLoadingStreamText).toBe('thinking: Some thought');
      options.lifecycleHooks.onTextDelta('', 'x'.repeat(60));
      expect(useMaestroStore.getState().suggestionsLoadingStreamText).toBe(`…${'x'.repeat(48)}`);
      throw new Error('provider unavailable');
    });
    const h = harness([message('a')]);
    await h.fetch();
    expect(errorLog).toHaveBeenCalledTimes(1);
    expect(useMaestroStore.getState().replySuggestions).toEqual([]);
    expect(useMaestroStore.getState().suggestionsLoadingStreamText).toBe('');
    expect(selectIsLoadingSuggestions(useMaestroStore.getState())).toBe(false);
    expect(useMaestroStore.getState().messages[0].isLoadingArtifact).toBe(false);
  });

  it('continues after profile read, history save and profile save failures', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    ports.getProfile.mockRejectedValue(new Error('read'));
    ports.saveHistory.mockRejectedValue(new Error('history'));
    ports.saveProfile.mockRejectedValue(new Error('profile'));
    ports.runSuggestions.mockResolvedValue({ suggestions: [suggestion], globalProfile: 'new', chatSummary: 'summary' });
    await harness([message('a')]).fetch();
    expect(ports.runSuggestions.mock.calls[0][0].existingGlobalProfile).toBe('');
    expect(useMaestroStore.getState().messages[0]).toMatchObject({ replySuggestions: [suggestion], chatSummary: 'summary', isLoadingArtifact: false });
    expect(selectIsLoadingSuggestions(useMaestroStore.getState())).toBe(false);
  });

  it.each(['chat', 'live'] as const)('splits artifact and audio tool with %s context and releases loading before tool execution', async source => {
    ports.runSuggestions.mockResolvedValue({ suggestions: [suggestion], artifact: { mimeType: 'text/markdown', fileName: 'lesson.md', content: '# Lesson' }, toolRequest: { tool: 'audio-note', text: 'Speak this' } });
    const h = harness([message('a', { storageOptimizedImageUrl: 'old', storageOptimizedImageMimeType: 'image/png' })]);
    await h.fetch('a', 'Hola', source);
    const [artifact, tool] = useMaestroStore.getState().messages;
    expect(artifact).toMatchObject({ imageMimeType: 'text/markdown', attachmentName: 'lesson.md', storageOptimizedImageUrl: undefined, storageOptimizedImageMimeType: undefined, uploadedFileVariants: undefined, artifactLoadStartTime: undefined });
    expect(tool).toMatchObject({ role: 'assistant', imageMimeType: 'audio/wav', maestroToolKind: 'audio-note' });
    expect(tool.id).not.toBe(artifact.id);
    expect(events).toContain('audio:false');
    expect(ports.audioNote).toHaveBeenCalledWith({ text: 'Speak this', langCode: pair.targetLanguageCode.split('-')[0], voiceName: 'Kore' });
    expect({ artifactRaw: artifact.llmRawResponse, toolRaw: tool.llmRawResponse }).toMatchSnapshot();
  });
});
