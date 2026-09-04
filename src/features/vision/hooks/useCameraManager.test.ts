// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, renderHook } from '@testing-library/react';

const state = vi.hoisted(() => ({
  settings: { selectedCameraId: null, sendWithSnapshotEnabled: true, smartReengagement: { useVisualContext: true } },
  setAvailableCameras: vi.fn(), setCurrentCameraFacingMode: vi.fn(), setLiveVideoStream: vi.fn(),
  setVisualContextCameraError: vi.fn(), setSnapshotUserError: vi.fn(), updateSetting: vi.fn(),
}));
vi.mock('../../../store', () => ({ useMaestroStore: Object.assign((select: any) => select(state), { getState: () => state }) }));
vi.mock('../../../shared/utils/sttFlowDebug', () => ({ errorSttFlow: vi.fn(), logSttFlow: vi.fn(), warnSttFlow: vi.fn() }));
import { useCameraManager } from './useCameraManager';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

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
