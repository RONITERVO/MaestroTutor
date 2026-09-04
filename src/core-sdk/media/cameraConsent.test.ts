import { describe, expect, it } from 'vitest';
import { hasCameraConsent } from './cameraConsent';
import { IMAGE_GEN_CAMERA_ID } from '../../core/config/app';
import type { AppSettings } from '../../core/types';

describe('physical camera consent', () => {
  const settings = (selectedCameraId: string | null, snapshot = true, visual = true) => ({
    selectedCameraId, sendWithSnapshotEnabled: snapshot,
    smartReengagement: { useVisualContext: visual },
  }) as AppSettings;
  it('rejects camera off even if snapshot and observer toggles are still on', () => {
    expect(hasCameraConsent(settings(null))).toBe(false);
    expect(hasCameraConsent(settings(IMAGE_GEN_CAMERA_ID))).toBe(false);
    expect(hasCameraConsent(settings('device', false, false))).toBe(false);
  });
  it('allows the selected physical camera only with current consent', () => {
    expect(hasCameraConsent(settings('device', true, false))).toBe(true);
    expect(hasCameraConsent(settings('device', false, true))).toBe(true);
  });
});
