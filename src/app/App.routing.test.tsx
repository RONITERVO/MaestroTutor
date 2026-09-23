// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { GeminiLiveSttTurnComplete } from '../features/speech';

// Capture real App handoffs while replacing feature/hardware endpoints. Store,
// routing decisions and idle arbitration remain real throughout the extraction.
const ports = vi.hoisted(() => ({
  initConfig: {} as any, speechConfig: {} as any, smartConfig: {} as any, observerConfig: {} as any,
  chatProps: {} as any, chatConfig: {} as any,
  initialize: vi.fn(), speech: {} as any, conversation: {} as any, camera: {} as any,
  stopListening: vi.fn(), startListening: vi.fn(), clearTranscript: vi.fn(), stopSpeaking: vi.fn(),
  send: vi.fn(), createSuggestion: vi.fn(), capture: vi.fn(), stopObserver: vi.fn(), resetObserver: vi.fn(),
  startLive: vi.fn(), stopLive: vi.fn(), liveTurn: vi.fn(), schedule: vi.fn(), cancel: vi.fn(),
  userActivity: vi.fn(), saveSettings: vi.fn(), setKeyError: vi.fn(),
  sending: { current: false }, speaking: { current: false }, stream: { current: null as any },
}));
vi.mock('../features/chat', () => ({
  ChatInterface: (props: unknown) => { ports.chatProps = props; return null; },
  useTutorConversation: (config: unknown) => { ports.chatConfig = config; return ports.conversation; },
  useSuggestions: vi.fn(), useChatPersistence: vi.fn(), setChatMetaDB: vi.fn(),
}));
vi.mock('../features/speech', () => ({
  useSpeechOrchestrator: (config: unknown) => { ports.speechConfig = config; return ports.speech; },
}));
vi.mock('../features/session', () => ({
  ApiKeyGate: () => null, Header: () => null, setAppSettingsDB: ports.saveSettings, getAppSettingsDB: vi.fn(),
  useSmartReengagement: (config: unknown) => {
    ports.smartConfig = config;
    return { reengagementPhase: 'idle', scheduleReengagement: ports.schedule, cancelReengagement: ports.cancel, handleUserActivity: ports.userActivity };
  },
}));
vi.mock('../features/live', () => ({
  useLiveSessionController: () => ({
    handleStartLiveSession: ports.startLive, handleStopLiveSession: ports.stopLive,
    handleLiveTurnComplete: ports.liveTurn, handleLiveTurnTranscriptUpdate: vi.fn(),
  }),
  useSilentObserverController: (config: unknown) => {
    ports.observerConfig = config;
    return { stopSilentObserver: ports.stopObserver, resetSilentObserver: ports.resetObserver };
  },
}));
vi.mock('../features/vision', () => ({ useCameraManager: () => ports.camera, VisualContextVideo: () => null }));
vi.mock('../features/theme', () => ({ useApplyCustomColors: vi.fn() }));
vi.mock('../features/diagnostics', () => ({ DebugLogPanel: () => null }));
vi.mock('../shared/hooks/useApiKey', () => ({ useApiKey: () => ({ hasKey: true, isLoading: false, setError: ports.setKeyError }) }));
vi.mock('../shared/hooks/useManagedAccess', () => ({ useManagedAccess: () => ({ hasManagedAccess: false, isLoading: false }) }));
vi.mock('./hooks', async () => ({
  useAppInitialization: (config: unknown) => { ports.initConfig = config; return ports.initialize(); },
  useMaestroActivityStage: vi.fn(),
  useIdleReengagement: (await import('./hooks/useIdleReengagement')).useIdleReengagement,
}));

import { allGeneratedLanguagePairs, initialSettings, useMaestroStore } from '../store';
import { selectSelectedLanguagePair } from '../store/slices/settingsSlice';
import App from './App';

