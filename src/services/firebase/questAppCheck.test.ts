// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { acquireQuestAppCheckToken, questAttestationBaseUrl } from './questAppCheck';
import { QuestIntegrityClient } from '../../platform/quest/questIntegrityBridge';
import { sessionActivity } from '../../platform/browser/sessionActivity';
const base = 'https://backend.example/questAttestation';
const nonce = 'b'.repeat(43), now = 1790940000000;
let client: QuestIntegrityClient;
beforeEach(async () => { sessionActivity.setSuspended(false); await Promise.resolve(); sessionActivity.resume(); client = new QuestIntegrityClient(); });
afterEach(() => client.dispose());
const response = (value: unknown) => new Response(JSON.stringify(value));
const challenge = () => response({ nonce, expiresAt: now + 300000 });
const minted = () => response({ token: 'firebase.app.check', ttlMillis: 1800000 });
async function nativeReply(token = 'native.proof.signature') {
  await vi.waitFor(() => expect(client.snapshot()).toBeDefined());
  client.receive({ version: 1, ...client.snapshot(), token });
}

it('uses one native proof and sends no identity/credentials to the bootstrap; expiry includes exchange delay', async () => {
  let currentTime = now;
  const fetcher = vi.fn().mockImplementationOnce(challenge).mockImplementationOnce(async () => { currentTime += 7000; return minted(); });
  const p = acquireQuestAppCheckToken(base, { client, fetcher, now: () => currentTime });
  await nativeReply();
  await expect(p).resolves.toEqual({ token: 'firebase.app.check', expireTimeMillis: now + 1800000 });
  expect(fetcher).toHaveBeenCalledTimes(2);
  expect(fetcher.mock.calls[0]).toMatchObject([base + '/challenge', { credentials: 'omit', redirect: 'error', cache: 'no-store', body: '{}' }]);
  expect(JSON.parse(fetcher.mock.calls[1][1].body)).toEqual({ nonce, token: 'native.proof.signature' });
});

it.each(['', 'http://backend.example', 'https://user:password@example.test/', 'https://example.test/?token=x', 'https://example.test/#secret'])('refuses unsafe or missing endpoint %s', configured => {
  expect(() => questAttestationBaseUrl(configured)).toThrow('not configured');
});

it.each([{}, { nonce: 'bad', expiresAt: now + 300000 }, { nonce, expiresAt: now - 1 }, { nonce, expiresAt: now + 999999 }, { nonce, expiresAt: 'tomorrow' }])('rejects malformed challenges before native calls: %j', async value => {
  const fetcher = vi.fn(async () => response(value));
  await expect(acquireQuestAppCheckToken(base, { client, fetcher, now: () => now })).rejects.toThrow('verification failed');
  expect(client.snapshot()).toBeUndefined(); expect(fetcher).toHaveBeenCalledTimes(1);
});

it.each([{ token: 'bad', ttlMillis: 1800000 }, { token: 'a.b.c', ttlMillis: 99999999 }, { token: 'a.b.c', ttlMillis: 0 }, { token: 123, ttlMillis: 1800000 }])('rejects malformed minted responses: %j', async value => {
  const fetcher = vi.fn().mockImplementationOnce(challenge).mockImplementationOnce(() => response(value));
  const p = acquireQuestAppCheckToken(base, { client, fetcher, now: () => now });
  const rejected = expect(p).rejects.toThrow('verification failed'); await nativeReply(); await rejected;
});

it('suspension cancels the native request; old proof cannot send an exchange', async () => {
  const fetcher = vi.fn(async () => challenge());
  const p = acquireQuestAppCheckToken(base, { client, fetcher, now: () => now }), rejected = expect(p).rejects.toMatchObject({ name: 'AbortError' });
  await vi.waitFor(() => expect(client.snapshot()).toBeDefined()); const old = client.snapshot();
  sessionActivity.setSuspended(true); await rejected;
  expect(client.receive({ version: 1, ...old, token: 'native.proof.signature' })).toBe(false);
  expect(fetcher).toHaveBeenCalledTimes(1);
});

it('page disposal during exchange rejects even a transport that ignores abort', async () => {
  let finish!: (value: Response) => void;
  const fetcher = vi.fn().mockImplementationOnce(challenge).mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve; }));
  const p = acquireQuestAppCheckToken(base, { client, fetcher, now: () => now }), rejected = expect(p).rejects.toMatchObject({ name: 'AbortError' });
  await nativeReply(); await vi.waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
  client.dispose(); finish(minted()); await rejected;
});

it('bounds provider responses and never discloses transport errors', async () => {
  for (const fetcher of [vi.fn(async () => new Response('x'.repeat(65537))),
    vi.fn(async () => { throw new Error('https://secret.example/?token=private'); }),
    vi.fn(async () => new Response('{}', { status: 503 }))]) {
    await expect(acquireQuestAppCheckToken(base, { client, fetcher, now: () => now })).rejects.toThrow('Quest verification failed. Check your connection and try again.');
  }
});
