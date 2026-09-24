// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createBrowserLiveVideo } from './browserVideo';
import { createLiveSessionState } from './state';

const flush = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
let frames: BlobCallback[];
let reads: (() => void)[];
beforeEach(() => {
  vi.useFakeTimers(); frames = []; reads = [];
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: vi.fn() } as any);
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(callback => { frames.push(callback); });
  vi.stubGlobal('FileReader', class {
    result = 'data:image/jpeg;base64,frame'; onloadend: (() => void) | null = null;
    readAsDataURL() { reads.push(() => this.onloadend?.()); }
  });
});
afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); });
const stream = () => ({ active: true }) as MediaStream;
const readyVideo = (source: MediaStream) => {
  const video = document.createElement('video'); video.srcObject = source;
  Object.defineProperties(video, { readyState: { value: 4 }, videoWidth: { value: 1280 }, videoHeight: { value: 720 } });
  return video;
};
const setup = () => {
  const state = createLiveSessionState({});
  state.currentSessionIdRef.current = 1;
  const sendRealtimeInput = vi.fn();
  state.sessionRef.current = { sendRealtimeInput };
  const video = createBrowserLiveVideo(state, { hasCameraConsent: () => true });
  return { state, video, sendRealtimeInput };
};

describe('browser Live video ownership', () => {
  it('does not detach a caller-owned fixed-position video', async () => {
    const h = setup(); const source = stream(); const video = readyVideo(source);
    video.style.position = 'fixed'; document.body.appendChild(video);
    await h.video.updateVideoInput(source, video); await h.video.updateVideoInput(null);
    expect(video.isConnected).toBe(true);
    expect(video.srcObject).toBe(source);
    expect(video.pause).not.toHaveBeenCalled();
  });

  it.each(['play', 'metadata'])('bounds missing %s readiness and releases its hidden video', async missing => {
    const h = setup();
    if (missing === 'play') vi.mocked(HTMLMediaElement.prototype.play).mockReturnValue(new Promise(() => {}));
    const finished = vi.fn();
    const preparation = h.video.updateVideoInput(stream()).then(finished);
    await vi.advanceTimersByTimeAsync(5000);
    expect(finished).toHaveBeenCalledOnce();
    await preparation;
    expect(h.state.captureVideoRef.current).toBeNull();
    expect(document.querySelector('video')).toBeNull();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('a superseded readiness continuation cannot overwrite or detach the newer video', async () => {
    const h = setup(); const first = document.createElement('video');
    const older = h.video.updateVideoInput(stream(), first);
    await flush();
    const source = stream(); const newer = readyVideo(source);
    await h.video.updateVideoInput(source, newer);
    Object.defineProperties(first, { videoWidth: { value: 640 }, videoHeight: { value: 480 } });
    first.dispatchEvent(new Event('loadedmetadata'));
    await older;
    expect(h.state.captureVideoRef.current).toBe(newer);
    await h.video.updateVideoInput(null);
  });

  it.each(['blob', 'read'])('drops an obsolete frame during %s and preserves the new frame lock', async phase => {
    const h = setup(); const first = stream();
    await h.video.updateVideoInput(first, readyVideo(first));
    await vi.advanceTimersByTimeAsync(1000);
    if (phase === 'read') frames[0](new Blob(['old']));
    const second = stream(); await h.video.updateVideoInput(second, readyVideo(second));
    await vi.advanceTimersByTimeAsync(1000);
    expect(frames).toHaveLength(2);
    if (phase === 'blob') frames[0](new Blob(['old']));
    else reads[0]();
    await flush();
    expect(h.sendRealtimeInput).not.toHaveBeenCalled();
    expect(h.state.videoFrameInFlightRef.current).toBe(true);
    frames[1](new Blob(['new'])); reads[reads.length - 1](); await flush();
    expect(h.sendRealtimeInput).toHaveBeenCalledOnce();
    expect(h.state.videoFrameInFlightRef.current).toBe(false);
    await h.video.updateVideoInput(null);
  });

  it('disabling capture discards a frame already being read', async () => {
    const h = setup(); const source = stream();
    await h.video.updateVideoInput(source, readyVideo(source));
    await vi.advanceTimersByTimeAsync(1000); frames[0](new Blob(['old']));
    await h.video.updateVideoInput(null); reads[0](); await flush();
    expect(h.sendRealtimeInput).not.toHaveBeenCalled();
  });
});
