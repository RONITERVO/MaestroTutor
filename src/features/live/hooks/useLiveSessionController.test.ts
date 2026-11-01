// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';

const ports = vi.hoisted(() => ({ pcmToWav: vi.fn(), cache: vi.fn(), suggestions: vi.fn(), clearDrafts: vi.fn(), capture: vi.fn(), prepare: vi.fn(), live: vi.fn(), start: vi.fn(), stop: vi.fn() }));
vi.mock('../../speech', () => ({
  useGeminiLiveConversation: (callbacks: any) => { ports.live(callbacks); return { start: ports.start, stop: ports.stop }; },
  pcmToWav: ports.pcmToWav, mapAudioSegmentsToTextLines: () => [0],
}));
vi.mock('../../chat', () => ({ computeTtsCacheKey: () => 'cache-key', captureLiveRoomAgentHandoff: ports.capture, prepareLiveRoomAgentContext: ports.prepare }));
vi.mock('../utils/liveSystemInstruction', () => ({ buildLiveSystemInstruction: async () => 'Original fixture instruction' }));
vi.mock('../../vision', () => ({ processMediaForUpload: vi.fn() }));
vi.mock('../../../api/gemini/files', () => ({ uploadMediaToFiles: vi.fn() }));

import { sessionActivity } from '../../../platform/browser/sessionActivity';
import { initialSettings, allGeneratedLanguagePairs, useMaestroStore } from '../../../store';
import { useLiveSessionController, type UseLiveSessionControllerConfig } from './useLiveSessionController';

beforeEach(() => {
  vi.resetAllMocks(); ports.pcmToWav.mockReturnValue('data:audio/wav;base64,retained'); ports.suggestions.mockResolvedValue(undefined);
  useMaestroStore.setState({ messages: [], isLoadingHistory: false, liveSessionState: 'idle', liveSessionError: null,
    settings: { ...initialSettings, selectedLanguagePairId: allGeneratedLanguagePairs[0].id } });
});
afterEach(cleanup);

