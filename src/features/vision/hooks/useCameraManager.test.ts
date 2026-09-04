// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, renderHook, waitFor } from '@testing-library/react';
import { DEFAULT_CAMERA_ID } from '../../../core-sdk/media/cameraConsent';

const state = vi.hoisted(() => ({
  settings: { selectedCameraId: null, sendWithSnapshotEnabled: true, smartReengagement: { useVisualContext: true } },
  setAvailableCameras: vi.fn(), setCurrentCameraFacingMode: vi.fn(), setLiveVideoStream: vi.fn(),
  setVisualContextCameraError: vi.fn(), setSnapshotUserError: vi.fn(), updateSetting: vi.fn(),
}));
vi.mock('../../../store', () => ({ useMaestroStore: Object.assign((select: any) => select(state), { getState: () => state }) }));
vi.mock('../../../shared/utils/sttFlowDebug', () => ({ errorSttFlow: vi.fn(), logSttFlow: vi.fn(), warnSttFlow: vi.fn() }));
import { useCameraManager } from './useCameraManager';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('exposes a selectable camera before permission and requests access only when enabled', async () => {
  const stop = vi.fn();
  const track = { stop, getSettings: () => ({ deviceId: 'permitted-camera' }) };
  const stream = { getTracks: () => [track], getVideoTracks: () => [track] };
  const getUserMedia = vi.fn().mockResolvedValue(stream);
  vi.stubGlobal('navigator', { mediaDevices: {
    getUserMedia,
    enumerateDevices: async () => [{ kind: 'videoinput', deviceId: '', label: '' }],
    addEventListener: vi.fn(), removeEventListener: vi.fn(),
  } });
  const { rerender } = renderHook(({ enabled }) => useCameraManager({
    t: (key: string) => key, selectedCameraId: enabled ? DEFAULT_CAMERA_ID : null,
    sendWithSnapshotEnabled: enabled, useVisualContext: false,
  }), { initialProps: { enabled: false } });
  await waitFor(() => expect(state.setAvailableCameras).toHaveBeenCalledWith([
    { deviceId: DEFAULT_CAMERA_ID, label: 'Camera 1', facingMode: 'unknown' },
  ]));
  expect(getUserMedia).not.toHaveBeenCalled();
  rerender({ enabled: true });
  await waitFor(() => expect(getUserMedia).toHaveBeenCalledWith({ video: true }));
  expect(state.updateSetting).toHaveBeenCalledWith('selectedCameraId', 'permitted-camera');
});

it('does not open a temporary camera when a completed Live turn requests a snapshot with camera off', async () => {
  const getUserMedia = vi.fn();
  vi.stubGlobal('navigator', { mediaDevices: {
    getUserMedia, enumerateDevices: async () => [], addEventListener: vi.fn(), removeEventListener: vi.fn(),
  } });
  const { result } = renderHook(() => useCameraManager({
    t: (key: string) => key, selectedCameraId: null, sendWithSnapshotEnabled: false, useVisualContext: false,
  }));
  // Even a leftover preview element must not override the current Off selection.
  result.current.visualContextVideoRef.current = document.createElement('video');
  expect(await result.current.captureSnapshot(false)).toBeNull();
  expect(getUserMedia).not.toHaveBeenCalled();
});
