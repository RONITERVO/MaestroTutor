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
  runText: vi.fn(), runImage: vi.fn(), sanitizeHistory: vi.fn(), fileStatuses: vi.fn(), avatar: vi.fn(),
}));
vi.mock('../../../api/gemini/journeys', () => ({
  runReplySuggestions: ports.runSuggestions, runTutorTextTurn: ports.runText, runMaestroImageGeneration: ports.runImage,
}));
vi.mock('../../../api/gemini/client', async () => ({ ApiError: (await import('../../../core-sdk/errors')).ApiError }));
vi.mock('../../../api/gemini/generative', () => ({ translateText: vi.fn() }));
vi.mock('../../../api/gemini/files', () => ({
  uploadMediaToFiles: ports.upload, checkFileStatuses: ports.fileStatuses, sanitizeHistoryWithVerifiedUris: ports.sanitizeHistory,
}));
vi.mock('../../../api/gemini/music', () => ({ generateMusic: vi.fn() }));
vi.mock('../../../api/gemini/maestroAvatarEnsure', () => ({ ensureMaestroAvatarUris: ports.avatar, invalidateMaestroAvatarCache: vi.fn() }));
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
vi.mock('../../../shared/utils/costTracker', () => ({ trackGeminiUsage: ports.usage, hasShownCostWarning: vi.fn(() => true), setCostWarningShown: vi.fn() }));

import { useMaestroStore, initialSettings, allGeneratedLanguagePairs } from '../../../store';
import { selectIsLoadingSuggestions } from '../../../store/slices/uiSlice';
import { useTutorConversation, type UseTutorConversationConfig } from './useTutorConversation';
import { ApiError } from '../../../core-sdk/errors';

const suggestion = { target: 'Hola', native: 'Hello' };
const message = (id: string, extra: Partial<ChatMessage> = {}): ChatMessage => ({
  id, timestamp: 1, role: 'assistant', text: 'Hola', isLoadingArtifact: true,
  artifactLoadStartTime: 123, ...extra,
});
let events: string[];
let savedHistory: ChatMessage[];
const pair = allGeneratedLanguagePairs[0];

