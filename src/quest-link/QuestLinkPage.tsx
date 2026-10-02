// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { ManagedAuthIdentity } from '../services/auth/firebaseAuthBridgeService';
import { isLinkCancelled, QuestLinkError, withLinkAbort } from '../services/auth/questLinkProtocol';
import { questApprovalService, type QuestApprovalAdapter } from './questApprovalService';
export const normalizedPairingCode = (input: string) => input.toUpperCase().replace(/[\s-]/g, '');
const primary = 'w-full bg-gate-btn-bg px-4 py-3 font-medium text-gate-btn-text disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin';
const secondary = 'px-3 py-2 underline text-gate-accent focus:outline-none focus:ring-2 focus:ring-gate-accent';
export default function QuestLinkPage({ adapter = questApprovalService }: { adapter?: QuestApprovalAdapter }) {
  const available = adapter.available();
  const [identity, setIdentity] = useState<ManagedAuthIdentity | null>(null);
  const [booting, setBooting] = useState(available);
  const [busy, setBusy] = useState(false);
  const [code, setCode] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [approved, setApproved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const operation = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true; let current = true;
    const restore = new AbortController();
    const deadline = setTimeout(() => restore.abort(), 30_000);
    if (available) void withLinkAbort(adapter.identity(), restore.signal).then(value => { if (current) setIdentity(value); })
      .catch(() => { if (current) setError('Could not restore your account. Sign in with Google to continue.'); })
      .finally(() => { clearTimeout(deadline); if (current) setBooting(false); });
    return () => { current = false; mounted.current = false; clearTimeout(deadline); restore.abort(); operation.current?.abort(); };
  }, [adapter, available]);
  const signIn = async () => {
    if (busy || booting) return;
    setBusy(true); setError(null); setConfirmed(false);
    try { const user = await adapter.signIn(); if (mounted.current) setIdentity(user); }
    catch { if (mounted.current) setError('Google sign-in did not finish. Please try again.'); }
    finally { if (mounted.current) setBusy(false); }
  };
  const signOut = async () => {
    if (busy || booting) return;
    setBusy(true); setError(null); setConfirmed(false);
    try { await adapter.signOut(); if (mounted.current) setIdentity(null); }
    catch { if (mounted.current) setError('Sign-out did not finish. Please try again.'); }
    finally { if (mounted.current) setBusy(false); }
  };
  const normalized = normalizedPairingCode(code);
  const valid = /^[A-HJ-NP-Z2-9]{10}$/.test(normalized);
  const approve = async (event: FormEvent) => {
    event.preventDefault();
    if (!identity || !valid || !confirmed || busy || booting || !available || operation.current) return;
    const request = new AbortController(); operation.current = request;
    const deadline = setTimeout(() => request.abort(new QuestLinkError('timeout', 'Approval timed out. Check the book before trying again.')), 60_000);
    setBusy(true); setError(null);
    try {
      await withLinkAbort(adapter.approve(normalized, identity.user.id, request.signal), request.signal);
      if (mounted.current && !request.signal.aborted) setApproved(true);
    } catch (failure) {
      if (mounted.current && !isLinkCancelled(failure)) {
        setError(failure instanceof QuestLinkError ? failure.message : 'Approval did not finish. Check the book before trying again.');
        setConfirmed(false);
      }
    } finally {
      clearTimeout(deadline); if (operation.current === request) operation.current = null;
      if (mounted.current) setBusy(false);
    }
  };
  return <main className="min-h-screen bg-page-bg paper-texture px-4 py-8 text-page-text">
    <div className="mx-auto max-w-md space-y-5">
      <header><a href="/" className="font-sketch text-2xl text-page-text">Maestro</a><h1 className="mt-5 font-sketch text-3xl font-semibold">Link your Quest book</h1></header>
      {!available ? <section role="status" className="bg-gate-bg p-5 sketchy-border-thin"><p>Account linking is not configured on this page yet.</p><p className="mt-3">You can keep using your own API key in the book.</p></section>
        : approved ? <section role="status" className="space-y-3 bg-gate-bg p-5 sketchy-border-thin"><h2 className="text-xl font-semibold">Your book is approved</h2><p>Return to Maestro on your Quest. The book will finish signing in to your existing account.</p><p>You can close this browser page.</p></section>
          : <section className="space-y-4 bg-gate-bg p-5 text-gate-text sketchy-border-thin">
            <p>Use the code displayed in the Maestro book you are signing in to. This links the book to your existing account and shared credit balance.</p>
            {booting ? <p role="status">Checking your account…</p> : identity ? <div className="space-y-1"><p className="text-sm text-gate-muted-text">Linking as</p><p className="break-all font-semibold">{identity.user.email || identity.user.displayName || 'Your Maestro account'}</p><button type="button" className={secondary} disabled={busy} onClick={() => void signOut()}>Use another account</button><button type="button" className={secondary} disabled={busy} onClick={() => void signIn()}>Sign in again</button></div>
              : <button type="button" className={primary} disabled={busy} onClick={() => void signIn()}>{busy ? 'Signing in…' : 'Sign in with Google'}</button>}
            <form onSubmit={event => void approve(event)} className="space-y-4">
              <label className="block space-y-2"><span className="font-medium">Code from your book</span><input type="text" autoComplete="off" autoCapitalize="characters" spellCheck={false} maxLength={16} value={code} onChange={event => { setCode(event.target.value); setConfirmed(false); }} disabled={busy || booting} placeholder="ABCDE-FGHJK" className="w-full border border-gate-muted-text bg-gate-input-bg px-3 py-3 font-mono text-xl tracking-wider text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent" /></label>
              <label className="flex items-start gap-3 text-sm"><input type="checkbox" className="mt-1 h-5 w-5 shrink-0" checked={confirmed} disabled={busy || !valid || !identity} onChange={event => setConfirmed(event.target.checked)} /><span>I started sign-in in my own Maestro book and this code matches its screen.</span></label>
              <p className="text-sm text-gate-muted-text">Do not approve a code sent by someone else. Approval gives that book access to your Maestro account.</p>
              <button className={primary} type="submit" disabled={!identity || !valid || !confirmed || busy || booting}>{busy && identity ? 'Connecting…' : 'Link my Quest book'}</button>
            </form>
          </section>}
      {error && <p role="alert" className="border border-notice-error-border bg-notice-error-bg p-3 text-notice-error-text">{error}</p>}
      <footer className="flex gap-5 text-sm"><a className="underline" href="/">Back to Maestro</a><a className="underline" href="/privacy.html">Privacy</a></footer>
    </div>
  </main>;
}
