// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it, vi } from 'vitest';
import { linkHttpsUrl, postQuestLink } from './questLinkProtocol';
const options = { base: 'https://backend.test/link', operation: 'approve' as const, body: { code: 'ABCDE-FGHJK', confirm: true },
  appCheck: 'proof', bearer: 'identity', signal: new AbortController().signal };
it.each(['http://backend.test', 'https://user:pass@backend.test', 'https://backend.test?token=private', 'https://backend.test#secret', 'not-a-url'])('rejects unsafe service URL %s', url => {
  expect(() => linkHttpsUrl(url)).toThrow('not configured');
});
it('uses POST headers with no URL credentials, cookies, caching or redirects', async () => {
  const fetcher = vi.fn(async () => new Response('{"approved":true}'));
  expect(await postQuestLink({ ...options, fetcher })).toEqual({ approved: true });
  expect(fetcher.mock.calls[0]).toEqual(['https://backend.test/link/approve', expect.objectContaining({ method: 'POST', credentials: 'omit', redirect: 'error', cache: 'no-store', headers: { 'Content-Type': 'application/json', 'X-Firebase-AppCheck': 'proof', Authorization: 'Bearer identity' } })]);
});
it('never displays untrusted server prose or exception URLs', async () => {
  for (const fetcher of [async () => new Response('{"error":"secret credential","code":"provider/secret"}', { status: 500 }),
    async () => { throw new Error('https://secret@private?token=credential'); }]) {
    await expect(postQuestLink({ ...options, fetcher })).rejects.not.toThrow(/secret|credential|private/);
  }
});
it('maps a known recent-login error to a fixed user-facing message', async () => {
  await expect(postQuestLink({ ...options, fetcher: async () => new Response('{"code":"quest-link/recent-login-required","error":"private"}', { status: 401 }) }))
    .rejects.toMatchObject({ code: 'quest-link/recent-login-required', message: 'Sign in with Google again before linking the headset.' });
});
it('rejects oversized and malformed streamed responses', async () => {
  for (const text of [' '.repeat(32769), '[]', '{', 'null']) {
    await expect(postQuestLink({ ...options, fetcher: async () => new Response(text) })).rejects.toMatchObject({ code: 'network' });
  }
});
it('already-cancelled requests never start fetch', async () => {
  const abort = new AbortController(); abort.abort(); const fetcher = vi.fn();
  await expect(postQuestLink({ ...options, signal: abort.signal, fetcher })).rejects.toMatchObject({ name: 'AbortError' });
  expect(fetcher).not.toHaveBeenCalled();
});
