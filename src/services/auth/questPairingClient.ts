// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { checkLinkSignal, isLinkCancelled, linkHttpsUrl, postQuestLink, QuestLinkError, questLinkCancelled, withLinkAbort } from './questLinkProtocol';
export interface QuestPairingView {
  phase: 'idle' | 'creating' | 'waiting' | 'paused' | 'redeeming' | 'signing-in' | 'cancelling';
  code?: string; verificationUrl?: string; expiresAt?: number;
}
interface Activity { status(): { suspended: boolean }; subscribe(listener: () => void): () => void }
export interface QuestPairingGrant {
  token: string; signal: AbortSignal; check(): void; finish(): void;
}
interface Attempt {
  controller: AbortController; code?: string; secret?: string; proof?: string; expiresAt?: number;
  cleanup(): void;
}
export class QuestPairingClient {
  private view: QuestPairingView = { phase: 'idle' };
  private readonly listeners = new Set<() => void>();
  private current: Attempt | null = null;
  readonly snapshot = () => this.view;
  readonly subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  private publish(value: QuestPairingView) { this.view = value; for (const listener of this.listeners) listener(); }
  constructor(private readonly options: {
    base(): string; verificationUrl(): string; getProof(): Promise<string | null>; activity: Activity;
    owner(): AbortSignal | null; fetcher?: typeof fetch; now?: () => number;
  }) {}
  cancel(reason: Error = questLinkCancelled()) {
    const attempt = this.current; if (!attempt || attempt.controller.signal.aborted) return;
    this.publish({ ...this.view, phase: 'cancelling' }); attempt.controller.abort(reason);
    // Best-effort server cancellation uses the already verified proof, never a
    // new native attestation after the owner has stopped. Local cancellation is
    // authoritative even if this bounded request cannot reach the server.
    if (attempt.code && attempt.secret && attempt.proof) {
      const timeout = AbortSignal.timeout(5000);
      void postQuestLink({ base: this.options.base(), operation: 'cancel', appCheck: attempt.proof,
        body: { code: attempt.code, deviceSecret: attempt.secret }, signal: timeout, fetcher: this.options.fetcher }).catch(() => {});
    }
  }
  private async visible(attempt: Attempt) {
    checkLinkSignal(attempt.controller.signal);
    if (!this.options.activity.status().suspended) return;
    this.publish({ ...this.view, phase: 'paused' });
    await new Promise<void>((resolve, reject) => {
      const abort = () => { unsubscribe(); reject(attempt.controller.signal.reason || questLinkCancelled()); };
      const unsubscribe = this.options.activity.subscribe(() => {
        if (!this.options.activity.status().suspended) {
          unsubscribe(); attempt.controller.signal.removeEventListener('abort', abort); resolve();
        }
      });
      attempt.controller.signal.addEventListener('abort', abort, { once: true });
      if (attempt.controller.signal.aborted) abort();
    });
  }
  private async request(attempt: Attempt, operation: 'create' | 'status' | 'redeem') {
    checkLinkSignal(attempt.controller.signal);
    const request = new AbortController();
    const deadline = setTimeout(() => request.abort(new QuestLinkError('network', 'Account linking timed out. Try again.')), 45_000);
    const stop = () => request.abort(attempt.controller.signal.reason || questLinkCancelled());
    const unsubscribe = this.options.activity.subscribe(() => { if (this.options.activity.status().suspended) request.abort(questLinkCancelled()); });
    attempt.controller.signal.addEventListener('abort', stop, { once: true });
    if (this.options.activity.status().suspended) request.abort(questLinkCancelled());
    try {
      const proof = await withLinkAbort(this.options.getProof(), request.signal); checkLinkSignal(request.signal);
      if (!proof) throw new QuestLinkError('verification', 'This Quest app could not be verified. Try again.');
      attempt.proof = proof;
      return await postQuestLink({ base: this.options.base(), operation, appCheck: proof,
        body: operation === 'create' ? {} : { code: attempt.code, deviceSecret: attempt.secret },
        signal: request.signal, fetcher: this.options.fetcher });
    } finally { clearTimeout(deadline); unsubscribe(); attempt.controller.signal.removeEventListener('abort', stop); }
  }
  async start(signal: AbortSignal): Promise<QuestPairingGrant> {
    if (this.current) throw new QuestLinkError('busy', 'The previous sign-in is still finishing. Try again shortly.');
    linkHttpsUrl(this.options.base()); const verificationUrl = linkHttpsUrl(this.options.verificationUrl(), true);
    const owner = this.options.owner(); if (!owner || owner.aborted || signal.aborted) throw questLinkCancelled();
    const now = this.options.now || Date.now;
    const controller = new AbortController(); let timer: ReturnType<typeof setTimeout> | undefined;
    const stop = () => this.cancel();
    const attempt: Attempt = { controller, cleanup: () => {
      if (timer) clearTimeout(timer);
      signal.removeEventListener('abort', stop); owner.removeEventListener('abort', stop);
      attempt.secret = undefined; attempt.proof = undefined;
      if (this.current === attempt) { this.current = null; this.publish({ phase: 'idle' }); }
    } };
    this.current = attempt; signal.addEventListener('abort', stop, { once: true }); owner.addEventListener('abort', stop, { once: true });
    this.publish({ phase: 'creating' });
    try {
      await this.visible(attempt);
      const issued = await this.request(attempt, 'create'); checkLinkSignal(controller.signal);
      if (typeof issued.code !== 'string' || !/^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$/.test(issued.code)
        || typeof issued.deviceSecret !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(issued.deviceSecret)
        || !Number.isSafeInteger(issued.expiresAt) || Number(issued.expiresAt) <= now() || Number(issued.expiresAt) > now() + 300_000
        || issued.verificationUrl !== verificationUrl || issued.pollIntervalMs !== 5000) throw new QuestLinkError('invalid-response', 'Maestro returned an invalid account link. Try again.');
      attempt.code = issued.code; attempt.secret = issued.deviceSecret; attempt.expiresAt = Number(issued.expiresAt);
      timer = setTimeout(() => controller.abort(new QuestLinkError('expired', 'This code expired. Start again in the book.')), attempt.expiresAt - now());
      const check = () => { checkLinkSignal(controller.signal); if (now() >= attempt.expiresAt!) throw new QuestLinkError('expired', 'This code expired. Start again in the book.'); };
      this.publish({ phase: 'waiting', code: attempt.code, verificationUrl, expiresAt: attempt.expiresAt });
      let failures = 0;
      while (true) {
        await new Promise<void>((resolve, reject) => {
          const abort = () => { clearTimeout(wait); reject(controller.signal.reason || questLinkCancelled()); };
          const wait = setTimeout(() => { controller.signal.removeEventListener('abort', abort); resolve(); }, 5000);
          controller.signal.addEventListener('abort', abort, { once: true }); if (controller.signal.aborted) abort();
        });
        await this.visible(attempt); check(); this.publish({ ...this.view, phase: 'waiting' });
        let status: Record<string, unknown>;
        try { status = await this.request(attempt, 'status'); }
        catch (error) {
          check();
          if (this.options.activity.status().suspended && isLinkCancelled(error)) continue;
          if (error instanceof QuestLinkError && error.code === 'network' && ++failures <= 2) continue;
          throw error;
        }
        check(); failures = 0;
        if (status.expiresAt !== attempt.expiresAt || !['pending', 'approved'].includes(String(status.state))) throw new QuestLinkError('invalid-response', 'Maestro returned an invalid account-link status.');
        if (status.state !== 'approved') continue;
        this.publish({ ...this.view, phase: 'redeeming' });
        // Never retry redemption: a lost response may already have consumed it.
        const result = await this.request(attempt, 'redeem'); check();
        if (typeof result.customToken !== 'string' || result.customToken.length > 16384
          || !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(result.customToken)) throw new QuestLinkError('invalid-response', 'Maestro returned an invalid sign-in response.');
        this.publish({ ...this.view, phase: 'signing-in' });
        return { token: result.customToken, signal: controller.signal, check, finish: attempt.cleanup };
      }
    } catch (error) {
      const interrupted = isLinkCancelled(error) && !controller.signal.aborted;
      attempt.cleanup();
      if (interrupted) throw new QuestLinkError('interrupted', 'Sign-in was interrupted. Return to the book and start again.');
      throw error;
    }
  }
}
