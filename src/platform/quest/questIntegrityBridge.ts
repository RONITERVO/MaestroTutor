// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export const isNativeQuestBook = (): boolean => typeof window !== 'undefined'
  && window.top === window && window.location.origin === 'https://appassets.androidplatform.net'
  && new URLSearchParams(window.location.search).get('surface') === 'quest-book';

export interface QuestIntegrityRequest { session: string; revision: number; nonce: string }
const unavailable = () => new Error('Quest verification is unavailable. Reopen the book and try again.');
const cancelled = () => new DOMException('Quest verification was interrupted.', 'AbortError');

/** The local top-level book is polled by native; there is no JS-to-JNI interface. */
export class QuestIntegrityClient {
  readonly lifetime = new AbortController();
  private readonly session = crypto.randomUUID().replace(/-/g, '');
  private revision = 0;
  private pending: { request: QuestIntegrityRequest; resolve: (token: string) => void; reject: (error: Error) => void; cleanup: () => void } | null = null;
  readonly snapshot = (): QuestIntegrityRequest | undefined => this.pending ? { ...this.pending.request } : undefined;
  request(nonce: string, signal: AbortSignal): Promise<string> {
    if (this.lifetime.signal.aborted || signal.aborted) return Promise.reject(cancelled());
    if (!/^[A-Za-z0-9_-]{43}$/.test(nonce) || this.pending) return Promise.reject(unavailable());
    return new Promise((resolve, reject) => {
      const abort = () => this.cancel();
      const timeout = setTimeout(() => this.finish(null, unavailable()), 20_000);
      const cleanup = () => { clearTimeout(timeout); signal.removeEventListener('abort', abort); };
      this.pending = { request: { session: this.session, revision: ++this.revision, nonce }, resolve, reject, cleanup };
      signal.addEventListener('abort', abort, { once: true });
    });
  }
  readonly receive = (input: unknown): boolean => {
    if (!this.pending || typeof input !== 'object' || !input || Array.isArray(input)) return false;
    const value = input as Record<string, unknown>, request = this.pending.request;
    if (value.version !== 1 || value.session !== request.session || value.revision !== request.revision) return false;
    if (typeof value.error === 'string' && value.error) {
      const reason = value.error === 'not-configured'
        ? new Error('Managed Quest verification is not configured in this build.') : unavailable();
      this.finish(null, reason); return true;
    }
    if (typeof value.token !== 'string' || value.token.length > 32768 || !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(value.token)) return false;
    this.finish(value.token); return true;
  };
  private finish(token: string | null, error?: Error) {
    const pending = this.pending; this.pending = null;
    if (!pending) return;
    pending.cleanup();
    if (token) pending.resolve(token); else pending.reject(error || cancelled());
  }
  cancel() { this.finish(null, cancelled()); }
  dispose() { this.lifetime.abort(); this.cancel(); }
}

let current: QuestIntegrityClient | null = null;
export const currentQuestIntegrityClient = () => current;
export function registerQuestIntegrity(client: QuestIntegrityClient) {
  current?.dispose(); current = client;
  return () => { client.dispose(); if (current === client) current = null; };
}
