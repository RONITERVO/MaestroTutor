// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export class QuestLinkError extends Error {
  constructor(readonly code: string, message: string) { super(message); this.name = 'QuestLinkError'; }
}
export const questLinkCancelled = () => new DOMException('Account linking was cancelled.', 'AbortError');
export const isLinkCancelled = (error: unknown) => error !== null && typeof error === 'object' && 'name' in error && error.name === 'AbortError';
export const checkLinkSignal = (signal: AbortSignal) => { if (signal.aborted) throw signal.reason || questLinkCancelled(); };
export const linkObject = (value: unknown): value is Record<string, unknown> => Boolean(value) && typeof value === 'object' && !Array.isArray(value);
export function linkHttpsUrl(value: string, approval = false): string {
  try {
    const url = new URL(value);
    if (url.protocol !== 'https:' || url.username || url.password || url.hash || url.search
      || (approval && url.pathname !== '/quest-link.html')) throw new Error();
    return approval ? url.href : url.href.replace(/\/$/, '');
  } catch { throw new QuestLinkError('not-configured', 'Account linking is not configured in this build.'); }
}
export function withLinkAbort<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  return new Promise((resolve, reject) => {
    const abort = () => reject(signal.reason || questLinkCancelled());
    signal.addEventListener('abort', abort, { once: true });
    if (signal.aborted) abort();
    promise.then(value => { if (!signal.aborted) resolve(value); }, reject)
      .finally(() => signal.removeEventListener('abort', abort));
  });
}
const errors: Record<string, string> = {
  'quest-link/expired': 'This code has expired or was already used. Start again in the book.',
  'quest-link/unauthorized': 'This app could not be verified. Use the released Maestro app and try again.',
  'quest-link/recent-login-required': 'Sign in with Google again before linking the headset.',
  'quest-link/invalid': 'Check the code shown in your book and try again.',
  'quest-link/rate-limited': 'Too many attempts. Wait a minute before trying again.',
  'quest-link/unavailable': 'Account linking is unavailable. Try again later.',
};
/** Used by both the book and the browser page. Credentials never go in URLs. */
export async function postQuestLink(options: {
  base: string; operation: 'create' | 'status' | 'approve' | 'redeem' | 'cancel'; body: unknown;
  appCheck: string; bearer?: string; signal: AbortSignal; fetcher?: typeof fetch;
}): Promise<Record<string, unknown>> {
  const timeout = new AbortController();
  const stop = () => timeout.abort(options.signal.reason || questLinkCancelled());
  options.signal.addEventListener('abort', stop, { once: true });
  const timer = setTimeout(() => timeout.abort(new QuestLinkError('network', 'Account linking timed out. Try again.')), 45_000);
  if (options.signal.aborted) stop();
  try {
    checkLinkSignal(timeout.signal);
    if (!options.appCheck || options.appCheck.length > 16384) throw new QuestLinkError('verification', 'This app could not be verified. Try again.');
    const response = await withLinkAbort((options.fetcher || fetch)(linkHttpsUrl(options.base) + '/' + options.operation, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Firebase-AppCheck': options.appCheck,
        ...(options.bearer ? { Authorization: `Bearer ${options.bearer}` } : {}) }, body: JSON.stringify(options.body),
      credentials: 'omit', redirect: 'error', cache: 'no-store', signal: timeout.signal,
    }), timeout.signal);
    checkLinkSignal(timeout.signal);
    if (!response.body) throw new Error();
    const reader = response.body.getReader(), decoder = new TextDecoder('utf-8', { fatal: true });
    let bytes = 0, text = '';
    try {
      while (true) {
        const next = await withLinkAbort(reader.read(), timeout.signal); checkLinkSignal(timeout.signal);
        if (next.done) break;
        bytes += next.value.byteLength; if (bytes > 32768) throw new Error();
        text += decoder.decode(next.value, { stream: true });
      }
      const value: unknown = JSON.parse(text + decoder.decode());
      if (!linkObject(value)) throw new Error();
      if (!response.ok) {
        const code = typeof value.code === 'string' && Object.prototype.hasOwnProperty.call(errors, value.code) ? value.code : 'quest-link/unavailable';
        throw new QuestLinkError(code, errors[code]);
      }
      return value;
    } finally { void reader.cancel().catch(() => {}); }
  } catch (error) {
    if (timeout.signal.aborted) throw timeout.signal.reason;
    if (error instanceof QuestLinkError) throw error;
    throw new QuestLinkError('network', 'Could not connect to Maestro. Check your connection and try again.');
  } finally { clearTimeout(timer); options.signal.removeEventListener('abort', stop); }
}
