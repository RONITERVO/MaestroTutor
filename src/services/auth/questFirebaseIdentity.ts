// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { Auth } from 'firebase/auth';
import type { ManagedAuthIdentity } from './firebaseAuthBridgeService';
import { clearManagedAccessSession } from '../../core/security/managedAccessSessionStorage';
import { sessionActivity } from '../../platform/browser/sessionActivity';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { isLinkCancelled, QuestLinkError, questLinkCancelled } from './questLinkProtocol';
import { questPairingService } from './questPairingService';

export const QUEST_PENDING_IDENTITY_KEY = 'maestro.quest.pending-sign-in.v1';
const recovered = new WeakMap<Auth, Promise<void>>();
let active: { cancel(): void; settled: Promise<void> } | null = null;
/** A crashed/cancelled sign-in must never restore a partially committed SDK user. */
export function recoverQuestIdentity(auth: Auth): Promise<void> {
  const existing = recovered.get(auth); if (existing) return existing;
  const recovery = (async () => {
    await auth.authStateReady();
    if (window.localStorage.getItem(QUEST_PENDING_IDENTITY_KEY)) {
      const { signOut } = await import('firebase/auth');
      await signOut(auth); await clearManagedAccessSession({ requirePersistence: true });
      window.localStorage.removeItem(QUEST_PENDING_IDENTITY_KEY);
    }
  })().catch(error => { recovered.delete(auth); throw error; });
  recovered.set(auth, recovery); return recovery;
}
export const isQuestIdentityPending = () => active !== null;
export async function cancelQuestIdentity() {
  const pending = active; if (!pending) return;
  pending.cancel(); await pending.settled;
}
/** Holds the native sign-in lock until the shared account/ledger handshake commits. */
export async function beginQuestIdentity(signal: AbortSignal): Promise<ManagedAuthIdentity> {
  if (active) throw new QuestLinkError('busy', 'The previous sign-in is still finishing. Try again shortly.');
  const cancellation = new AbortController();
  const attemptSignal = AbortSignal.any([signal, cancellation.signal]);
  let settle!: () => void;
  const pending = { cancel: () => { cancellation.abort(questLinkCancelled()); questPairingService.cancel(); }, settled: new Promise<void>(resolve => { settle = resolve; }) };
  active = pending;
  let auth: Auth | null = null, sdkStarted = false, done = false;
  let removeGuard = () => {}, removeActivity = () => {};
  let grant: Awaited<ReturnType<typeof questPairingService.start>> | null = null;
  const release = () => {
    removeGuard(); removeActivity(); grant?.finish();
    if (active === pending) active = null;
    settle();
  };
  const rollback = async () => {
    if (done) return; done = true;
    try {
      if (sdkStarted && auth) {
        const { signOut } = await import('firebase/auth');
        await signOut(auth); await clearManagedAccessSession({ requirePersistence: true });
        window.localStorage.removeItem(QUEST_PENDING_IDENTITY_KEY);
      }
    } catch {
      if (auth) recovered.delete(auth); // Keep the marker so restart/retry cleans up.
      throw new QuestLinkError('cleanup', 'Sign-in could not be cleared. Reopen the book before trying again.');
    } finally { release(); }
  };
  try {
    if (attemptSignal.aborted) throw questLinkCancelled();
    auth = await maestroFirebaseService.getAuth(); await recoverQuestIdentity(auth);
    if (attemptSignal.aborted) throw questLinkCancelled();
    if (auth.currentUser) throw new QuestLinkError('already-signed-in', 'This book is already signed in. Refresh your account, or sign out before linking another account.');
    await clearManagedAccessSession({ requirePersistence: true });
    grant = await questPairingService.start(attemptSignal); grant.check();
    const transaction = grant;
    removeActivity = sessionActivity.subscribe(() => {
      if (sessionActivity.status().suspended) questPairingService.cancel(new QuestLinkError('interrupted', 'Sign-in was interrupted. Return to the book and start again.'));
    });
    if (sessionActivity.status().suspended) throw new QuestLinkError('interrupted', 'Sign-in was interrupted. Return to the book and start again.');
    const sdk = await import('firebase/auth'); transaction.check();
    // This marker contains no credential. It covers the SDK's unabortable
    // persistence window as well as the later shared-backend session handshake.
    window.localStorage.setItem(QUEST_PENDING_IDENTITY_KEY, 'pending'); sdkStarted = true;
    removeGuard = sdk.beforeAuthStateChanged(auth, user => { if (user) transaction.check(); });
    await sdk.setPersistence(auth, sdk.browserLocalPersistence); transaction.check();
    const result = await sdk.signInWithCustomToken(auth, transaction.token); transaction.check();
    const token = await result.user.getIdToken(true); transaction.check();
    return {
      firebaseIdToken: token, refreshToken: result.user.refreshToken, expiresAt: null,
      user: { id: result.user.uid, email: result.user.email, displayName: result.user.displayName, photoUrl: result.user.photoURL },
      signal: transaction.signal, assertCurrent: transaction.check,
      commit: () => {
        if (done) throw questLinkCancelled(); transaction.check();
        window.localStorage.removeItem(QUEST_PENDING_IDENTITY_KEY); done = true; release();
      }, rollback,
    };
  } catch (error) {
    // Firebase wraps a blocking auth-state callback in auth/login-blocked.
    // Preserve our cancellation/deadline reason after cleanup instead of
    // misreporting a deliberate cancellation as a provider failure.
    const stopped = grant?.signal.aborted ? grant.signal : attemptSignal.aborted ? attemptSignal : null;
    await rollback();
    if (stopped) throw stopped.reason || questLinkCancelled();
    if (isLinkCancelled(error) || error instanceof QuestLinkError) throw error;
    throw new QuestLinkError('sign-in', 'Could not finish sign-in. Return to the book and try again.');
  }
}
