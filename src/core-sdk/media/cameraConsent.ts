import type { AppSettings } from '../../core/types';
import { IMAGE_GEN_CAMERA_ID } from '../../core/config/app';

/** Off and generated-image modes never authorize physical camera capture. */
export const hasCameraConsent = (settings: AppSettings): boolean => Boolean(
  settings.selectedCameraId
  && settings.selectedCameraId !== IMAGE_GEN_CAMERA_ID
  && (settings.sendWithSnapshotEnabled || settings.smartReengagement.useVisualContext),
);