it.each(['', 'A transcribed answer'])('retains the model audio after completion with transcript %j', async modelText => {
  const config = createConfig();
  const h = renderHook(() => useLiveSessionController(config));
  const audio = new Int16Array([100, 200, 300]);
  await act(async () => { await h.result.current.handleLiveTurnComplete('', modelText, undefined, [audio], { conversationId: allGeneratedLanguagePairs[0].id }); });
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

function createConfig() {
  const store = useMaestroStore.getState();
  return {
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
  } satisfies UseLiveSessionControllerConfig;
}

it('captures the completed spoken source messages before starting shared suggestion verification', async () => {
  const config = createConfig();
  const h = renderHook(() => useLiveSessionController(config));
  ports.capture.mockImplementation(async (identity, source, user, reply) => {
    expect(identity).toBe('owned-live-context');
    expect(user).toBe('Make a blue robot.'); expect(reply).toBe('I will ask the agent.');
    const messages = useMaestroStore.getState().messages;
    expect(messages.find(message => message.id === source.sourceUserId)?.text).toBe(user);
    expect(messages.find(message => message.id === source.sourceAssistantId)?.llmRawResponse).toBe(reply);
    expect(ports.suggestions).not.toHaveBeenCalled();
    return true;
  });
  await act(async () => { await h.result.current.handleLiveTurnComplete('Make a blue robot.', 'I will ask the agent.', undefined, undefined,
    { conversationId: allGeneratedLanguagePairs[0].id, systemInstruction: 'Original captured context', handoffId: 'owned-live-context', liveInputMedia: { version: 1, complete: false, issue: 'limit', frames: [], packets: [] } }); });
  expect(ports.capture).toHaveBeenCalledOnce(); expect(ports.suggestions).toHaveBeenCalledOnce();
  expect(ports.capture.mock.calls[0][4]).toEqual({ version: 1, complete: false, issue: 'limit', frames: [], packets: [] });
  expect(ports.suggestions.mock.calls[0][3]).toEqual({ responseSource: 'live' });
});

it('does not persist a spoken turn into another conversation after a slow snapshot', async () => {
  const config = createConfig(); let resolve!: (value: null) => void;
  config.captureSnapshot = vi.fn(() => new Promise<null>(done => { resolve = done; }));
  const h = renderHook(() => useLiveSessionController(config)); let completing!: Promise<void>;
  act(() => { completing = h.result.current.handleLiveTurnComplete('Make a robot.', 'I will ask the agent.', undefined, undefined,
    { conversationId: allGeneratedLanguagePairs[0].id, handoffId: 'owned-live-context' }); });
  useMaestroStore.setState({ settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: 'different-conversation' } });
  await act(async () => { resolve(null); await completing; });
  expect(useMaestroStore.getState().messages).toHaveLength(0);
  expect(ports.capture).not.toHaveBeenCalled(); expect(ports.suggestions).not.toHaveBeenCalled();
  expect(ports.clearDrafts).not.toHaveBeenCalled();
});

it.each(['finish', 'stop', 'conversation'])('yields idle user-owned Live without losing its choice; %s', async outcome => {
  const callbacks = () => ports.live.mock.calls[ports.live.mock.calls.length - 1][0];
  ports.start.mockImplementation(async options => callbacks().onStateChange(options.gateInputOnSpeech ? 'armed' : 'active'));
  ports.stop.mockImplementation(async () => callbacks().onStateChange('idle'));
  const config = { ...createConfig(), liveVideoStream: { active: true } as MediaStream };
  const h = renderHook(() => useLiveSessionController(config));
  await act(async () => { await h.result.current.handleStartLiveSession(); });
  expect(ports.start.mock.calls[0][0].conversationId).toBe(allGeneratedLanguagePairs[0].id);
  expect(await h.result.current.pauseLiveForSpeech()).toBeNull(); // Active speech cannot be displaced.
  await act(async () => { callbacks().onStateChange('idle'); });
  expect(useMaestroStore.getState().liveSessionState).toBe('armed'); expect(ports.start).toHaveBeenCalledTimes(2);
  let resume!: (() => void) | null;
  await act(async () => { resume = await h.result.current.pauseLiveForSpeech(); });
  expect(resume).toBeTypeOf('function'); expect(ports.start).toHaveBeenCalledTimes(2);
  expect(useMaestroStore.getState().liveSessionState).toBe('armed');
  if (outcome === 'stop') await act(async () => { await h.result.current.handleStopLiveSession(); });
  if (outcome === 'conversation') act(() => useMaestroStore.setState({ settings: { ...initialSettings, selectedLanguagePairId: 'other' } }));
  await act(async () => { resume!(); resume!(); });
  expect(ports.start).toHaveBeenCalledTimes(outcome === 'finish' ? 3 : 2);
  if (outcome === 'finish') expect(ports.start.mock.calls[2][0]).toMatchObject({ gateInputOnSpeech: true });
});


it.each(['text', 'audio'])('rejects a delayed %s completion from a previously selected conversation', async kind => {
  const config = createConfig(), origin = useMaestroStore.getState().settings.selectedLanguagePairId!;
  const h = renderHook(() => useLiveSessionController(config));
  act(() => useMaestroStore.setState({ messages: [{ id: 'new-draft', role: 'assistant', text: 'Current draft', timestamp: 1 }],
    settings: { ...initialSettings, selectedLanguagePairId: allGeneratedLanguagePairs[1].id } }));
  const before = useMaestroStore.getState().messages;
  await act(async () => { await h.result.current.handleLiveTurnComplete(kind === 'text' ? 'Old question' : '',
    kind === 'text' ? 'Old answer' : '', undefined, kind === 'audio' ? [new Int16Array([100])] : undefined,
    { conversationId: origin }); });
  expect(useMaestroStore.getState().messages).toEqual(before);
  expect(config.captureSnapshot).not.toHaveBeenCalled();
  expect(ports.suggestions).not.toHaveBeenCalled(); expect(ports.capture).not.toHaveBeenCalled();
  expect(ports.clearDrafts).not.toHaveBeenCalled(); expect(ports.cache).not.toHaveBeenCalled();
});

it('does not fetch suggestions or clear the new draft when the conversation changes during handoff persistence', async () => {
  const config = createConfig(); let resolve!: () => void;
  ports.capture.mockImplementation(() => new Promise<void>(done => { resolve = done; }));
  const h = renderHook(() => useLiveSessionController(config)); let completing!: Promise<void>;
  await act(async () => { completing = h.result.current.handleLiveTurnComplete('Request', 'Reply', undefined, undefined,
    { conversationId: useMaestroStore.getState().settings.selectedLanguagePairId!, handoffId: 'owned-context' }); });
  act(() => useMaestroStore.setState({ messages: [{ id: 'new-draft', role: 'assistant', text: 'Current draft', timestamp: 1 }],
    settings: { ...initialSettings, selectedLanguagePairId: allGeneratedLanguagePairs[1].id } }));
  await act(async () => { resolve(); await completing; });
  expect(ports.suggestions).not.toHaveBeenCalled(); expect(ports.clearDrafts).not.toHaveBeenCalled();
  expect(useMaestroStore.getState().messages.map(message => message.id)).toEqual(['new-draft']);
});


it('opens and restarts microphone-only Live without requesting a camera', async () => {
  const config = createConfig();
  const callbacks = () => ports.live.mock.calls[ports.live.mock.calls.length - 1][0];
  ports.start.mockImplementation(async options => callbacks().onStateChange(options.gateInputOnSpeech ? 'armed' : 'active'));
  const h = renderHook(() => useLiveSessionController(config));
  sessionActivity.requireResume();
  await act(async () => { await h.result.current.handleStartLiveSession(); });
  expect(sessionActivity.isActive()).toBe(true);
  expect(ports.start).toHaveBeenCalledWith(expect.objectContaining({ stream: null, gateAudioAfterConnect: true }));
  expect(config.setLiveVideoStream).not.toHaveBeenCalled();
  await act(async () => { callbacks().onStateChange('idle'); });
  expect(ports.start).toHaveBeenCalledTimes(2);
  expect(ports.start.mock.calls[1][0]).toMatchObject({ stream: undefined, gateInputOnSpeech: true });
  await act(async () => { await h.result.current.handleStopLiveSession(); });
  expect(ports.stop).toHaveBeenCalled();
});

it('does not resume Live while the native app is suspended', async () => {
  const h = renderHook(() => useLiveSessionController(createConfig()));
  try {
    sessionActivity.setSuspended(true);
    await act(async () => { await h.result.current.handleStartLiveSession(); });
    expect(ports.start).not.toHaveBeenCalled();
  } finally {
    await Promise.resolve();
    sessionActivity.setSuspended(false); sessionActivity.resume();
  }
});
