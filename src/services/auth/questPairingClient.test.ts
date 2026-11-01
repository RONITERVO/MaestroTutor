// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { QuestPairingClient } from './questPairingClient';
import { QuestLinkError } from './questLinkProtocol';
const now = 1_790_950_000_000;
const issued = () => ({ code: 'ABCDE-FGHJK', deviceSecret: 's'.repeat(43), expiresAt: Date.now() + 300000,
  verificationUrl: 'https://chatwithmaestro.com/quest-link.html', pollIntervalMs: 5000 });
const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });
function harness(overrides: { create?: unknown; fetcher?: typeof fetch; proof?: () => Promise<string | null> } = {}) {
  let suspended = false;
  const listeners = new Set<() => void>(), owner = new AbortController(), external = new AbortController();
  const calls: { url: string; init: RequestInit }[] = [];
  let approved = false;
  const fetcher = overrides.fetcher || (async (url, init) => {
    calls.push({ url: String(url), init: init! });
    if (String(url).endsWith('/create')) return response(overrides.create ?? issued());
    if (String(url).endsWith('/status')) return response({ state: approved ? 'approved' : 'pending', expiresAt: now + 300000 });
    if (String(url).endsWith('/redeem')) return response({ customToken: 'header.payload.signature' });
    return response({ cancelled: true });
  }) as typeof fetch;
  const proof = vi.fn(overrides.proof || (async () => 'verified-proof'));
  const client = new QuestPairingClient({ base: () => 'https://backend.test/pairing', verificationUrl: () => issued().verificationUrl,
    getProof: proof, owner: () => owner.signal, fetcher,
    activity: { status: () => ({ suspended }), subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; } } });
  return { client, calls, proof, owner, external, approve: () => { approved = true; },
    suspend(value: boolean) { suspended = value; for (const listener of listeners) listener(); },
    start: () => client.start(external.signal), listeners };
}
beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(now); });
afterEach(() => { vi.useRealTimers(); });
const flush = async () => { await vi.advanceTimersByTimeAsync(0); };
it('keeps secrets out of public snapshots and returns a private grant only after approval', async () => {
  const h = harness(); const run = h.start(); await flush();
  expect(h.client.snapshot()).toEqual({ phase: 'waiting', code: 'ABCDE-FGHJK', verificationUrl: issued().verificationUrl, expiresAt: now + 300000 });
  expect(JSON.stringify(h.client.snapshot())).not.toContain('ssss');
  await vi.advanceTimersByTimeAsync(4999); expect(h.calls).toHaveLength(1);
  h.approve(); await vi.advanceTimersByTimeAsync(1);
  const grant = await run; expect(grant.token).toBe('header.payload.signature');
  expect(h.calls.map(c => c.url.split('/').slice(-1)[0])).toEqual(['create', 'status', 'redeem']);
  expect(h.calls[1].init.body).toBe(JSON.stringify({ code: 'ABCDE-FGHJK', deviceSecret: 's'.repeat(43) }));
  expect(h.client.snapshot().phase).toBe('signing-in'); grant.finish(); expect(h.client.snapshot()).toEqual({ phase: 'idle' });
});
it('pauses polling and resumes the same code after returning from the browser', async () => {
  const h = harness(); const run = h.start(); await flush(); h.suspend(true);
  await vi.advanceTimersByTimeAsync(15000); expect(h.calls).toHaveLength(1); expect(h.client.snapshot().phase).toBe('paused');
  h.approve(); h.suspend(false); await flush(); const grant = await run;
  expect(h.calls.filter(c => c.url.endsWith('/create'))).toHaveLength(1); grant.finish();
});
it('cancels locally and sends at most one bounded remote cancellation', async () => {
  const h = harness(); const run = h.start(); const rejected = expect(run).rejects.toMatchObject({ name: 'AbortError' });
  await flush(); h.external.abort(); h.client.cancel(); await rejected;
  await vi.advanceTimersByTimeAsync(15000);
  expect(h.calls.filter(c => c.url.endsWith('/cancel'))).toHaveLength(1);
  expect(h.calls.some(c => c.url.endsWith('/redeem'))).toBe(false);
  expect(h.client.snapshot().phase).toBe('idle');
});
it('book replacement aborts waiting and a newer attempt can start only after cleanup', async () => {
  const h = harness(); const run = h.start(); const rejected = expect(run).rejects.toMatchObject({ name: 'AbortError' }); await flush();
  await expect(h.start()).rejects.toMatchObject({ code: 'busy' }); h.owner.abort(); await rejected;
  expect(h.listeners.size).toBe(0); await expect(h.start()).rejects.toMatchObject({ name: 'AbortError' });
});
it('expires while backgrounded without requesting another proof', async () => {
  const h = harness(); const run = h.start(); const rejected = expect(run).rejects.toMatchObject({ code: 'expired' });
  await flush(); h.suspend(true); await vi.advanceTimersByTimeAsync(300000); await rejected;
  expect(h.proof).toHaveBeenCalledTimes(1); expect(h.client.snapshot().phase).toBe('idle');
});
it.each([{ verificationUrl: 'https://attacker.test/quest-link.html' }, { expiresAt: now + 300001 }, { expiresAt: now },
  { deviceSecret: 'short' }, { code: 'abcde-fghjk' }, { pollIntervalMs: 0 }])('rejects malformed creation: %j', async change => {
  const h = harness({ create: { ...issued(), ...change } }); await expect(h.start()).rejects.toMatchObject({ code: 'invalid-response' });
  expect(h.calls).toHaveLength(1);
});
it('a late creation response cannot resurrect a cancelled request', async () => {
  let finish!: (response: Response) => void;
  const h = harness({ fetcher: vi.fn(() => new Promise<Response>(resolve => { finish = resolve; })) });
  const run = h.start(); const rejected = expect(run).rejects.toMatchObject({ name: 'AbortError' }); await flush();
  h.external.abort(); await rejected; finish(response(issued())); await flush();
  expect(h.client.snapshot().phase).toBe('idle');
});
it('proof acquisition has a deadline even if the Firebase SDK never returns', async () => {
  const h = harness({ proof: () => new Promise(() => {}) }); const run = h.start();
  const rejected = expect(run).rejects.toMatchObject({ code: 'network' }); await vi.advanceTimersByTimeAsync(45000); await rejected;
  expect(h.calls).toHaveLength(0);
});
it('never retries a redemption whose response was lost', async () => {
  let redeems = 0;
  const h = harness({ fetcher: (async url => {
    if (String(url).endsWith('/create')) return response(issued());
    if (String(url).endsWith('/status')) return response({ state: 'approved', expiresAt: now + 300000 });
    redeems++; throw new Error('network failure with private details');
  }) as typeof fetch });
  const run = h.start(); const rejected = expect(run).rejects.toMatchObject({ code: 'network' });
  await vi.advanceTimersByTimeAsync(5000); await rejected; expect(redeems).toBe(1);
});
it('limits repeated status network failures instead of polling forever', async () => {
  let polls = 0;
  const h = harness({ fetcher: (async url => {
    if (String(url).endsWith('/create')) return response(issued());
    polls++; throw new Error('private network detail');
  }) as typeof fetch });
  const run = h.start(); const rejected = expect(run).rejects.toBeInstanceOf(QuestLinkError);
  await vi.advanceTimersByTimeAsync(15000); await rejected; expect(polls).toBe(3);
});
