// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import native from '../../../test-fixtures/browser/roomCapture.json';
import { CameraBookClient } from './cameraBookBridge';
import { cameraFrameState, VIRTUAL_SCENE_CAMERA_ID } from '../browser/cameraSources';
import { sessionActivity } from '../browser/sessionActivity';
let client: CameraBookClient, clock: number, revision: number, draw: ReturnType<typeof vi.fn>;
let decodes: Array<(image: HTMLImageElement) => void>;
const image = () => ({ width: 512, height: 384 }) as HTMLImageElement;
const publish = (extra: Record<string, unknown> = {}) => client.receive({ version: 1, session: client.snapshot().session, host: 'a'.repeat(32), revision: ++revision, sources: ['maestro-camera:virtual-scene'], sourceId: 'maestro-camera:virtual-scene', status: 'ready', ...extra });
const frame = (capturedAt = new Date().toISOString()) => ({ sourceId: 'maestro-camera:virtual-scene', capture: { ...native.capture, capturedAt }, data: native.data });
beforeEach(() => {
 vi.useFakeTimers(); clock = 100; revision = 0; decodes = []; draw = vi.fn(); sessionActivity.resume();
 vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage: draw, clearRect: vi.fn() } as any);
 Object.defineProperty(HTMLCanvasElement.prototype, 'captureStream', { configurable: true, value: () => {
   const track = Object.assign(new EventTarget(), { readyState: 'live', requestFrame: vi.fn(), stop() { track.readyState = 'ended'; } });
   return { get active() { return track.readyState === 'live'; }, getVideoTracks: () => [track], getTracks: () => [track] };
 }});
 client = new CameraBookClient(() => clock, () => new Promise(resolve => decodes.push(resolve)));
});
afterEach(() => { client.dispose(); vi.restoreAllMocks(); vi.useRealTimers(); });
it('advertises a source only after native readiness and acquires no pixels until selected', async () => {
 expect(client.devices()).toEqual([]); expect(client.snapshot().requestId).toBe(''); publish();
 expect(client.devices()[0].deviceId).toBe(VIRTUAL_SCENE_CAMERA_ID); expect(decodes).toHaveLength(0);
 const pending = client.acquire(VIRTUAL_SCENE_CAMERA_ID); const request = client.snapshot();
 publish({ requestId: request.requestId, frame: frame() }); expect(client.snapshot().acknowledged).toBe('');
 decodes[0](image()); const stream = await pending;
 expect(draw).toHaveBeenCalledOnce(); expect(cameraFrameState(stream)).toEqual({ origin: 'virtual-scene', fresh: true });
 expect(client.snapshot().acknowledged).toBe(native.capture.captureId);
 stream.getTracks()[0].stop(); expect(client.snapshot().requestId).toBe(''); expect(cameraFrameState(stream)?.fresh).toBe(false);
});
it.each(['session', 'request', 'stale', 'hash'])('rejects %s frames and bounds missing first-frame waits', async reason => {
 publish(); const pending = client.acquire(VIRTUAL_SCENE_CAMERA_ID); const rejected = expect(pending).rejects.toThrow('stopped');
 const value: any = { requestId: client.snapshot().requestId, frame: frame() };
 if (reason === 'session') value.session = 'b'.repeat(32);
 if (reason === 'request') value.requestId = 'b'.repeat(32);
 if (reason === 'stale') value.frame.capture.capturedAt = new Date(Date.now() - 5000).toISOString();
 if (reason === 'hash') value.frame.data = 'bad';
 publish(value); expect(decodes).toHaveLength(0); clock += 10100; await vi.advanceTimersByTimeAsync(200); await rejected;
});
it('never revives a stream from a late decode after suspension', async () => {
 publish(); const pending = client.acquire(VIRTUAL_SCENE_CAMERA_ID); const rejected = expect(pending).rejects.toThrow('stopped');
 publish({ requestId: client.snapshot().requestId, frame: frame() }); client.suspend(); decodes[0](image()); await rejected; await Promise.resolve();
 expect(draw).not.toHaveBeenCalled(); expect(client.snapshot().requestId).toBe('');
});
it('does not stop a newly selected lease for an older failure', async () => {
 publish(); const first = client.acquire(VIRTUAL_SCENE_CAMERA_ID); const rejected = expect(first).rejects.toThrow('stopped'); const old = client.snapshot().requestId;
 const next = client.acquire(VIRTUAL_SCENE_CAMERA_ID); const stopped = expect(next).rejects.toThrow('stopped'); await rejected;
 publish({ requestId: old, status: 'failed' }); expect(client.snapshot().requestId).not.toBe(''); client.suspend(); await stopped;
});
it('expires displayed frames even when the native host continues sending ready messages', async () => {
 publish(); const pending = client.acquire(VIRTUAL_SCENE_CAMERA_ID); publish({ requestId: client.snapshot().requestId, frame: frame() }); decodes[0](image()); const stream = await pending;
 const ended = vi.fn(); stream.getVideoTracks()[0].addEventListener('ended', ended);
 clock += 2100; publish(); expect(cameraFrameState(stream)?.fresh).toBe(false);
 clock += 1001; publish(); await vi.advanceTimersByTimeAsync(200); expect(ended).toHaveBeenCalledOnce(); expect(stream.active).toBe(false);
});

it.each(['headset-camera', 'mixed-view'] as const)('keeps %s and virtual sources distinct and requires re-selection after permission', async origin => {
 const physical = 'maestro-camera:' + origin;
 publish({ sources: [VIRTUAL_SCENE_CAMERA_ID, physical] });
 expect(client.devices().map(device => device.deviceId)).toEqual([VIRTUAL_SCENE_CAMERA_ID, physical]);
 const denied = client.acquire(physical); const rejected = expect(denied).rejects.toThrow(origin === 'mixed-view' ? 'Approve headset screen sharing' : 'Allow headset camera');
 const previous = client.snapshot().requestId;
 publish({ sources: [VIRTUAL_SCENE_CAMERA_ID, physical], sourceId: physical, requestId: previous, status: 'failed', error: origin === 'mixed-view' ? 'screen-share-consent' : 'permission-required' }); await rejected;
 expect(client.snapshot().requestId).toBe('');
 const acquired = client.acquire(physical); const requestId = client.snapshot().requestId;
 publish({ sources: [VIRTUAL_SCENE_CAMERA_ID, physical], sourceId: VIRTUAL_SCENE_CAMERA_ID, requestId, frame: frame() }); expect(decodes).toHaveLength(0);
 const captured = frame();
 publish({ sources: [VIRTUAL_SCENE_CAMERA_ID, physical], sourceId: physical, requestId,
  frame: { sourceId: physical, capture: { captureId: captured.capture.captureId, capturedAt: captured.capture.capturedAt, sha256: captured.capture.sha256, mimeType: 'image/jpeg', width: 512, height: 384 }, data: captured.data } });
 decodes[0](image()); const stream = await acquired;
 expect(cameraFrameState(stream)?.origin).toBe(origin); expect(client.snapshot().sourceId).toBe(physical);
 client.suspend(); expect(stream.active).toBe(false);
});
