// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SpeechGate } from '../../../../shared/audio/speechGate';
import { ContinuousLiveTurnBoundary } from '../../../core-sdk/media/continuousLiveTurnBoundary';
import { SemanticSpeechCapture } from '../../../core-sdk/media/observerSpeechDetection';
import { LiveInputContext } from '../../../core-sdk/media/liveInputContext';
import { createLiveInputCapture } from './inputCapture';
import { createLiveSessionState } from './state';

beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(10_000); });
afterEach(() => vi.useRealTimers());
const flush = async () => { for (let i = 0; i < 15; i++) await Promise.resolve(); };
const setup = (gated = true) => {
  const state = createLiveSessionState({});
  state.currentSessionIdRef.current = 1;
  if (gated) {
    state.speechGateRef.current = new SpeechGate({ requireConfirmation: true });
    state.speechGateRef.current.openFromConfirmedTrigger(Date.now());
    state.speechTurnBoundaryRef.current = new ContinuousLiveTurnBoundary();
    state.speechTurnBoundaryRef.current.openFromConfirmedSpeech(Date.now());
    state.semanticSpeechCaptureRef.current = new SemanticSpeechCapture({ sampleRate: 16000 });
  }
  const send = vi.fn(); const encode = vi.fn(async () => 'pcm');
  state.sessionRef.current = { sendRealtimeInput: send };
  createLiveInputCapture(state, {
    ensureInputCodecWorker: () => ({ encodePcmToBase64: encode }) as any,
    setVadActivity: vi.fn(), setLocalSpeechTriggerPhase: vi.fn(), emitTurnTranscriptUpdate: vi.fn(),
  }, { sessionId: 1, speechGateEpoch: 0, speechGateEnabled: gated, observerActivity: false, localSpeechTrigger: null, workletNode: null, inputSource: null });
  return { state, send, encode, capture: async () => { await state.pcmCaptureRouterRef.current!.push(new Int16Array(1600).fill(1000), 16000, 'device'); await flush(); } };
};

describe('continuous Live input boundaries', () => {
  it.each([false, true])('waits for native reflection quiet after PCM completion (gated=%s)', async gated => {
    const h = setup(gated), tail = vi.fn(() => true);
    h.state.speechOutputRef.current = { microphonePolicy: 'suppress-during-playback', isMicrophoneSuppressed: tail } as any;
    h.state.playbackPendingRef.current = false; h.state.playbackUntilRef.current = 0;
    await h.capture(); await vi.advanceTimersByTimeAsync(1000); await h.capture();
    expect(h.encode).not.toHaveBeenCalled();
    tail.mockReturnValue(false); await h.capture();
    await vi.advanceTimersByTimeAsync(499); await h.capture(); expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); await h.capture(); expect(h.encode).toHaveBeenCalledOnce();
    h.state.inputPacketizerRef.current!.dispose();
  });
  it('suppresses native-speaker echo in full Live until actual drain and the settling interval', async () => {
    const h = setup(false); h.state.speechOutputRef.current = { microphonePolicy: 'suppress-during-playback' } as any;
    h.state.liveInputContextRef.current = new LiveInputContext();
    h.state.playbackPendingRef.current = true; h.state.playbackUntilRef.current = Date.now() - 1000;
    await h.capture(); await vi.advanceTimersByTimeAsync(1000); await h.capture();
    expect(h.encode).not.toHaveBeenCalled(); expect(h.state.currentUserAudioTotalLengthRef.current).toBe(0);
    expect(h.state.liveInputContextRef.current.finish().audio).toBeUndefined();
    h.state.playbackPendingRef.current = false;
    await h.capture(); await vi.advanceTimersByTimeAsync(499); await h.capture();
    expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); await h.capture();
    expect(h.encode).toHaveBeenCalledOnce(); expect(h.state.currentUserAudioTotalLengthRef.current).toBe(1600);
    h.state.inputPacketizerRef.current!.dispose();
  });

  it('keeps ordinary full-browser Live input continuous with its existing echo cancellation', async () => {
    const h = setup(false); h.state.playbackPendingRef.current = true;
    await h.capture(); expect(h.encode).toHaveBeenCalledOnce();
    h.state.inputPacketizerRef.current!.dispose();
  });

  it('keeps gated input closed beyond the estimated end until the selected output drains', async () => {
    const h = setup(); h.state.playbackUntilRef.current = Date.now() - 1000;
    h.state.playbackPendingRef.current = true;
    await h.capture(); expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1000); await h.capture(); expect(h.encode).not.toHaveBeenCalled();
    h.state.playbackPendingRef.current = false;
    await h.capture(); await vi.advanceTimersByTimeAsync(499); await h.capture();
    expect(h.encode).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); await h.capture();
    expect(h.encode).toHaveBeenCalledOnce(); h.state.inputPacketizerRef.current!.dispose();
  });

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

describe('Live handoff audio provenance', () => {
  it('records only the encoded bytes successfully sent', async () => {
    const h = setup(); const input = new LiveInputContext(() => 0); h.state.liveInputContextRef.current = input;
    h.encode.mockResolvedValue('AAD/fwCA//8=');
    await h.capture();
    expect(h.send).toHaveBeenCalledExactlyOnceWith({ audio: { data: 'AAD/fwCA//8=', mimeType: 'audio/pcm;rate=16000' } });
    const media = input.finish(); expect(atob(media.audio!.data).slice(44)).toBe(atob('AAD/fwCA//8='));
    expect(media.audio!.samples).toBe(4); // Not the synthetic 1600-sample capture before encoding.
    h.state.inputPacketizerRef.current!.dispose();
  });
  it.each(['closed', 'stale', 'failed'])('does not retain %s packets as sent context', async reason => {
    const h = setup(); const input = new LiveInputContext(); h.state.liveInputContextRef.current = input;
    if (reason === 'closed') h.state.inputClosedByServerRef.current = true;
    if (reason === 'stale') h.encode.mockImplementation(async () => { h.state.currentSessionIdRef.current = 2; return 'AAA='; });
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    if (reason === 'failed') h.send.mockImplementation(() => { throw new Error('Closed socket'); });
    await h.capture(); expect(input.finish()).toMatchObject({ complete: false, issue: 'missing', packets: [] });
    h.state.inputPacketizerRef.current!.dispose(); warn.mockRestore();
  });
});
