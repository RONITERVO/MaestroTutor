// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { LiveInputContext, LIVE_INPUT_LIMITS, validateLiveInputMedia, type LiveInputMedia } from './liveInputContext';
const audio = btoa(String.fromCharCode(0, 0, 255, 127, 0, 128, 255, 255));
const jpeg = btoa(String.fromCharCode(255, 216, 10, 20, 255, 217));
const valid = () => { const input = new LiveInputContext(() => 100); input.recordAudio(audio); input.recordFrame(jpeg); return input.finish(); };

describe('bounded original Live input', () => {
  it('preserves every sent PCM byte, packet order, JPEG and relative delivery time', () => {
    let now = 1000;
    const input = new LiveInputContext(() => now);
    input.recordFrame(jpeg); now += 100; input.recordAudio(audio); now += 40; input.recordFrame(jpeg);
    now += 60; input.recordAudio(audio);
    const result = input.finish(); validateLiveInputMedia(result);
    expect(atob(result.audio!.data).slice(44)).toBe(atob(audio) + atob(audio));
    expect(result.audio).toMatchObject({ samples: 8, sampleRate: 16000, mimeType: 'audio/wav' });
    expect(result.packets).toEqual([{ atMs: 100, sampleOffset: 0, samples: 4 }, { atMs: 200, sampleOffset: 4, samples: 4 }]);
    expect(result.frames).toEqual([{ mimeType: 'image/jpeg', data: jpeg, atMs: 0, audioOffsetSamples: 0 },
      { mimeType: 'image/jpeg', data: jpeg, atMs: 140, audioOffsetSamples: 4 }]);
    input.recordAudio(audio); input.recordFrame(jpeg);
    expect(input.finish()).toMatchObject({ complete: false, issue: 'missing', frames: [], packets: [] });
    expect(result.frames).toHaveLength(2);
  });
  it.each(['samples', 'frames', 'packets', 'bytes'] as const)('discards the whole capture on the %s limit, never passes a partial context', kind => {
    const input = new LiveInputContext();
    if (kind === 'samples') input.recordAudio(btoa('a'.repeat(LIVE_INPUT_LIMITS.samples * 2 + 2)));
    if (kind === 'frames') for (let i = 0; i <= LIVE_INPUT_LIMITS.frames; i++) input.recordFrame(jpeg);
    if (kind === 'packets') for (let i = 0; i <= LIVE_INPUT_LIMITS.packets; i++) input.recordAudio('AAA=');
    if (kind === 'bytes') {
      input.recordAudio(audio);
      const frame = btoa(String.fromCharCode(255, 216) + 'a'.repeat(1024 * 1024 - 4) + String.fromCharCode(255, 217));
      for (let i = 0; i < 4; i++) input.recordFrame(frame);
    }
    input.recordAudio(audio);
    const result = input.finish();
    expect(result).toEqual({ version: 1, complete: false, issue: 'limit', frames: [], packets: [] });
    expect(() => validateLiveInputMedia(result)).toThrow('original audio');
  });
  it.each(['?', 'AA==', 'AB==', 'data:audio/pcm;base64,AAA='])('rejects invalid PCM %s without breaking the live sender', data => {
    const input = new LiveInputContext(); input.recordAudio(data);
    expect(input.finish()).toMatchObject({ complete: false, issue: 'invalid', packets: [] });
  });
  it('releases evidence on teardown or interruption and requires actual sent audio', () => {
    const input = new LiveInputContext(); input.recordAudio(audio); input.recordFrame(jpeg); input.discard();
    expect(input.finish()).toMatchObject({ complete: false, packets: [], frames: [] });
    const interrupted = new LiveInputContext(); interrupted.recordAudio(audio); interrupted.invalidate('interrupted');
    expect(interrupted.finish()).toMatchObject({ complete: false, issue: 'interrupted', packets: [] });
    const cameraOnly = new LiveInputContext(); cameraOnly.recordFrame(jpeg);
    expect(() => validateLiveInputMedia(cameraOnly.finish())).toThrow();
  });
  it.each([
    (m: LiveInputMedia) => { m.version = 2 as 1; },
    (m: LiveInputMedia) => { Object.assign(m, { extra: 'unbounded data' }); },
    (m: LiveInputMedia) => { Object.assign(m.audio!, { extra: 'unbounded data' }); },
    (m: LiveInputMedia) => { Object.assign(m.frames[0], { extra: 'unbounded data' }); },
    (m: LiveInputMedia) => { Object.assign(m.packets[0], { extra: 'unbounded data' }); },
    (m: LiveInputMedia) => { m.audio!.sampleRate = 24000 as 16000; },
    (m: LiveInputMedia) => { m.audio!.samples++; },
    (m: LiveInputMedia) => { m.audio!.data = btoa('RIFF' + 'a'.repeat(48)); },
    (m: LiveInputMedia) => { m.packets[0].sampleOffset = 1; },
    (m: LiveInputMedia) => { m.packets[0].atMs = Infinity; },
    (m: LiveInputMedia) => { m.frames[0].data = audio; },
    (m: LiveInputMedia) => { m.frames[0].audioOffsetSamples = 99; },
    (m: LiveInputMedia) => { m.frames[0].atMs = -1; },
  ])('validates persisted context before reuse %#', corrupt => {
    const media = valid(); corrupt(media); expect(() => validateLiveInputMedia(media)).toThrow('original audio');
  });
});

it.each(['virtual-scene', 'headset-camera', 'mixed-view'] as const)('retains %s source and rejects invented origins on restore', origin => {
 const input = new LiveInputContext(() => 0); input.recordAudio(audio); input.recordFrame(jpeg, origin);
 const result = input.finish(); expect(result.frames[0].origin).toBe(origin); expect(() => validateLiveInputMedia(result)).not.toThrow();
 (result.frames[0] as any).origin = 'trusted-real-camera'; expect(() => validateLiveInputMedia(result)).toThrow();
});
