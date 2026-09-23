// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const ports = vi.hoisted(() => ({
  getAi: vi.fn(), connect: vi.fn(), getUserMedia: vi.fn(), decode: vi.fn(), encode: vi.fn(), disposeCodec: vi.fn(),
  flushCapture: vi.fn(), trigger: vi.fn(), acquireWhisper: vi.fn(), releaseWhisper: vi.fn(),
  trackUsage: vi.fn(), flushUsage: vi.fn(), completeLog: vi.fn(), errorLog: vi.fn(),
  tokens: new Set<string>(),
  cameraConsent: true,
}));
vi.mock('../../../api/gemini/client', () => ({ getAi: ports.getAi }));
vi.mock('../../../store', () => ({ useMaestroStore: Object.assign((selector: (state: unknown) => unknown) => selector({
  addActivityToken: (category: string, subtype: string) => { const key = `${category}:${subtype}`; ports.tokens.add(key); return key; },
  removeActivityToken: (token: string) => ports.tokens.delete(token),
}), { getState: () => ({ settings: { selectedCameraId: ports.cameraConsent ? 'camera' : null, sendWithSnapshotEnabled: true, smartReengagement: { useVisualContext: true } } }) }) }));
vi.mock('../../diagnostics', () => ({ debugLogService: { logRequest: () => ({ complete: ports.completeLog, error: ports.errorLog }) } }));
vi.mock('../../../platform/browser/turnTiming', () => ({ beginTurnTiming: () => ({ mark: vi.fn(), markOnce: vi.fn(), markLatest: vi.fn() }) }));
vi.mock('../../../shared/utils/costTracker', () => ({ createLiveUsageTracker: () => ({ trackSnapshot: ports.trackUsage, flush: ports.flushUsage }) }));
vi.mock('../worklets', () => ({ FLOAT_TO_INT16_PROCESSOR_URL: '/capture.js', FLOAT_TO_INT16_PROCESSOR_NAME: 'capture', PCM_PLAYBACK_PROCESSOR_URL: '/playback.js', PCM_PLAYBACK_PROCESSOR_NAME: 'playback' }));
vi.mock('../utils/audioCodecWorkerClient', () => ({ AudioCodecWorkerClient: class {
  decodeBase64ToPcmBuffer = ports.decode; encodePcmToBase64 = ports.encode; dispose = ports.disposeCodec;
} }));
vi.mock('../utils/captureWorkletMessaging', () => ({ flushCaptureWorkletNode: ports.flushCapture }));
vi.mock('../utils/localWhisperClient', () => ({ acquireLocalWhisperClient: ports.acquireWhisper, releaseLocalWhisperClient: ports.releaseWhisper }));
vi.mock('../utils/localSpeechTrigger', () => ({ waitForLocalSpeechTrigger: ports.trigger }));

import { useGeminiLiveConversation, type UseGeminiLiveConversationCallbacks } from './useGeminiLiveConversation';
import { LIVE_OPEN_TRIGGER } from '../../../../shared/liveOpenReason';

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
};
let order: string[];
let contexts: FakeAudioContext[];
let nodes: FakeWorklet[];
let connections: any[];
let sessions: ReturnType<typeof makeSession>[];
let stopTrack: ReturnType<typeof vi.fn>;
const makeSession = () => ({ close: vi.fn(() => { order.push('session.close'); }), sendRealtimeInput: vi.fn() });
class FakeAudioContext {
  state = 'running'; sampleRate: number; destination = {}; baseLatency = 0; outputLatency = 0;
  audioWorklet = { addModule: vi.fn(async () => {}) };
  createMediaStreamSource = vi.fn(() => ({ connect: vi.fn() }));
  close = vi.fn(async () => { order.push(`context.close:${this.sampleRate}`); this.state = 'closed'; });
  constructor(options?: { sampleRate?: number }) { this.sampleRate = options?.sampleRate || 48000; contexts.push(this); }
}
class FakeWorklet {
  port = { onmessage: null as null | ((event: { data: any }) => void), postMessage: vi.fn((message: any) => { order.push(`worklet:${this.name}:${message.type}`); }) };
  connect = vi.fn(); disconnect = vi.fn(() => { order.push(`disconnect:${this.name}`); });
  constructor(_context: unknown, public name: string) { nodes.push(this); }
}
const flush = async () => act(async () => { for (let i = 0; i < 40; i++) await Promise.resolve(); });
const advance = async (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });
function harness() {
  const callbacks = { onStateChange: vi.fn(), onError: vi.fn(), onTurnComplete: vi.fn(), onTurnTranscriptUpdate: vi.fn(), onGoAway: vi.fn() };
  const hook = renderHook((latest: UseGeminiLiveConversationCallbacks) => useGeminiLiveConversation(latest), { initialProps: callbacks });
  return { ...hook, callbacks, start: async (playModelAudio = false) => act(async () => {
    await hook.result.current.start({ liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, systemInstruction: 'Existing instruction', playModelAudio });
  }) };
}

