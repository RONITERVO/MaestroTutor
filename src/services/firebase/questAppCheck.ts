// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { currentQuestIntegrityClient, type QuestIntegrityClient } from '../../platform/quest/questIntegrityBridge';
import { sessionActivity } from '../../platform/browser/sessionActivity';

const failure = () => new Error('Quest verification failed. Check your connection and try again.');
const interrupted = () => new DOMException('Quest verification was interrupted.', 'AbortError');
const object = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

export function questAttestationBaseUrl(configured: string): string {
  try {
    const url = new URL(configured);
    if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash) throw failure();
    return url.href.replace(/\/$/, '');
  } catch { throw new Error('Managed Quest verification is not configured in this build.'); }
}

/** One native proof and one server exchange; no Google/Gemini credentials cross JNI. */
export async function acquireQuestAppCheckToken(configured: string, options: {
  client?: QuestIntegrityClient | null; fetcher?: typeof fetch; now?: () => number;
} = {}): Promise<{ token: string; expireTimeMillis: number }> {
  const base = questAttestationBaseUrl(configured), client = options.client ?? currentQuestIntegrityClient();
  if (!client || client.lifetime.signal.aborted || sessionActivity.status().suspended) throw interrupted();
  const fetcher = options.fetcher || fetch, now = options.now || Date.now;
  const controller = new AbortController();
  const abort = () => controller.abort();
  const unsubscribe = sessionActivity.onSuspend(abort);
  client.lifetime.signal.addEventListener('abort', abort, { once: true });
  const timeout = setTimeout(abort, 45_000);
  const live = () => { if (controller.signal.aborted || client.lifetime.signal.aborted || sessionActivity.status().suspended) throw interrupted(); };
  const post = async (path: string, body: unknown): Promise<Record<string, unknown>> => {
    live();
    const response = await fetcher(base + path, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body), credentials: 'omit', redirect: 'error', cache: 'no-store', signal: controller.signal });
    live();
    if (!response.ok) throw failure();
    // The endpoint's response is small. Read with a limit before parsing so a
    // broken endpoint cannot allocate an unbounded body in the book.
    if (!response.body) throw failure();
    const reader = response.body.getReader(); let text = '', bytes = 0;
    const decoder = new TextDecoder('utf-8', { fatal: true });
    try {
      while (true) {
        const chunk = await reader.read(); live();
        if (chunk.done) break;
        bytes += chunk.value.byteLength; if (bytes > 65536) throw failure();
        text += decoder.decode(chunk.value, { stream: true });
      }
      const value: unknown = JSON.parse(text + decoder.decode());
      if (!object(value)) throw failure();
      return value;
    } finally { await reader.cancel().catch(() => {}); }
  };
  try {
    const challenge = await post('/challenge', {});
    if (typeof challenge.nonce !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(challenge.nonce)
      || typeof challenge.expiresAt !== 'number' || !Number.isSafeInteger(challenge.expiresAt)
      || challenge.expiresAt <= now() || challenge.expiresAt > now() + 330_000) throw failure();
    const proof = await client.request(challenge.nonce, controller.signal); live();
    if (challenge.expiresAt <= now()) throw failure();
    // Count the response's lifetime from request start, not from receipt: network
    // delay must not make Firebase cache a token beyond its actual expiry.
    const requestedAt = now();
    const result = await post('/exchange', { nonce: challenge.nonce, token: proof }); live();
    if (typeof result.token !== 'string' || result.token.length > 32768
      || !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(result.token)
      || typeof result.ttlMillis !== 'number' || !Number.isSafeInteger(result.ttlMillis)
      || result.ttlMillis < 60_000 || result.ttlMillis > 1_800_000) throw failure();
    const expireTimeMillis = requestedAt + result.ttlMillis;
    if (expireTimeMillis <= now() + 30_000) throw failure();
    return { token: result.token, expireTimeMillis };
  } catch (error) {
    // Preserve only app-authored errors; never disclose upstream URLs or tokens.
    if (controller.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) throw interrupted();
    if (error instanceof Error && error.message === 'Managed Quest verification is not configured in this build.') throw error;
    throw failure();
  } finally { clearTimeout(timeout); unsubscribe(); client.lifetime.signal.removeEventListener('abort', abort); }
}
