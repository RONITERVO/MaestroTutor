// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** Image provenance is data, not an instruction or a camera permission. */
export type CameraImageOrigin = 'virtual-scene' | 'headset-camera';
export type ImageOrigin = 'generated' | CameraImageOrigin;
export const isCameraImageOrigin = (value: unknown): value is CameraImageOrigin => value === 'virtual-scene' || value === 'headset-camera';
export const isImageOrigin = (value: unknown): value is ImageOrigin => value === 'generated' || isCameraImageOrigin(value);