beforeEach(() => {
  vi.resetAllMocks(); vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-23T12:00:00Z'));
  order = []; contexts = []; nodes = []; connections = []; sessions = []; ports.tokens.clear(); ports.cameraConsent = true;
  stopTrack = vi.fn(() => { order.push('track.stop'); });
  vi.stubGlobal('AudioContext', FakeAudioContext); vi.stubGlobal('AudioWorkletNode', FakeWorklet);
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: ports.getUserMedia } });
  ports.getUserMedia.mockResolvedValue({ getTracks: () => [{ stop: stopTrack }] });
  ports.getAi.mockResolvedValue({ live: { connect: ports.connect } });
  ports.connect.mockImplementation(async options => { connections.push(options); const session = makeSession(); sessions.push(session); options.callbacks.onopen(); return session; });
  ports.decode.mockResolvedValue(new Int16Array([100, 200, 300]).buffer);
  ports.encode.mockResolvedValue('AQID');
  ports.flushCapture.mockImplementation(async () => { order.push('capture.flush'); });
  ports.acquireWhisper.mockReturnValue({ initialize: async () => {}, status: 'ready', transcribe: async () => 'Words' });
});
afterEach(async () => { cleanup(); await flush(); vi.useRealTimers(); vi.unstubAllGlobals(); });

