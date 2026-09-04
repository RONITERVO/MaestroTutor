import type { AppSettings } from '../../core/types';
import { IMAGE_GEN_CAMERA_ID } from '../../core/config/app';

// Browsers can hide device IDs until the user grants camera permission.
// Keep that selectable default camera distinct from Off (null).
export const DEFAULT_CAMERA_ID = '__default_camera__';

export const cameraVideoConstraints = (deviceId: string | null): MediaTrackConstraints | true => (
  deviceId && deviceId !== DEFAULT_CAMERA_ID ? { deviceId: { exact: deviceId } } : true
);

/** Off and generated-image modes never authorize physical camera capture. */
export const hasCameraConsent = (settings: AppSettings): boolean => Boolean(
  settings.selectedCameraId
  && settings.selectedCameraId !== IMAGE_GEN_CAMERA_ID
  && (settings.sendWithSnapshotEnabled || settings.smartReengagement.useVisualContext),
);
