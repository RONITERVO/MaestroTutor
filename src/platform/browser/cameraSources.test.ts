// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { acquireCameraMedia, registerCameraSources, VIRTUAL_SCENE_CAMERA_ID } from './cameraSources';
afterEach(() => vi.unstubAllGlobals());
it('keeps ordinary browser capture unchanged and never falls back for a reserved native ID', async () => {
 const getUserMedia = vi.fn().mockResolvedValue('browser'); vi.stubGlobal('navigator', { mediaDevices: { getUserMedia } });
 expect(await acquireCameraMedia({ audio: true })).toBe('browser');
 await expect(acquireCameraMedia({ video: { deviceId: { exact: VIRTUAL_SCENE_CAMERA_ID } } })).rejects.toMatchObject({ name: 'NotFoundError' });
 expect(getUserMedia).toHaveBeenCalledOnce();
});
it('stops an acquired virtual camera if adding the microphone fails', async () => {
 const stop = vi.fn(); const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
 const remove = registerCameraSources({ devices: () => [{ deviceId: VIRTUAL_SCENE_CAMERA_ID, label: 'Virtual', facingMode: 'environment' }], subscribe: () => () => {}, acquire: async () => stream });
 vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn().mockRejectedValue(new Error('Microphone denied')) } });
 try { await expect(acquireCameraMedia({ video: { deviceId: VIRTUAL_SCENE_CAMERA_ID }, audio: true })).rejects.toThrow('denied'); expect(stop).toHaveBeenCalledOnce(); } finally { remove(); }
});

it('never falls through to the OS for native IDs in ideal or list constraints', async () => {
 const getUserMedia = vi.fn(); vi.stubGlobal('navigator', { mediaDevices: { getUserMedia } });
 for (const deviceId of [{ ideal: VIRTUAL_SCENE_CAMERA_ID }, [VIRTUAL_SCENE_CAMERA_ID], { exact: [VIRTUAL_SCENE_CAMERA_ID] }]) {
  await expect(acquireCameraMedia({ video: { deviceId } })).rejects.toMatchObject({ name: 'NotFoundError' });
 }
 await expect(acquireCameraMedia({ video: { deviceId: { ideal: [VIRTUAL_SCENE_CAMERA_ID, 'physical'] } } })).rejects.toMatchObject({ name: 'OverconstrainedError' });
 expect(getUserMedia).not.toHaveBeenCalled();
});
