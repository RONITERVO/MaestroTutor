// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ speak: vi.fn(), stop: vi.fn(), start: vi.fn() }));
vi.mock('./useBrowserSpeech', () => ({ default: () => ({
  speak: ports.speak, stopSpeaking: ports.stop, startListening: ports.start, stopListening: vi.fn(),
  isSpeaking: false, isListening: false, transcript: '', speechPreviewProgress: 0, sttError: null,
  clearTranscript: vi.fn(), claimRecordedUtterance: vi.fn(), hasPendingQueueItems: () => false,
  isSpeechSynthesisSupported: true, isSpeechRecognitionSupported: true, speakingUtteranceText: null,
}) }));
import { useSpeechOrchestrator } from './useSpeechOrchestrator';
import { useMaestroStore, initialSettings, allGeneratedLanguagePairs } from '../../../store';
import { getPrimaryCode } from '../../../shared/utils/languageUtils';
import type { ChatMessage } from '../../../core/types';
const pair = allGeneratedLanguagePairs.find(item => item.nativeLanguageCode.startsWith('en') && item.targetLanguageCode.startsWith('es'))!;
beforeEach(() => {
  vi.clearAllMocks(); useMaestroStore.setState({ messages: [], activityTokens: new Set(),
    settings: { ...initialSettings, selectedLanguagePairId: pair.id, tts: { ...initialSettings.tts, voiceName: 'Kore' } } });
});
afterEach(cleanup);
it.each([true, false])('reads an agent result using ordinary message voice, languages and cache; native=%s', speakNative => {
  useMaestroStore.setState({ settings: { ...useMaestroStore.getState().settings, tts: { ...initialSettings.tts, speakNative } } });
  const result: ChatMessage = { id: 'agent-result', role: 'assistant', timestamp: 1,
    translations: [{ target: 'Listo.', native: 'Ready.' }], agentTask: { id: 'agent-result', phase: 'completed', note: 'Finished.' } };
  useMaestroStore.setState({ messages: [result] });
  const cache = vi.fn();
  const h = renderHook(() => useSpeechOrchestrator({ upsertMessageTtsCache: cache, upsertSuggestionTtsCache: vi.fn() }));
  act(() => h.result.current.speakMessage(result));
  const [parts, language, trigger] = ports.speak.mock.calls[0];
  expect(parts.map((part: any) => part.text)).toEqual(speakNative ? ['Listo.', 'Ready.'] : ['Listo.']);
  expect(language).toBe(getPrimaryCode(pair.targetLanguageCode)); expect(trigger).toBe('voice.tts-auto-message');
  expect(parts[0]).toMatchObject({ voiceName: 'Kore', context: { source: 'message', messageId: 'agent-result' } });
  parts[0].onAudioCached('data:audio/wav;base64,synthetic');
  expect(cache).toHaveBeenCalledWith('agent-result', expect.objectContaining({ provider: 'gemini-live', voiceName: 'Kore', audioDataUrl: 'data:audio/wav;base64,synthetic' }));
});
