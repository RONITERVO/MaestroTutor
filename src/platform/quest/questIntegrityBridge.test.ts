// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QuestIntegrityClient, currentQuestIntegrityClient, registerQuestIntegrity, isNativeQuestBook } from './questIntegrityBridge';
const nonce = 'a'.repeat(43);
const result = (client: QuestIntegrityClient, token = 'header.body.signature') => ({ version: 1, ...client.snapshot(), token });
afterEach(() => { currentQuestIntegrityClient()?.dispose(); vi.useRealTimers(); vi.unstubAllGlobals(); });
describe('top-level Quest integrity exchange', () => {
  it('matches both session and revision; snapshots never contain a token', async () => {
    const c = new QuestIntegrityClient(), p = c.request(nonce, new AbortController().signal);
    const request = c.snapshot()!;
    expect(request).toEqual({ session: expect.stringMatching(/^[a-f0-9]{32}$/), revision: 1, nonce });
    expect(c.receive({ ...result(c), session: 'b'.repeat(32) })).toBe(false);
    expect(c.receive({ ...result(c), revision: 2 })).toBe(false);
    expect(c.receive(result(c))).toBe(true);
    await expect(p).resolves.toBe('header.body.signature');
    expect(c.snapshot()).toBeUndefined(); expect(c.receive({ version: 1, ...request, token: 'h.b.s' })).toBe(false);
  });
  it('rejects malformed results without resolving and returns only safe native errors', async () => {
    const c = new QuestIntegrityClient(), p = c.request(nonce, new AbortController().signal);
    for (const v of [null, [], { ...result(c), version: 2 }, result(c, 'not JWT'), result(c, 'a'.repeat(32769))]) expect(c.receive(v)).toBe(false);
    expect(c.receive({ ...result(c), error: 'native secret error detail' })).toBe(true);
    await expect(p).rejects.toThrow('Quest verification is unavailable');
  });
  it('allows only one bounded request and makes cancellation terminal', async () => {
    const c = new QuestIntegrityClient(), abort = new AbortController();
    const p = c.request(nonce, abort.signal), old = result(c); const rejection = expect(p).rejects.toMatchObject({ name: 'AbortError' });
    await expect(c.request(nonce, abort.signal)).rejects.toThrow();
    abort.abort(); await rejection; expect(c.receive(old)).toBe(false);
    const next = c.request(nonce, new AbortController().signal); expect(c.snapshot()?.revision).toBe(2);
    expect(c.receive(old)).toBe(false); c.receive(result(c)); await next;
  });
  it('expires a missing native reply', async () => {
    vi.useFakeTimers(); const c = new QuestIntegrityClient();
    const p = c.request(nonce, new AbortController().signal), rejected = expect(p).rejects.toThrow('unavailable');
    await vi.advanceTimersByTimeAsync(20_000); await rejected; expect(c.snapshot()).toBeUndefined();
  });
  it('page replacement aborts the old owner; late cleanup cannot remove the replacement', async () => {
    const first = new QuestIntegrityClient(), removeFirst = registerQuestIntegrity(first);
    const old = first.request(nonce, new AbortController().signal), rejected = expect(old).rejects.toMatchObject({ name: 'AbortError' });
    const second = new QuestIntegrityClient(), removeSecond = registerQuestIntegrity(second);
    await rejected; expect(first.lifetime.signal.aborted).toBe(true);
    removeFirst(); expect(currentQuestIntegrityClient()).toBe(second);
    removeSecond(); expect(currentQuestIntegrityClient()).toBeNull();
  });
});

it('selects native verification only for the local top-level Quest book, never previews or embedded artifacts', () => {
  const target = { location: { origin: 'https://appassets.androidplatform.net', search: '?surface=quest-book' }, top: null as unknown };
  target.top = target; vi.stubGlobal('window', target); expect(isNativeQuestBook()).toBe(true);
  target.top = {}; expect(isNativeQuestBook()).toBe(false); target.top = target;
  for (const origin of ['https://localhost', 'http://127.0.0.1:5178', 'https://chatwithmaestro.com', 'https://appassets.androidplatform.net.evil.test']) {
    target.location.origin = origin; expect(isNativeQuestBook()).toBe(false);
  }
  target.location.origin = 'https://appassets.androidplatform.net'; target.location.search = ''; expect(isNativeQuestBook()).toBe(false);
});
