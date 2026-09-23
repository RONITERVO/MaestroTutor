// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SpeechGate } from '../../../../shared/audio/speechGate';
import { ContinuousLiveTurnBoundary } from '../../../core-sdk/media/continuousLiveTurnBoundary';
import { SemanticSpeechCapture } from '../../../core-sdk/media/observerSpeechDetection';
import { createLiveInputCapture } from './inputCapture';
import { createLiveSessionState } from './state';

beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(10_000); });
afterEach(() => vi.useRealTimers());
const flush = async () => { for (let i = 0; i < 15; i++) await Promise.resolve(); };
const setup = () => {
  const state = createLiveSessionState({});
  state.currentSessionIdRef.current = 1;
  state.speechGateRef.current = new SpeechGate({ requireConfirmation: true });
  state.speechGateRef.current.openFromConfirmedTrigger(Date.now());
  state.speechTurnBoundaryRef.current = new ContinuousLiveTurnBoundary();
  state.speechTurnBoundaryRef.current.openFromConfirmedSpeech(Date.now());
  state.semanticSpeechCaptureRef.current = new SemanticSpeechCapture({ sampleRate: 16000 });
  const send = vi.fn(); const encode = vi.fn(async () => 'pcm');
  state.sessionRef.current = { sendRealtimeInput: send };
  createLiveInputCapture(state, {
    ensureInputCodecWorker: () => ({ encodePcmToBase64: encode }) as any,
    setVadActivity: vi.fn(), setLocalSpeechTriggerPhase: vi.fn(), emitTurnTranscriptUpdate: vi.fn(),
  }, { sessionId: 1, speechGateEpoch: 0, speechGateEnabled: true, observerActivity: false, localSpeechTrigger: null, workletNode: null, inputSource: null });
  return { state, send, encode, capture: async () => { await state.pcmCaptureRouterRef.current!.push(new Int16Array(1600).fill(1000), 16000, 'device'); await flush(); } };
};

describe('continuous Live input boundaries', () => {
  it('preserves the playback settling interval even inside an open continuous turn', async () => {
    const h = setup(); h.state.playbackUntilRef.current = Date.now() + 10;
    await h.capture(); expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(11); await h.capture();
    expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(499); await h.capture();
    expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); await h.capture();
    expect(h.send).toHaveBeenCalledExactlyOnceWith({ audio: { data: 'pcm', mimeType: 'audio/pcm;rate=16000' } });
    h.state.inputPacketizerRef.current!.dispose();
  });

  it('counts a successfully sent continuous turn end once', async () => {
    const h = setup();
    await vi.advanceTimersByTimeAsync(4000); await h.capture();
    await h.state.boundaryClosePromiseRef.current;
    await h.capture();
    expect(h.send).toHaveBeenCalledExactlyOnceWith({ activityEnd: {} });
    expect(h.state.inputAudioTelemetryRef.current.audioStreamEnds).toBe(1);
    h.state.inputPacketizerRef.current!.dispose();
  });
});