const pair = allGeneratedLanguagePairs.find(value => value.nativeLanguageCode.startsWith('en') && value.targetLanguageCode.startsWith('es'))!;
const nextPair = allGeneratedLanguagePairs.find(value => value.nativeLanguageCode.startsWith('en') && value.targetLanguageCode.startsWith('fi'))!;
let events: string[];
const turn = (overrides: Partial<GeminiLiveSttTurnComplete> = {}): GeminiLiveSttTurnComplete => ({
  turnId: 1, turnTranscript: '  hola  ', committedTranscript: 'hola', inputTranscript: '', outputTranscript: '', audioSamples: 100, destination: 'message', ...overrides,
});
const deferred = () => {
  let resolve!: (value?: unknown) => void;
  const promise = new Promise(done => { resolve = done; });
  return { promise, resolve };
};
const setStt = (patch: Partial<typeof initialSettings.stt>) => {
  const state = useMaestroStore.getState();
  useMaestroStore.setState({ settings: { ...state.settings, stt: { ...state.settings.stt, ...patch } } });
};
beforeEach(() => {
  vi.resetAllMocks();
  vi.useFakeTimers();
  events = [];
  ports.sending.current = false; ports.speaking.current = false; ports.stream.current = null;
  useMaestroStore.setState({
    settings: { ...initialSettings, selectedLanguagePairId: pair.id, isSuggestionMode: false, stt: { ...initialSettings.stt, enabled: true, language: 'es' } },
    activityTokens: new Set(), liveSessionState: 'idle', isLoadingHistory: false, isUserActive: false,
    replySuggestions: [], lastFetchedSuggestionsFor: 'old', attachedImageBase64: null, attachedImageMimeType: null,
    sttError: null, showDebugLogs: false,
  });
  ports.initialize.mockImplementation(() => {
    const settings = useMaestroStore(state => state.settings);
    const isLoadingHistory = useMaestroStore(state => state.isLoadingHistory);
    const replySuggestions = useMaestroStore(state => state.replySuggestions);
    return { ...useMaestroStore.getState(), settings, isLoadingHistory, replySuggestions, t: (key: string) => key, selectedLanguagePair: selectSelectedLanguagePair(useMaestroStore.getState()) };
  });
  for (const [name, method] of [
    ['stop-listening', ports.stopListening], ['clear-transcript', ports.clearTranscript], ['stop-speaking', ports.stopSpeaking],
    ['stop-observer', ports.stopObserver], ['reset-observer', ports.resetObserver], ['start-live', ports.startLive],
    ['stop-live', ports.stopLive], ['live-turn', ports.liveTurn], ['cancel', ports.cancel],
  ] as const) method.mockImplementation(() => { events.push(name); });
  ports.startListening.mockImplementation((language: string) => { events.push(`listen:${language}`); });
  ports.createSuggestion.mockImplementation(async (text: string) => { events.push(`translate:${text}`); });
  ports.send.mockImplementation(async (_text, _image, _mime, type) => { events.push(`send:${type}`); return true; });
  ports.capture.mockImplementation(async () => { events.push('capture'); return { base64: 'frame', mimeType: 'image/png' }; });
  ports.saveSettings.mockResolvedValue(undefined);
  ports.speech = {
    isSpeaking: false, isListening: false, transcript: '', isSpeechSynthesisSupported: true,
    stopSpeaking: ports.stopSpeaking, startListening: ports.startListening, stopListening: ports.stopListening,
    clearTranscript: ports.clearTranscript, speechIsSpeakingRef: ports.speaking, hasPendingQueueItems: () => false,
    claimRecordedUtterance: vi.fn(), speakMessage: vi.fn(), speakWrapper: vi.fn(),
  };
  ports.conversation = {
    isSending: false, isSendingRef: ports.sending, handleSendMessageInternal: ports.send,
    handleSendMessageInternalRef: { current: ports.send }, handleCreateSuggestion: ports.createSuggestion,
    handleSuggestionInteraction: vi.fn(), setMaestroActivityStage: vi.fn(), fetchAndSetReplySuggestions: vi.fn(),
  };
  ports.camera = {
    availableCamerasRef: { current: [] }, liveVideoStream: null, setLiveVideoStream: vi.fn(),
    visualContextVideoRef: { current: null }, visualContextStreamRef: ports.stream,
    setSnapshotUserError: vi.fn(), captureSnapshot: ports.capture,
  };
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks(); });
const mount = () => { const app = render(<App />); events = []; return app; };