describe('actual Live hook lifecycle before session-controller extraction', () => {
  it('preserves the connect configuration and closes resources after capture is flushed', async () => {
    const h = harness(); await h.start();
    expect({ model: connections[0].model, config: connections[0].config, trigger: connections[0].liveOpenReason.trigger }).toMatchSnapshot();
    order = [];
    await act(async () => { await h.result.current.stop(); });
    expect(order).toEqual(['capture.flush', 'disconnect:capture', 'track.stop', 'context.close:16000', 'session.close']);
    expect(h.callbacks.onStateChange.mock.calls.map(call => call[0])).toEqual(['connecting', 'active', 'idle']);
    expect(ports.tokens.size).toBe(0);
  });

  it('stops while connect is pending and closes its eventual session without reviving capture', async () => {
    const pending = deferred<ReturnType<typeof makeSession>>();
    ports.connect.mockImplementation(options => { connections.push(options); return pending.promise; });
    const h = harness(); let starting!: Promise<void>;
    act(() => { starting = h.result.current.start({ liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, playModelAudio: false }); });
    await flush(); expect(connections).toHaveLength(1);
    await act(async () => { await h.result.current.stop(); });
    const lateSession = makeSession();
    await act(async () => { pending.resolve(lateSession); await starting; });
    connections[0].callbacks.onopen(); await flush();
    expect(lateSession.close).toHaveBeenCalledOnce();
    expect(h.callbacks.onStateChange.mock.calls.map(call => call[0])).toEqual(['connecting', 'idle']);
    expect(stopTrack).toHaveBeenCalledOnce();
    expect(nodes[0].port.onmessage).toBeNull();
  });

  it('stops while microphone permission is pending and releases the eventual stream', async () => {
    const pending = deferred<any>(); ports.getUserMedia.mockReturnValue(pending.promise);
    const h = harness(); let starting!: Promise<void>;
    act(() => { starting = h.result.current.start({ liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, playModelAudio: false }); });
    await flush();
    await act(async () => { await h.result.current.stop(); });
    await act(async () => { pending.resolve({ getTracks: () => [{ stop: stopTrack }] }); await starting; });
    expect(stopTrack).toHaveBeenCalledOnce();
    expect(ports.connect).not.toHaveBeenCalled();
    expect(contexts).toHaveLength(0);
  });

  it('ignores stale provider callbacks after a new session starts', async () => {
    const h = harness(); await h.start(); const stale = connections[0].callbacks;
    await h.start(); h.callbacks.onError.mockClear(); h.callbacks.onTurnTranscriptUpdate.mockClear();
    stale.onmessage({ serverContent: { inputTranscription: { text: 'Stale' } } }); stale.onopen(); stale.onerror('Stale error'); stale.onclose();
    await flush(); await advance(60);
    expect(h.callbacks.onError).not.toHaveBeenCalled();
    expect(h.callbacks.onTurnTranscriptUpdate).not.toHaveBeenCalled();
    expect(sessions[1].close).not.toHaveBeenCalled();
    expect(h.callbacks.onStateChange.mock.calls.map(call => call[0])).toEqual(['connecting', 'active', 'connecting', 'active']);
    // Late accounting callbacks still flush the old usage tracker.
    expect(ports.flushUsage).toHaveBeenCalledTimes(2);
  });

  it('cancels pending decoded output on stop before it can enqueue or complete a turn', async () => {
    const decode = deferred<ArrayBuffer>(); ports.decode.mockReturnValue(decode.promise);
    const h = harness(); await h.start(true);
    connections[0].callbacks.onmessage({ serverContent: { modelTurn: { parts: [{ inlineData: { data: 'AQID' } }] }, outputTranscription: { text: 'Hello' }, turnComplete: true } });
    await flush(); expect(ports.decode).toHaveBeenCalledOnce();
    await act(async () => { await h.result.current.stop(); });
    decode.resolve(new Int16Array([1, 2, 3]).buffer); await flush(); await advance(1700);
    expect(nodes.find(node => node.name === 'playback')!.port.postMessage.mock.calls.some(([message]) => message.type === 'push')).toBe(false);
    expect(h.callbacks.onTurnComplete).not.toHaveBeenCalled();
    expect(ports.disposeCodec).toHaveBeenCalledOnce();
  });

  it('retains playback until a worklet drain acknowledgement and hardware tail have elapsed', async () => {
    const h = harness(); await h.start(true);
    connections[0].callbacks.onmessage({ serverContent: { modelTurn: { parts: [{ inlineData: { data: 'AQID' } }] } } });
    await flush(); connections[0].callbacks.onclose(); await flush();
    const playback = nodes.find(node => node.name === 'playback')!;
    const request = playback.port.postMessage.mock.calls.find(([message]) => message.type === 'request-drain')![0];
    expect(contexts[1].close).not.toHaveBeenCalled();
    expect(ports.flushUsage).not.toHaveBeenCalled();
    playback.port.onmessage!({ data: { type: 'drained', requestId: request.requestId } }); await flush();
    await advance(119); expect(contexts[1].close).not.toHaveBeenCalled();
    await advance(1);
    expect(contexts[1].close).toHaveBeenCalledOnce();
    expect(ports.flushUsage).toHaveBeenCalledOnce();
    expect(h.callbacks.onStateChange).toHaveBeenLastCalledWith('idle');
  });

  it('collects late transcripts and usage before finalizing, and invokes the latest callback', async () => {
    const h = harness(); await h.start(); const callback = vi.fn();
    h.rerender({ ...h.callbacks, onTurnComplete: callback });
    connections[0].callbacks.onmessage({ serverContent: { inputTranscription: { text: 'User' }, outputTranscription: { text: 'First' }, turnComplete: true } });
    await flush(); await advance(1400); expect(callback).not.toHaveBeenCalled();
    connections[0].callbacks.onmessage({ usageMetadata: { totalTokenCount: 3 }, serverContent: { outputTranscription: { text: ' late' } } });
    await flush(); await advance(1499); expect(callback).not.toHaveBeenCalled();
    await advance(1);
    expect(callback).toHaveBeenCalledWith('User', 'First late', new Int16Array(), []);
    expect(h.callbacks.onTurnComplete).not.toHaveBeenCalled();
    expect(ports.trackUsage).toHaveBeenCalledWith({ totalTokenCount: 3 });
    expect(ports.flushUsage).toHaveBeenCalledOnce();
    expect(h.callbacks.onTurnTranscriptUpdate.mock.calls.map(([update]) => update)).toMatchSnapshot();
  });

  it('transfers confirmed pre-connect capture once and keeps the same microphone graph', async () => {
    const input = new FakeAudioContext({ sampleRate: 16000 }); const node = new FakeWorklet(input, 'capture');
    const transfer = vi.fn(() => ({ bufferedPackets: 1, bufferedSamples: 1600 }));
    ports.trigger.mockResolvedValue({ transcript: 'Local words', pcm: new Int16Array(1600).fill(500), microphoneStream: { getTracks: () => [{ stop: stopTrack }] }, capture: { audioContext: input, workletNode: node, transferTo: transfer } });
    const h = harness();
    await act(async () => { await h.result.current.start({ liveOpenTrigger: LIVE_OPEN_TRIGGER.WHISPER_OBSERVER, gateInputOnSpeech: true, playModelAudio: false }); });
    expect(ports.getUserMedia).not.toHaveBeenCalled();
    expect(contexts).toHaveLength(1); expect(nodes).toHaveLength(1);
    expect(transfer).toHaveBeenCalledOnce();
    expect(sessions[0].sendRealtimeInput).toHaveBeenCalledWith({ activityStart: {} });
    expect(h.callbacks.onTurnTranscriptUpdate.mock.calls.some(([update]) => update.userText.includes('Local words'))).toBe(false);
    await act(async () => { await h.result.current.stop(); });
    expect(ports.flushCapture).not.toHaveBeenCalled();
    expect(stopTrack).toHaveBeenCalledOnce();
    expect(input.close).toHaveBeenCalledOnce();
  });

  it('keeps a supplied video element attached and rechecks camera consent before sending encoded frames', async () => {
    const video = document.createElement('video'); const stream = { active: true } as MediaStream;
    video.srcObject = stream;
    Object.defineProperties(video, { readyState: { value: 4 }, videoWidth: { value: 1280 }, videoHeight: { value: 720 } });
    document.body.appendChild(video);
    const drawing = { drawImage: vi.fn(), imageSmoothingEnabled: false, imageSmoothingQuality: '' };
    const contextMock = vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(drawing as unknown as CanvasRenderingContext2D);
    let encodeFrame!: BlobCallback;
    let finishRead!: () => void;
    vi.stubGlobal('FileReader', class {
      result = 'data:image/jpeg;base64,ZnJhbWU=';
      onloadend: (() => void) | null = null;
      readAsDataURL() { finishRead = () => { this.onloadend?.(); }; }
    });
    const blobMock = vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(callback => { encodeFrame = callback; });
    const pause = vi.spyOn(video, 'pause').mockImplementation(() => {});
    const h = harness();
    await act(async () => { await h.result.current.start({ liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, playModelAudio: false, stream, videoElement: video }); });
    await advance(1000);
    expect(drawing.drawImage).toHaveBeenCalledWith(video, 0, 0, 640, 360);
    encodeFrame(new Blob(['frame'], { type: 'image/jpeg' })); finishRead(); await flush();
    expect(sessions[0].sendRealtimeInput).toHaveBeenCalledWith({ video: { data: 'ZnJhbWU=', mimeType: 'image/jpeg' } });
    sessions[0].sendRealtimeInput.mockClear();
    await advance(1000);
    encodeFrame(new Blob(['frame'], { type: 'image/jpeg' }));
    ports.cameraConsent = false;
    finishRead(); await flush();
    expect(sessions[0].sendRealtimeInput.mock.calls.some(([input]) => input.video)).toBe(false);
    await act(async () => { await h.result.current.stop(); });
    expect(video.srcObject).toBe(stream); expect(document.body.contains(video)).toBe(true); expect(pause).not.toHaveBeenCalled();
    video.remove(); contextMock.mockRestore(); blobMock.mockRestore(); pause.mockRestore();
  });

  it('treats go-away as an advance notice and leaves the transport open', async () => {
    const h = harness(); await h.start();
    connections[0].callbacks.onmessage({ goAway: { timeLeft: '10s' } }); await flush();
    expect(h.callbacks.onGoAway).toHaveBeenCalledWith({ timeLeft: '10s' });
    expect(sessions[0].close).not.toHaveBeenCalled();
    expect(stopTrack).not.toHaveBeenCalled();
  });
});
