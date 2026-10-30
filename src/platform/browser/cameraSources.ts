// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { CameraImageOrigin } from '../../../shared/imageOrigin';
import type { CameraDevice } from '../../core/types';

export const VIRTUAL_SCENE_CAMERA_ID = 'maestro-camera:virtual-scene';
export const isNativeCameraId = (id: string | null | undefined) => Boolean(id?.startsWith('maestro-camera:'));
export interface CameraFrameState { origin: CameraImageOrigin; fresh: boolean }
export interface CameraSourceProvider {
  devices(): CameraDevice[];
  acquire(id: string, signal?: AbortSignal): Promise<MediaStream>;
  subscribe(listener: () => void): () => void;
}
let provider: CameraSourceProvider | null = null;
const listeners = new Set<() => void>();
const frames = new WeakMap<MediaStream, () => CameraFrameState>();
const changed = () => { for (const listener of listeners) listener(); };
export const cameraSourceProvider = () => provider;
export const onCameraSourcesChanged = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; };
export function registerCameraSources(value: CameraSourceProvider) {
  provider = value; const remove = value.subscribe(changed); changed();
  return () => { remove(); if (provider === value) { provider = null; changed(); } };
}
export function registerCameraFrameState(stream: MediaStream, read: () => CameraFrameState) { frames.set(stream, read); }
export const cameraFrameState = (stream: MediaStream | null | undefined) => stream ? frames.get(stream)?.() : undefined;
export const cameraStreamFresh = (stream: MediaStream | null | undefined) => stream?.active !== false && cameraFrameState(stream)?.fresh !== false;

/** Reserved native source IDs never fall through to an OS-selected camera. */
export async function acquireCameraMedia(constraints: MediaStreamConstraints, signal?: AbortSignal): Promise<MediaStream> {
  const video = constraints.video;
  const device = video && typeof video === 'object' ? video.deviceId : undefined;
  const candidates = typeof device === 'string' || Array.isArray(device) ? device : device?.exact ?? device?.ideal;
  const ids = typeof candidates === 'string' ? [candidates] : candidates ?? [];
  const nativeIds = ids.filter(isNativeCameraId);
  if (!nativeIds.length) return navigator.mediaDevices.getUserMedia(constraints);
  if (ids.length !== 1) throw new DOMException('Choose exactly one book camera.', 'OverconstrainedError');
  const id = nativeIds[0];
  if (!provider || !provider.devices().some(value => value.deviceId === id)) throw new DOMException('This book camera is unavailable. Choose a camera when the book is ready.', 'NotFoundError');
  const stream = await provider.acquire(id, signal);
  if (!constraints.audio) return stream;
  try {
    const audio = await navigator.mediaDevices.getUserMedia({ audio: constraints.audio });
    if (!stream.active) { audio.getTracks().forEach(track => track.stop()); throw new DOMException('Camera sharing was interrupted.', 'AbortError'); }
    audio.getAudioTracks().forEach(track => stream.addTrack(track)); return stream;
  } catch (error) { stream.getTracks().forEach(track => track.stop()); throw error; }
}
