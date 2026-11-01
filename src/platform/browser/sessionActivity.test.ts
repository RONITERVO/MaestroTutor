// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { SessionActivity } from './sessionActivity';
const deferred = <T,>() => { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done; }); return { resolve, promise }; };
const flush = async () => { await Promise.resolve(); await Promise.resolve(); await Promise.resolve(); };
const capture = () => { const stop = vi.fn(); const stream = { getTracks: () => [{ stop, readyState: 'live' }] } as unknown as MediaStream; return { stop, stream }; };

describe('host session interruptions', () => {
  it('keeps ordinary browser use active and stops owned tracks immediately on interruption', async () => {
    const activity = new SessionActivity(); const media = capture();
    expect(await activity.capture({ audio: true }, async () => media.stream)).toBe(media.stream);
    const pending = deferred<void>(); const stopped = vi.fn(() => pending.promise); activity.onSuspend(stopped);
    activity.setSuspended(true);
    expect(media.stop).toHaveBeenCalledOnce(); expect(stopped).toHaveBeenCalledOnce();
    expect(activity.status()).toEqual({ active: false, suspended: true, settled: false });
    activity.setSuspended(false); expect(activity.resume()).toBe(false);
    pending.resolve(); await flush();
    expect(activity.isActive()).toBe(false); expect(activity.resume()).toBe(true);
  });

  it('stops a permission result that arrives after a suspend/resume cycle', async () => {
    const activity = new SessionActivity(); const media = capture(); const pending = deferred<MediaStream>();
    const requested = activity.capture({ audio: true }, () => pending.promise);
    activity.setSuspended(true); await flush(); activity.setSuspended(false); activity.resume();
    pending.resolve(media.stream);
    await expect(requested).rejects.toMatchObject({ name: 'AbortError' });
    expect(media.stop).toHaveBeenCalledOnce();
  });

  it('requires a deliberate resume on cold native start and never asks for permission while inactive', async () => {
    const activity = new SessionActivity(); const acquire = vi.fn(); activity.requireResume();
    await expect(activity.capture({ audio: true }, acquire)).rejects.toMatchObject({ name: 'AbortError' });
    expect(acquire).not.toHaveBeenCalled(); expect(activity.resume()).toBe(true);
    activity.setSuspended(true); await flush(); expect(activity.resume()).toBe(false);
    activity.setSuspended(false); expect(activity.isActive()).toBe(false);
  });

  it('does not acknowledge failed shutdown, but still stops every owner', async () => {
    const activity = new SessionActivity(); const second = vi.fn();
    activity.onSuspend(() => { throw new Error('owner failure'); }); activity.onSuspend(second);
    activity.setSuspended(true); await flush();
    expect(second).toHaveBeenCalledOnce(); expect(activity.status().settled).toBe(false);
  });

  it('rejects an old shutdown acknowledgment after another focus loss', async () => {
    const activity = new SessionActivity(); const old = deferred<void>(); const next = deferred<void>();
    const stop = vi.fn().mockReturnValueOnce(old.promise).mockReturnValueOnce(next.promise); activity.onSuspend(stop);
    activity.setSuspended(true); activity.setSuspended(false); activity.setSuspended(true);
    old.resolve(); await flush(); expect(activity.status().settled).toBe(false);
    next.resolve(); await flush(); expect(activity.status().settled).toBe(true);
  });
});