describe('actual App speech and idle routing (baseline before extraction)', () => {
  it('sends trimmed STT text and the current attachment through the existing send boundary', async () => {
    mount();
    act(() => useMaestroStore.setState({ attachedImageBase64: 'photo', attachedImageMimeType: 'image/png' }));
    await act(async () => { await ports.speechConfig.onSttTurnComplete(turn()); });
    expect(ports.send).toHaveBeenCalledExactlyOnceWith('hola', 'photo', 'image/png', 'user', { triggeredByStt: true });
    expect(ports.createSuggestion).not.toHaveBeenCalled();
    expect(ports.stopListening).not.toHaveBeenCalled();
  });

  it.each(['short', 'disabled', 'response', 'speaking'])('ignores an STT completion when %s', async reason => {
    mount();
    act(() => {
      if (reason === 'disabled') setStt({ enabled: false });
      if (reason === 'response') useMaestroStore.setState({ activityTokens: new Set(['gen:response']) });
      if (reason === 'speaking') useMaestroStore.setState({ activityTokens: new Set(['tts:speak']) });
    });
    await act(async () => { await ports.speechConfig.onSttTurnComplete(turn(reason === 'short' ? { turnTranscript: 'x' } : {})); });
    expect(ports.send).not.toHaveBeenCalled();
    expect(ports.createSuggestion).not.toHaveBeenCalled();
  });

  it('retains captured translation destination after the UI changes and rereads STT language before restart', async () => {
    mount();
    ports.createSuggestion.mockImplementation(async (text: string) => { events.push(`translate:${text}`); setStt({ language: 'fi' }); });
    await act(async () => { await ports.speechConfig.onSttTurnComplete(turn({ destination: 'translation' })); });
    expect(events.filter(value => value !== 'cancel')).toEqual(['stop-listening', 'clear-transcript', 'translate:hola', 'listen:fi']);
    expect(ports.send).not.toHaveBeenCalled();
  });

  it('uses committed text and the current translation view as compatibility fallbacks', async () => {
    const state = useMaestroStore.getState();
    useMaestroStore.setState({ settings: { ...state.settings, isSuggestionMode: true } });
    mount();
    await act(async () => { await ports.speechConfig.onSttTurnComplete(turn({ turnTranscript: '', committedTranscript: '  translate this  ' })); });
    expect(ports.createSuggestion).toHaveBeenCalledExactlyOnceWith('translate this');
    expect(ports.send).not.toHaveBeenCalled();
  });

  it.each(['stt-disabled', 'response', 'speaking', 'listening'])('does not restart STT when translation finishes during %s', async state => {
    mount();
    ports.createSuggestion.mockImplementation(async () => {
      if (state === 'stt-disabled') setStt({ enabled: false });
      else useMaestroStore.setState({ activityTokens: new Set([state === 'response' ? 'gen:response' : state === 'speaking' ? 'tts:speak' : 'stt:listen']) });
    });
    await act(async () => { await ports.speechConfig.onSttTurnComplete(turn({ destination: 'translation' })); });
    expect(ports.startListening).not.toHaveBeenCalled();
  });

  it('continues translation after a stop error, but propagates translation failure without restarting', async () => {
    mount();
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    ports.stopListening.mockRejectedValue(new Error('already stopped'));
    ports.createSuggestion.mockRejectedValue(new Error('translation failed'));
    await act(async () => { await expect(ports.speechConfig.onSttTurnComplete(turn({ destination: 'translation' }))).rejects.toThrow('translation failed'); });
    expect(ports.clearTranscript).toHaveBeenCalledOnce();
    expect(ports.createSuggestion).toHaveBeenCalledExactlyOnceWith('hola');
    expect(ports.startListening).not.toHaveBeenCalled();
    expect(ports.send).not.toHaveBeenCalled();
  });

  it.each(['history', 'sending', 'speaking'])('does not start reengagement while %s is busy', async busy => {
    mount();
    act(() => {
      if (busy === 'history') useMaestroStore.setState({ isLoadingHistory: true });
      if (busy === 'sending') ports.sending.current = true;
      if (busy === 'speaking') ports.speaking.current = true;
    });
    await act(async () => { await ports.smartConfig.triggerReengagementSequence(); });
    expect(ports.stopObserver).not.toHaveBeenCalled();
    expect(ports.send).not.toHaveBeenCalled();
  });

  it('stops observer, clears suggestions and falls back from visual to conversational reengagement', async () => {
    ports.stream.current = { active: true };
    const state = useMaestroStore.getState();
    useMaestroStore.setState({ settings: { ...state.settings, smartReengagement: { ...state.settings.smartReengagement, useVisualContext: true } }, replySuggestions: [{ target: 'old', native: 'old' }] });
    mount();
    ports.send.mockImplementation(async (_text, _image, _mime, type) => { events.push(`send:${type}`); return type !== 'image-reengagement'; });
    await act(async () => { await ports.smartConfig.triggerReengagementSequence(); });
    expect(events).toEqual(['stop-observer', 'capture', 'send:image-reengagement', 'send:conversational-reengagement']);
    expect(ports.send.mock.calls).toEqual([
      ['', 'frame', 'image/png', 'image-reengagement'], ['', undefined, undefined, 'conversational-reengagement'],
    ]);
    expect(useMaestroStore.getState()).toMatchObject({ replySuggestions: [], lastFetchedSuggestionsFor: null });
  });

  it('blocks overlapping visual capture and clears its busy flag even when capture throws', async () => {
    ports.stream.current = { active: true };
    const state = useMaestroStore.getState();
    useMaestroStore.setState({ settings: { ...state.settings, smartReengagement: { ...state.settings.smartReengagement, useVisualContext: true } } });
    mount();
    const capture = deferred();
    ports.capture.mockReturnValueOnce(capture.promise);
    await act(async () => {
      const first = ports.smartConfig.triggerReengagementSequence();
      await Promise.resolve();
      await ports.smartConfig.triggerReengagementSequence();
      capture.resolve(null);
      await first;
    });
    expect(ports.capture).toHaveBeenCalledOnce();
    ports.capture.mockRejectedValueOnce(new Error('camera failed'));
    await act(async () => { await expect(ports.smartConfig.triggerReengagementSequence()).rejects.toThrow('camera failed'); });
    await act(async () => { await ports.smartConfig.triggerReengagementSequence(); });
    expect(ports.capture).toHaveBeenCalledTimes(3);
  });

  it('waits for both Live systems before loading history even if one stop fails', async () => {
    mount();
    ports.resetObserver.mockRejectedValue(new Error('observer stop failed'));
    const liveStop = deferred(); ports.stopLive.mockReturnValue(liveStop.promise);
    let completed = false;
    const waiting = ports.initConfig.waitForConversationSystemsIdle().then(() => { completed = true; });
    await Promise.resolve();
    expect(completed).toBe(false);
    expect(ports.stopLive).toHaveBeenCalledWith({ scheduleReengagement: false });
    liveStop.resolve(); await waiting;
    expect(completed).toBe(true);
  });

  it('manual Live starts after observer stop, and observer completion schedules only after turn persistence', async () => {
    mount();
    await act(async () => { await ports.chatProps.onStartLiveSession(); });
    expect(events).toEqual(['stop-observer', 'start-live']);
    const persisted = deferred(); ports.liveTurn.mockReturnValue(persisted.promise);
    ports.schedule.mockClear();
    const completion = ports.observerConfig.onTurnComplete('user', 'model');
    expect(ports.schedule).not.toHaveBeenCalled();
    persisted.resolve(); await completion;
    expect(ports.schedule).toHaveBeenCalledExactlyOnceWith('silent-observer-response');
  });

  it('switches translation language, stops listening and delays restart for 250ms', async () => {
    ports.speech.isListening = true;
    mount();
    act(() => { ports.chatProps.onToggleSuggestionMode(true); });
    expect(useMaestroStore.getState().settings).toMatchObject({ isSuggestionMode: true, stt: { language: 'en-US' } });
    expect(ports.stopListening).toHaveBeenCalledOnce();
    await act(async () => { await vi.advanceTimersByTimeAsync(249); });
    expect(ports.startListening).not.toHaveBeenCalled();
    await act(async () => { await vi.advanceTimersByTimeAsync(1); });
    expect(ports.startListening).toHaveBeenCalledExactlyOnceWith('en-US');
    expect(ports.clearTranscript).toHaveBeenCalledOnce();
  });

  it('master STT toggle clears errors and orders observer stop before enabling capture', async () => {
    setStt({ enabled: false });
    useMaestroStore.setState({ sttError: 'old error' });
    mount();
    await act(async () => { await ports.chatProps.onSttToggle(); });
    expect(events).toEqual(['stop-observer', 'clear-transcript', 'listen:es']);
    expect(useMaestroStore.getState()).toMatchObject({ sttError: null, settings: { stt: { enabled: true } } });
    await act(async () => { await ports.chatProps.onSttToggle(); });
    expect(ports.stopListening).toHaveBeenCalledOnce();
    expect(useMaestroStore.getState().settings.stt.enabled).toBe(false);
  });

  it('language change stops speech and both Live systems, then restarts in the latest language', async () => {
    mount();
    await act(async () => {
      const state = useMaestroStore.getState();
      useMaestroStore.setState({ settings: { ...state.settings, selectedLanguagePairId: nextPair.id, stt: { ...state.settings.stt, language: 'fi' } } });
    });
    expect(events.filter(event => event !== 'cancel')).toEqual(['stop-speaking', 'stop-listening', 'clear-transcript', 'reset-observer', 'stop-live']);
    expect(ports.stopLive).toHaveBeenCalledWith({ scheduleReengagement: false });
    await act(async () => { await vi.advanceTimersByTimeAsync(250); });
    expect(ports.startListening).toHaveBeenCalledExactlyOnceWith('fi');
  });

  it.each(['unmount', 'live-active', 'disabled'])('suppresses delayed language restart after %s', async reason => {
    const app = mount();
    await act(async () => {
      const state = useMaestroStore.getState();
      useMaestroStore.setState({ settings: { ...state.settings, selectedLanguagePairId: nextPair.id } });
    });
    if (reason === 'unmount') app.unmount();
    else act(() => {
      if (reason === 'disabled') setStt({ enabled: false });
      else useMaestroStore.setState({ liveSessionState: 'active' });
    });
    await act(async () => { await vi.advanceTimersByTimeAsync(250); });
    expect(ports.startListening).not.toHaveBeenCalled();
  });

  it('idle scheduling responds to user/busy state and resumes when activity clears', () => {
    mount();
    expect(ports.schedule).toHaveBeenCalledWith('became-idle');
    ports.schedule.mockClear(); ports.cancel.mockClear();
    act(() => useMaestroStore.setState({ activityTokens: new Set(['tts:speak']) }));
    expect(ports.cancel).toHaveBeenCalledOnce();
    act(() => useMaestroStore.setState({ activityTokens: new Set(), isUserActive: true }));
    expect(ports.schedule).not.toHaveBeenCalled();
    act(() => useMaestroStore.setState({ isUserActive: false }));
    expect(ports.schedule).toHaveBeenCalledExactlyOnceWith('became-idle');
  });
});
