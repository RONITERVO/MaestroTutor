// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createRoot } from 'react-dom/client';
import { installBookBridge } from '../../src/platform/quest/bookBridge';
import { useCameraManager } from '../../src/features/vision/hooks/useCameraManager';
import { useMaestroStore } from '../../src/store';
import { sessionActivity } from '../../src/platform/browser/sessionActivity';
import { createBrowserLiveVideo } from '../../src/features/speech/live/browserVideo';
import { createLiveSessionState } from '../../src/features/speech/live/state';
import { LiveInputContext } from '../../src/core-sdk/media/liveInputContext';
const t = (key: string) => key;
useMaestroStore.getState().updateSetting('selectedCameraId', null);
useMaestroStore.getState().updateSetting('sendWithSnapshotEnabled', false);
installBookBridge(window, () => ({ version: 1, layout: 'conversation', activity: 'idle', bookmarkMessageId: null, selectedArtifactId: null, historyStart: 0, historyEnd: 0, historyTotal: 0 }), () => {});
sessionActivity.resume();
const target = window as any;
function CameraFixture() {
 const settings = useMaestroStore(state => state.settings);
 const camera = useCameraManager({ t, selectedCameraId: settings.selectedCameraId, sendWithSnapshotEnabled: settings.sendWithSnapshotEnabled, useVisualContext: false });
 target.cameraFixture = camera;
 const choose = (id: string | null) => { useMaestroStore.getState().updateSetting('selectedCameraId', id); useMaestroStore.getState().updateSetting('sendWithSnapshotEnabled', Boolean(id)); };
 return <main><h1>Book camera verification</h1><p>Native image transport through the normal camera manager.</p>
  {camera.availableCameras.map(source => <button key={source.deviceId} data-source={source.deviceId} onClick={() => choose(source.deviceId)}>{source.label}</button>)}
  <button id="off" onClick={() => choose(null)}>Off</button>
  <p role="status">{camera.visualContextCameraError || 'Ready'}</p><video ref={camera.visualContextVideoRef} muted playsInline style={{ display: 'block', width: 512, height: 384 }}/>
 </main>;
}
target.startFixtureLive = async () => {
 const camera = target.cameraFixture;
 const state = createLiveSessionState({}); state.currentSessionIdRef.current = 1;
 target.liveInputs = [];
 state.sessionRef.current = { sendRealtimeInput: (value: unknown) => target.liveInputs.push(value) } as any;
 const input = new LiveInputContext(); input.recordAudio('AAA='); state.liveInputContextRef.current = input;
 const live = createBrowserLiveVideo(state, { hasCameraConsent: () => Boolean(useMaestroStore.getState().settings.selectedCameraId) });
 await live.updateVideoInput(camera.liveVideoStream, camera.visualContextVideoRef.current);
 target.stopFixtureLive = () => { live.stopVideoFrameLoop(); live.detachCaptureVideo(); return input.finish(); };
};
createRoot(document.getElementById('root')!).render(<CameraFixture/>);
