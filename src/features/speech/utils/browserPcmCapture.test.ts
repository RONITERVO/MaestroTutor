// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { openBrowserPcmCapture } from './browserPcmCapture';

const mocks = vi.hoisted(() => ({ acquire: vi.fn(), flush: vi.fn() }));
vi.mock('../../../platform/browser/sessionActivity', () => ({ acquireUserMedia: mocks.acquire }));
vi.mock('./captureWorkletMessaging', () => ({ flushCaptureWorkletNode: mocks.flush }));
vi.mock('../worklets', () => ({ FLOAT_TO_INT16_PROCESSOR_URL: '/capture.js', FLOAT_TO_INT16_PROCESSOR_NAME: 'capture' }));
const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason: Error) => void;
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};
let contexts: Context[], nodes: Node[];
let stopTrack: ReturnType<typeof vi.fn<() => void>>;
let load: () => Promise<void>;
let closeContext: () => Promise<void>;
class Context {
  state = 'running';
  audioWorklet = { addModule: () => load() };
  source = { connect: vi.fn(), disconnect: vi.fn() };
  createMediaStreamSource = vi.fn(() => this.source);
  close = vi.fn(async () => { await closeContext(); this.state = 'closed'; });
  constructor() { contexts.push(this); }
}
class Node {
  port = { onmessage: null as ((event: { data: unknown }) => void) | null };
  disconnect = vi.fn();
  constructor() { nodes.push(this); }
}
const settle = async () => { for (let i = 0; i < 20; i++) await Promise.resolve(); };
beforeEach(() => {
  vi.resetAllMocks(); contexts = []; nodes = [];
  stopTrack = vi.fn(); load = async () => {}; closeContext = async () => {};
  mocks.acquire.mockResolvedValue({ getTracks: () => [{ stop: stopTrack }] });
  mocks.flush.mockResolvedValue(undefined);
  vi.stubGlobal('AudioContext', Context); vi.stubGlobal('AudioWorkletNode', Node);
});
afterEach(() => vi.unstubAllGlobals());

describe('browser microphone owner', () => {
  it('prepares without delivering, installs one consumer, drains and terminally closes once', async () => {
    const capture = await openBrowserPcmCapture();
    const sink = vi.fn();
    expect(contexts[0].source.connect).not.toHaveBeenCalled();
    await capture.flush(); expect(mocks.flush).not.toHaveBeenCalled();
    capture.start(sink);
    expect(() => capture.start(vi.fn())).toThrow('already has a consumer');
    const callback = nodes[0].port.onmessage!;
    callback({ data: { type: 'flush-complete' } }); callback({ data: new Int16Array() });
    const pcm = new Int16Array([12, -20]); callback({ data: pcm });
    expect(sink).toHaveBeenCalledExactlyOnceWith(pcm);
    const drained = deferred<void>(); mocks.flush.mockReturnValueOnce(drained.promise);
    const first = capture.flush(); expect(capture.flush()).toBe(first);
    callback({ data: new Int16Array([24]) }); drained.resolve(); await first;
    const closed = capture.close(); expect(capture.close()).toBe(closed);
    callback({ data: new Int16Array([30]) });
    expect(sink).toHaveBeenCalledTimes(2);
    expect(nodes[0].port.onmessage).toBeNull(); expect(stopTrack).toHaveBeenCalledOnce();
    await closed; await capture.flush();
    expect(contexts[0].close).toHaveBeenCalledOnce();
    expect(contexts[0].source.disconnect).toHaveBeenCalledOnce();
    expect(() => capture.start(sink)).toThrow('closed');
  });

  it('releases a late cancelled permission result without affecting a replacement owner', async () => {
    const permission = deferred<MediaStream>(); mocks.acquire.mockReturnValueOnce(permission.promise);
    const abort = new AbortController();
    const old = openBrowserPcmCapture({ signal: abort.signal });
    const rejected = expect(old).rejects.toMatchObject({ name: 'AbortError' });
    abort.abort(); await rejected;
    const next = await openBrowserPcmCapture();
    const staleStop = vi.fn();
    permission.resolve({ getTracks: () => [{ stop: staleStop }] } as unknown as MediaStream);
    await settle();
    expect(staleStop).toHaveBeenCalledOnce(); expect(stopTrack).not.toHaveBeenCalled();
    expect(contexts).toHaveLength(1); expect(contexts[0].state).toBe('running');
    await next.close(); expect(stopTrack).toHaveBeenCalledOnce();
  });

  it('cancels pending module loading, waits for context release, and never constructs a late node', async () => {
    const module = deferred<void>(), closing = deferred<void>();
    load = () => module.promise; closeContext = () => closing.promise;
    const abort = new AbortController(); let ended = false;
    const opening = openBrowserPcmCapture({ signal: abort.signal });
    const rejected = expect(opening.finally(() => { ended = true; })).rejects.toMatchObject({ name: 'AbortError' });
    await settle(); expect(contexts).toHaveLength(1);
    abort.abort(); await settle();
    expect(stopTrack).toHaveBeenCalledOnce(); expect(ended).toBe(false);
    closing.resolve(); await rejected;
    module.resolve(); await settle(); expect(nodes).toHaveLength(0);
  });

  it('does not bind the acquisition signal to a successfully transferred owner', async () => {
    const abort = new AbortController();
    const capture = await openBrowserPcmCapture({ signal: abort.signal });
    abort.abort(); expect(stopTrack).not.toHaveBeenCalled();
    capture.start(vi.fn()); await capture.close(); expect(stopTrack).toHaveBeenCalledOnce();
  });

  it('closes every track and context when preparation fails', async () => {
    const other = vi.fn();
    stopTrack.mockImplementation(() => { throw new Error('device gone'); });
    mocks.acquire.mockResolvedValueOnce({ getTracks: () => [{ stop: stopTrack }, { stop: other }] });
    load = async () => { throw new Error('module failed'); };
    await expect(openBrowserPcmCapture()).rejects.toThrow('module failed');
    expect(stopTrack).toHaveBeenCalledOnce(); expect(other).toHaveBeenCalledOnce();
    expect(contexts[0].close).toHaveBeenCalledOnce(); expect(nodes).toHaveLength(0);
  });

  it('closes on connect failure and cannot be restarted', async () => {
    const capture = await openBrowserPcmCapture();
    contexts[0].source.connect.mockImplementation(() => { throw new Error('connect failed'); });
    expect(() => capture.start(vi.fn())).toThrow('connect failed');
    expect(stopTrack).toHaveBeenCalledOnce(); await capture.close();
    expect(contexts[0].close).toHaveBeenCalledOnce();
    expect(() => capture.start(vi.fn())).toThrow('closed');
  });
});
