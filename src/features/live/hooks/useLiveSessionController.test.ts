// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';

const ports = vi.hoisted(() => ({ pcmToWav: vi.fn(), cache: vi.fn(), suggestions: vi.fn(), clearDrafts: vi.fn() }));
vi.mock('../../speech', () => ({
  useGeminiLiveConversation: () => ({ start: vi.fn(), stop: vi.fn() }),
  pcmToWav: ports.pcmToWav, mapAudioSegmentsToTextLines: () => [0],
}));
vi.mock('../../chat', () => ({ computeTtsCacheKey: () => 'cache-key' }));
vi.mock('../../vision', () => ({ processMediaForUpload: vi.fn() }));
vi.mock('../../../api/gemini/files', () => ({ uploadMediaToFiles: vi.fn() }));

import { initialSettings, allGeneratedLanguagePairs, useMaestroStore } from '../../../store';
import { useLiveSessionController, type UseLiveSessionControllerConfig } from './useLiveSessionController';

beforeEach(() => {
  vi.resetAllMocks(); ports.pcmToWav.mockReturnValue('data:audio/wav;base64,retained'); ports.suggestions.mockResolvedValue(undefined);
  useMaestroStore.setState({ messages: [], liveSessionState: 'idle', liveSessionError: null,
    settings: { ...initialSettings, selectedLanguagePairId: allGeneratedLanguagePairs[0].id } });
});
afterEach(cleanup);

it.each(['', 'A transcribed answer'])('retains the model audio after completion with transcript %j', async modelText => {
  const store = useMaestroStore.getState();
  const config: UseLiveSessionControllerConfig = {
    t: key => key, setSettings: vi.fn(), addMessage: store.addMessage, updateMessage: store.updateMessage,
    upsertLiveTranscriptMessage: vi.fn(), removeLiveTranscriptMessage: vi.fn(), clearLiveTranscriptMessages: ports.clearDrafts,
    getHistoryRespectingBookmark: messages => messages, fetchAndSetReplySuggestions: ports.suggestions,
    upsertMessageTtsCache: ports.cache, liveVideoStream: null, setLiveVideoStream: vi.fn(),
    visualContextVideoRef: { current: null }, visualContextStreamRef: { current: null }, captureSnapshot: vi.fn(async () => null),
    isListening: false, stopListening: vi.fn(), startListening: vi.fn(), clearTranscript: vi.fn(),
    addActivityToken: vi.fn(), removeActivityToken: vi.fn(), scheduleReengagement: vi.fn(), cancelReengagement: vi.fn(),
    handleUserInputActivity: vi.fn(), currentSystemPromptText: '',
    parseGeminiResponse: text => [{ target: text || '', native: '' }], resolveBookmarkContextSummary: () => null,
    computeHistorySubsetForMedia: messages => messages, computeMaxMessagesForArray: () => undefined,
    maestroAvatarUriRef: { current: null }, maestroAvatarMimeTypeRef: { current: null },
  };
  const h = renderHook(() => useLiveSessionController(config));
  const audio = new Int16Array([100, 200, 300]);
  await act(async () => { await h.result.current.handleLiveTurnComplete('', modelText, undefined, [audio]); });
  const messages = useMaestroStore.getState().messages;
  expect(messages).toHaveLength(1);
  expect(messages[0].role).toBe('assistant');
  expect(ports.pcmToWav).toHaveBeenCalledWith(audio, 24000);
  expect(ports.clearDrafts).toHaveBeenCalledOnce();
  if (modelText) {
    expect(messages[0].rawAssistantResponse).toBe(modelText);
    expect(messages[0].imageUrl).toBeUndefined();
    expect(ports.cache).toHaveBeenCalledOnce();
    expect(ports.cache).toHaveBeenCalledWith(expect.any(String), expect.objectContaining({ audioDataUrl: 'data:audio/wav;base64,retained' }));
    expect(ports.suggestions).toHaveBeenCalledOnce();
  } else {
    expect(messages[0]).toMatchObject({ imageUrl: 'data:audio/wav;base64,retained', imageMimeType: 'audio/wav', attachmentName: 'live-response.wav' });
    expect(ports.cache).not.toHaveBeenCalled();
    expect(ports.suggestions).not.toHaveBeenCalled();
  }
});
