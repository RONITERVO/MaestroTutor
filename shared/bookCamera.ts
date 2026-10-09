// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { sha256 } from '@noble/hashes/sha2.js';
import { jpegDimensions } from './inlineImages';
import { validRoomCaptureImage, ROOM_CAPTURE_BYTES } from './roomViewCapture';
import type { CameraImageOrigin } from './imageOrigin';
export const BOOK_CAMERA_SOURCES = [
  { deviceId: 'maestro-camera:virtual-scene', label: 'Virtual scene (no real camera)', origin: 'virtual-scene' },
  { deviceId: 'maestro-camera:headset-camera', label: 'Headset camera (real surroundings)', origin: 'headset-camera' },
] as const;
export const bookCameraSource = (id: unknown) => BOOK_CAMERA_SOURCES.find(source => source.deviceId === id);
export interface BookCameraImage {
  sourceId: string;
  capture: { captureId: string; sha256: string; mimeType: 'image/jpeg'; width: number; height: number; capturedAt: string };
  data: string;
}
export function validBookCameraImage(input: unknown, sourceId: string): input is BookCameraImage {
  if (!input || typeof input !== 'object') return false;
  const value = input as BookCameraImage;
  const source = bookCameraSource(sourceId);
  if (!source || value.sourceId !== sourceId) return false;
  if (source.origin === 'virtual-scene') return validRoomCaptureImage(value);
  const c = value.capture;
  if (!c || typeof c !== 'object' || typeof c.captureId !== 'string' || typeof c.sha256 !== 'string' || !/^[a-f0-9]{32}$/.test(c.captureId) || !/^[a-f0-9]{64}$/.test(c.sha256)
    || c.mimeType !== 'image/jpeg' || !Number.isInteger(c.width) || !Number.isInteger(c.height)
    || c.width < 1 || c.width > 512 || c.height < 1 || c.height > 512
    || typeof c.capturedAt !== 'string' || c.capturedAt.length > 32 || !/^\d{4}-\d{2}-\d{2}T[0-9:.]+Z$/.test(c.capturedAt) || !Number.isFinite(Date.parse(c.capturedAt))
    || typeof value.data !== 'string' || !value.data.length || value.data.length > ROOM_CAPTURE_BYTES * 4 / 3
    || value.data.length % 4 !== 0 || !/^[A-Za-z0-9+/]+={0,2}$/.test(value.data)) return false;
  try {
    const bytes = Uint8Array.from(atob(value.data), char => char.charCodeAt(0));
    const size = jpegDimensions(bytes);
    return bytes.length <= ROOM_CAPTURE_BYTES && size?.width === c.width && size.height === c.height
      && Array.from(sha256(bytes), byte => byte.toString(16).padStart(2, '0')).join('') === c.sha256;
  } catch { return false; }
}
const cameraErrors: Readonly<Record<string, string>> = {
  'permission-required': 'Allow headset camera access, then resume the book, turn the camera off and select it again.',
  'passthrough-required': 'Show some of your real surroundings, then turn the camera off and select the headset camera again.',
  'camera-unavailable': 'The headset camera is unavailable. Stop other camera apps, then turn the camera off and select it again.',
  'camera-stale': 'The headset camera stopped updating. Turn the camera off and select it again to retry.',
};
export const cameraErrorMessage = (code: unknown): string => typeof code === 'string' && Object.prototype.hasOwnProperty.call(cameraErrors, code)
  ? cameraErrors[code] : 'Book camera sharing stopped. When the book is ready, turn the camera off and select it again.';
export type { CameraImageOrigin };
