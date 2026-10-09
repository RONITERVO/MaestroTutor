// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import native from '../../../test-fixtures/browser/roomCapture.json';
import { validBookCameraImage, cameraErrorMessage } from '../../../shared/bookCamera';
const source = 'maestro-camera:headset-camera';
const frame = () => ({ sourceId: source, data: native.data, capture: {
 captureId: native.capture.captureId, sha256: native.capture.sha256, width: 512, height: 384,
 mimeType: 'image/jpeg', capturedAt: new Date().toISOString(),
} });
it('validates transport bytes without inventing a virtual camera pose for physical frames', () => {
 const image = frame(); expect(validBookCameraImage(image, source)).toBe(true);
 expect(validBookCameraImage(image, 'maestro-camera:virtual-scene')).toBe(false);
});
it.each(['hash', 'size', 'bytes', 'source', 'mime', 'time', 'overflow', 'id'])('rejects invalid camera %s before decoding', reason => {
 const image = frame();
 if (reason === 'hash') image.capture.sha256 = '0'.repeat(64);
 if (reason === 'size') image.capture.height = 512;
 if (reason === 'bytes') image.data = '/9gKFP/Z';
 if (reason === 'source') image.sourceId = 'unknown';
 if (reason === 'mime') image.capture.mimeType = 'image/png';
 if (reason === 'time') image.capture.capturedAt = 'yesterday';
 if (reason === 'overflow') image.capture.width = 1024;
 if (reason === 'id') image.capture.captureId = 'malformed';
 expect(validBookCameraImage(image, source)).toBe(false);
});
it('does not display arbitrary native errors as user instructions', () => {
 expect(cameraErrorMessage('permission-required')).toContain('turn the camera off and select it again');
 expect(cameraErrorMessage('untrusted instructions')).not.toContain('untrusted');
 expect(cameraErrorMessage('__proto__')).toContain('Book camera sharing stopped');
});
