// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { validRoomCaptureImage, type RoomCaptureImage } from '../../../shared/roomViewCapture';
import { registerCameraFrameState, VIRTUAL_SCENE_CAMERA_ID, type CameraSourceProvider } from '../browser/cameraSources';
import { sessionActivity } from '../browser/sessionActivity';

export interface BookCameraRequest { session: string; pulse: number; requestId: string; acknowledged: string }
const token = (value: unknown): value is string => typeof value === 'string' && /^[a-f0-9]{32}$/.test(value);
const unavailable = () => new DOMException('Book camera sharing stopped. Choose the camera again when the book is ready.', 'AbortError');
interface CaptureOwner {
  id: string; stream: MediaStream; canvas: HTMLCanvasElement; track: CanvasCaptureMediaStreamTrack;
  removeAbort(): void; stop(): void; resolve(stream: MediaStream): void; reject(error: Error): void;
  accepted: string; drawnAt: number; startedAt: number; decoding: boolean; closed: boolean;
}
/** Latest-frame-only transport, separate from durable room actions and receipts. */
export class CameraBookClient implements CameraSourceProvider {
  private readonly session = crypto.randomUUID().replace(/-/g, '');
  private pulse = 0;
  private revision = 0;
  private lastSeen = -Infinity;
  private host = '';
  private available = false;
  private closed = false;
  private current: CaptureOwner | null = null;
  private readonly listeners = new Set<() => void>();
  private readonly watchdog: ReturnType<typeof setInterval>;
  constructor(private readonly now = () => performance.now(), private readonly decode = (data: string) => new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image(); image.onload = () => resolve(image); image.onerror = () => reject(unavailable()); image.src = `data:image/jpeg;base64,${data}`;
  })) {
    this.watchdog = setInterval(() => {
      if (this.now() - this.lastSeen > 2000) { this.setAvailable(false); this.stop(); }
      const owner = this.current;
      if (owner && this.now() - (owner.drawnAt || owner.startedAt) > 3000) this.stop();
    }, 200);
  }
  private setAvailable(value: boolean) { if (this.available === value) return; this.available = value; for (const listener of this.listeners) listener(); }
  devices = () => this.available && !this.closed ? [{ deviceId: VIRTUAL_SCENE_CAMERA_ID, label: 'Virtual scene (no real camera)', facingMode: 'environment' as const }] : [];
  subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  snapshot = (): BookCameraRequest => ({ session: this.session, pulse: ++this.pulse, requestId: this.current?.id ?? '', acknowledged: this.current?.accepted ?? '' });
  async acquire(id: string, signal?: AbortSignal): Promise<MediaStream> {
    if (signal?.aborted || id !== VIRTUAL_SCENE_CAMERA_ID || !this.available || this.closed || !sessionActivity.isActive()) throw unavailable();
    this.stop();
    const canvas = document.createElement('canvas'); canvas.width = 512; canvas.height = 384;
    if (!canvas.getContext('2d') || typeof canvas.captureStream !== 'function') throw new DOMException('This browser cannot display a native camera.', 'NotSupportedError');
    const stream = canvas.captureStream(0), track = stream.getVideoTracks()[0] as CanvasCaptureMediaStreamTrack;
    if (!track || typeof track.requestFrame !== 'function') { stream.getTracks().forEach(value => value.stop()); throw new DOMException('This browser cannot display a native camera.', 'NotSupportedError'); }
    return new Promise<MediaStream>((resolve, reject) => {
      const owner: CaptureOwner = { id: crypto.randomUUID().replace(/-/g, ''), stream, canvas, track,
        removeAbort: () => signal?.removeEventListener('abort', abort), stop: track.stop.bind(track), resolve, reject, accepted: '', drawnAt: 0, startedAt: this.now(), decoding: false, closed: false };
      const abort = () => this.stop(owner);
      this.current = owner;
      signal?.addEventListener('abort', abort, { once: true });
      track.stop = () => this.stop(owner, false);
      registerCameraFrameState(stream, () => ({ origin: 'virtual-scene', fresh: !owner.closed && Boolean(owner.accepted) && this.now() - owner.drawnAt <= 2000 && this.now() - this.lastSeen <= 2000 }));
    });
  }
  private stop(owner = this.current, notify = true) {
    if (!owner || owner.closed) return;
    owner.closed = true; owner.removeAbort(); if (this.current === owner) this.current = null;
    owner.canvas.getContext('2d')?.clearRect(0, 0, 512, 384);
    owner.track.requestFrame(); owner.stop(); owner.reject(unavailable());
    if (notify) owner.track.dispatchEvent(new Event('ended'));
  }
  receive = (input: unknown): boolean => {
    if (this.closed || !input || typeof input !== 'object') return false;
    const value = input as Record<string, unknown>;
    if (value.version !== 1 || value.session !== this.session || !token(value.host) || typeof value.available !== 'boolean'
      || !Number.isSafeInteger(value.revision) || (value.host === this.host && (value.revision as number) <= this.revision)
      || !['ready', 'streaming', 'failed'].includes(String(value.status))) return false;
    if (this.host && this.host !== value.host) this.stop();
    this.host = value.host; this.revision = value.revision as number; this.lastSeen = this.now();
    this.setAvailable(value.available);
    if (!value.available || (value.status === 'failed' && value.requestId === this.current?.id)) { this.stop(); return true; }
    const owner = this.current;
    if (!owner || value.requestId !== owner.id || !value.frame || owner.decoding) return true;
    if (!validRoomCaptureImage(value.frame)) { this.stop(); return false; }
    const frame: RoomCaptureImage = value.frame;
    if (frame.capture.captureId === owner.accepted) return true;
    const age = Date.now() - Date.parse(frame.capture.capturedAt);
    if (age < -1000 || age > 2000) return false;
    owner.decoding = true; const received = this.now() - Math.max(0, age);
    void this.decode(frame.data).then(image => {
      if (this.current !== owner || owner.closed || this.now() - received > 2000 || this.now() - this.lastSeen > 2000 || !sessionActivity.isActive()) return;
      if (image.width !== 512 || image.height !== 384) { this.stop(owner); return; }
      owner.canvas.getContext('2d')!.drawImage(image, 0, 0); owner.track.requestFrame();
      owner.accepted = frame.capture.captureId; owner.drawnAt = received; owner.resolve(owner.stream);
    }).catch(() => this.stop(owner)).finally(() => { owner.decoding = false; });
    return true;
  };
  suspend() { this.stop(); this.setAvailable(false); this.lastSeen = -Infinity; }
  dispose() { this.suspend(); this.closed = true; clearInterval(this.watchdog); this.listeners.clear(); }
}