function harness(messages: ChatMessage[], overrides: Partial<UseTutorConversationConfig> = {}) {
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
    ...overrides,
  };
  const hook = renderHook(() => useTutorConversation(config));
  return {
    ...hook,
    config,
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
  ports.sanitizeHistory.mockImplementation(async history => history);
  ports.fileStatuses.mockResolvedValue({});
  ports.avatar.mockResolvedValue({});
  ports.runText.mockResolvedValue({
    response: { text: 'Hola', modelUsed: 'model', usageMetadata: { promptTokenCount: 1 } },
    rawResponse: 'Hola', searchQueryCount: 0,
    parsed: { visibleText: 'Hola', translations: [], hasSkippedNonLanguageContent: false },
  });
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

describe('actual tutor hook send contract before coordinator extraction', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(console, 'log').mockImplementation(() => {});
    useMaestroStore.setState({
      settings: { ...initialSettings, selectedLanguagePairId: pair.id, selectedCameraId: 'camera', sendWithSnapshotEnabled: false },
      isLoadingHistory: false, sttInterruptedBySend: false, recordedUtterancePending: null,
      pendingRecordedAudioMessageId: null, attachedImageBase64: null, attachedImageMimeType: null, attachedFileName: null,
    });
  });

  const send = async (hook: ReturnType<typeof harness>, text = 'User input', image?: string, mime?: string, kind: 'user' | 'conversational-reengagement' | 'image-reengagement' = 'user', stt = false) => {
    let result = false;
    await act(async () => { result = await hook.result.current.handleSendMessageInternal(text, image, mime, kind, { triggeredByStt: stt }); });
    return result;
  };

  it('preserves the composed request, placeholder lifecycle and early suggestion input', async () => {
    const h = harness([message('u', { role: 'user', text: 'Before' }), message('old', { isLoadingArtifact: false })]);
    expect(await send(h)).toBe(true);
    expect(ports.runText.mock.calls[0][0]).toMatchSnapshot();
    const messages = useMaestroStore.getState().messages;
    expect(messages.slice(-2).map(({ role, text, thinking }) => ({ role, text, thinking }))).toEqual([
      { role: 'user', text: 'User input', thinking: undefined }, { role: 'assistant', text: 'Hola', thinking: false },
    ]);
    expect(ports.runSuggestions).toHaveBeenCalledTimes(1);
    expect(ports.runSuggestions.mock.calls[0][0]).toMatchObject({ lastTutorMessage: 'Hola' });
    expect(useMaestroStore.getState().activityTokens.size).toBe(0);
    expect(useMaestroStore.getState().sendPrep).toBeNull();
    expect(h.config.cancelReengagementRef.current).toHaveBeenCalledOnce();
    expect(h.config.scheduleReengagementRef.current).toHaveBeenCalledWith('send-complete');
  });

  it.each(['loading', 'speaking', 'pending', 'empty', 'no-pair'] as const)('rejects %s before provider access', async reason => {
    if (reason === 'loading') useMaestroStore.setState({ isLoadingHistory: true });
    if (reason === 'speaking') useMaestroStore.getState().addActivityToken('tts', 'speak');
    if (reason === 'pending') useMaestroStore.getState().addActivityToken('gen', 'response');
    if (reason === 'no-pair') useMaestroStore.setState({ selectedLanguagePair: undefined, settings: { ...initialSettings } });
    const h = harness([]);
    expect(await send(h, reason === 'empty' ? '' : 'Hello')).toBe(false);
    expect(ports.runText).not.toHaveBeenCalled();
    expect(ports.getProfile).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().messages.length).toBe(reason === 'no-pair' ? 1 : 0);
  });

  it.each([
    { code: 'MISSING_API_KEY', status: undefined, message: 'missing', text: 'error.apiKeyMissing', gate: 'missing' },
    { code: 'INVALID_ARGUMENT', status: 400, message: 'API_KEY_INVALID', text: 'error.apiKeyInvalid', gate: 'invalid' },
    { code: 'RESOURCE_EXHAUSTED', status: 429, message: 'quota', text: 'error.apiQuotaExceeded', gate: undefined },
    { code: 'INTERNAL', status: 500, message: '{"error":{"message":"Server error"}}', text: 'Server error', gate: undefined },
  ])('clears the placeholder and token on $code without changing error actions', async error => {
    const gate = vi.fn();
    ports.runText.mockRejectedValue(new ApiError(error.message, { code: error.code, status: error.status }));
    const h = harness([], { onApiKeyGateOpen: gate });
    expect(await send(h)).toBe(false);
    expect(useMaestroStore.getState().messages.slice(-1)[0]).toMatchObject({ role: 'error', text: error.text, thinking: false, isLoadingArtifact: false });
    expect(useMaestroStore.getState().messages.slice(-1)[0]?.errorAction).toBe(error.status === 429 ? 'quota' : undefined);
    expect(useMaestroStore.getState().activityTokens.size).toBe(0);
    expect(useMaestroStore.getState().sendPrep).toBeNull();
    expect(h.config.scheduleReengagementRef.current).toHaveBeenCalledWith('send-error');
    if (error.gate) expect(gate).toHaveBeenCalledWith({ reason: error.gate, instructionIndex: 0 });
    else expect(gate).not.toHaveBeenCalled();
  });

  it.each(['success', 'failure'] as const)('hands STT back after %s and preserves pending-queue arbitration', async outcome => {
    const state = useMaestroStore.getState();
    state.setSettings(previous => ({ ...previous, stt: { ...previous.stt, enabled: true } }));
    state.addActivityToken('stt', 'listen');
    const h = harness([], { stopListening: vi.fn(async () => { events.push('stop'); useMaestroStore.getState().removeActivityToken('stt:listen'); }), startListening: vi.fn(() => { events.push('start'); }) });
    if (outcome === 'failure') ports.runText.mockRejectedValue(new Error('provider down'));
    expect(await send(h, 'Speech', undefined, undefined, 'user', true)).toBe(outcome === 'success');
    expect(h.config.stopListening).toHaveBeenCalledOnce();
    expect(h.config.clearTranscript).toHaveBeenCalledOnce();
    expect(h.config.startListening).toHaveBeenCalledWith(useMaestroStore.getState().settings.stt.language);
    expect(useMaestroStore.getState().sttInterruptedBySend).toBe(false);
    expect(events[0]).toBe('stop');
    expect(events.slice(-1)[0]).toBe('start');
  });

  it('uploads original current media, keeps optimized bytes local, and sends file parts only', async () => {
    const original = 'data:image/png;base64,AQID';
    ports.optimize.mockResolvedValue({ dataUrl: 'data:image/jpeg;base64,AQ==', mimeType: 'image/jpeg' });
    ports.upload.mockResolvedValue({ uri: 'https://files/photo', mimeType: 'image/png' });
    const h = harness([]);
    expect(await send(h, 'Picture', original, 'image/png')).toBe(true);
    expect(ports.upload.mock.calls[0].slice(0, 2)).toEqual([original, 'image/png']);
    expect(ports.runText.mock.calls[0][0].currentFileParts).toEqual([{ fileUri: 'https://files/photo', mimeType: 'image/png' }]);
    expect(useMaestroStore.getState().messages[0]).toMatchObject({ imageUrl: original, storageOptimizedImageUrl: 'data:image/jpeg;base64,AQ==' });
  });

  it('fails before generation when a current attachment cannot be uploaded', async () => {
    ports.upload.mockRejectedValue(new Error('upload failed'));
    const h = harness([]);
    expect(await send(h, 'Picture', 'data:image/png;base64,AQID', 'image/png')).toBe(false);
    expect(ports.runText).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().activityTokens.size).toBe(0);
    expect(useMaestroStore.getState().messages.slice(-1)[0]?.role).toBe('error');
  });

  it.each(['conversational-reengagement', 'image-reengagement'] as const)('keeps %s distinct from a new user message', async kind => {
    ports.upload.mockResolvedValue({ uri: 'https://files/snapshot', mimeType: 'image/png' });
    const h = harness([]);
    expect(await send(h, '', 'data:image/png;base64,AQID', 'image/png', kind)).toBe(true);
    expect(useMaestroStore.getState().messages.every(m => m.role !== 'user')).toBe(true);
    expect(ports.runText.mock.calls[0][0].prompt).toMatchSnapshot();
    expect(ports.runText.mock.calls[0][0].currentFileParts).toEqual(kind === 'image-reengagement' ? [{ fileUri: 'https://files/snapshot', mimeType: 'image/png' }] : undefined);
  });

  it.each([false, true])('preserves generated user images when upload fails: %s', async fails => {
    useMaestroStore.getState().setSettings(previous => ({ ...previous, selectedCameraId: 'image-gen-camera', sendWithSnapshotEnabled: true }));
    ports.runImage.mockResolvedValue({ base64Image: 'data:image/png;base64,AQID', mimeType: 'image/png' });
    if (fails) ports.upload.mockRejectedValue(new Error('upload'));
    else ports.upload.mockResolvedValue({ uri: 'https://files/generated', mimeType: 'image/png' });
    const h = harness([]);
    expect(await send(h, 'Draw this')).toBe(true);
    expect(ports.runImage.mock.calls[0][0]).toMatchObject({ contextText: 'Draw this', maestroAvatarUri: undefined, maestroAvatarMimeType: undefined });
    expect(useMaestroStore.getState().messages[0]).toMatchObject({ imageUrl: 'data:image/png;base64,AQID', isGeneratingImage: false, imageGenError: null });
    expect(ports.runText.mock.calls[0][0].currentFileParts).toEqual(fails ? undefined : [{ fileUri: 'https://files/generated', mimeType: 'image/png' }]);
  });

  it('keeps a generated audio note visible when its upload fails', async () => {
    ports.runSuggestions.mockResolvedValue({ suggestions: [suggestion], toolRequest: { tool: 'audio-note', text: 'Speak' } });
    ports.upload.mockRejectedValue(new Error('upload'));
    await harness([message('a')]).fetch();
    expect(useMaestroStore.getState().messages[0]).toMatchObject({ imageUrl: 'data:audio/wav;base64,AQID', imageMimeType: 'audio/wav', maestroToolKind: 'audio-note', isGeneratingToolAttachment: false });
    expect(useMaestroStore.getState().activityTokens.size).toBe(0);
  });
});

