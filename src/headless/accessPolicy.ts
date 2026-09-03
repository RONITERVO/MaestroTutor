// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { HeadlessAccessMode } from './access';

export type HeadlessParityClass = 'local-parity' | 'provider-parity' | 'managed-account-only';
export type HeadlessCostClass = 'none' | 'provider-usage' | 'provider-storage' | 'managed-purchase';
export type HeadlessReleaseProof = 'unit' | 'paired-first-lesson' | 'paired-raw' | 'managed-billing' | 'hosted-browser';

export interface HeadlessMethodAccessPolicy {
  accessModes: readonly HeadlessAccessMode[];
  parityClass: HeadlessParityClass;
  costClass: HeadlessCostClass;
  releaseProof: HeadlessReleaseProof;
}

const BOTH = ['managed', 'byok'] as const;
const MANAGED_ONLY = ['managed'] as const;

const local = (releaseProof: HeadlessReleaseProof = 'unit'): HeadlessMethodAccessPolicy => ({
  accessModes: BOTH,
  parityClass: 'local-parity',
  costClass: 'none',
  releaseProof,
});

const provider = (
  releaseProof: HeadlessReleaseProof,
  costClass: HeadlessCostClass = 'provider-usage',
): HeadlessMethodAccessPolicy => ({
  accessModes: BOTH,
  parityClass: 'provider-parity',
  costClass,
  releaseProof,
});

const managed = (
  releaseProof: HeadlessReleaseProof,
  costClass: HeadlessCostClass = 'none',
): HeadlessMethodAccessPolicy => ({
  accessModes: MANAGED_ONLY,
  parityClass: 'managed-account-only',
  costClass,
  releaseProof,
});

/**
 * Authoritative access and cost classification for every public headless method.
 *
 * New methods must be placed here before they can be advertised or dispatched.
 * Provider-parity methods are required to work through both the direct BYOK SDK
 * and the managed transport; managed-account-only methods have no honest BYOK
 * equivalent and are intentionally rejected before any external action.
 */
export const HEADLESS_METHOD_ACCESS_POLICY = {
  'system.describe': local(),
  'profile.get': local(),
  'auth.status': local(),
  'auth.signIn': managed('unit'),
  'auth.signOut': managed('unit'),
  'auth.google.verifyHosted': managed('hosted-browser'),
  'language.list': local(),
  'language.select': local(),
  'chat.history': local(),
  'chat.turn': provider('paired-first-lesson'),
  'chat.attachment.turn': provider('paired-first-lesson', 'provider-storage'),
  'suggestions.generate': provider('paired-first-lesson'),
  'suggestions.process': provider('paired-first-lesson'),
  'translation.create': provider('paired-first-lesson'),
  'chat.reengage': provider('paired-first-lesson'),
  'media.image.generate': provider('paired-first-lesson'),
  'media.audioNote.generate': provider('paired-first-lesson'),
  'media.music.generate': provider('paired-first-lesson'),
  'speech.synthetic.live': provider('paired-first-lesson'),
  'speech.transcribe': provider('paired-first-lesson'),
  'speech.tts.generate': provider('paired-first-lesson'),
  'live.conversation.turn': provider('paired-first-lesson'),
  'live.observer.turn': provider('paired-first-lesson'),
  'journey.firstLesson': provider('paired-first-lesson'),
  'account.summary': managed('managed-billing'),
  'account.ledgers': managed('managed-billing'),
  'account.delete': managed('managed-billing'),
  'billing.checkout.create': managed('managed-billing', 'managed-purchase'),
  'billing.checkout.reconcile': managed('managed-billing', 'managed-purchase'),
  'billing.checkout.completeTest': managed('managed-billing', 'managed-purchase'),
  // Reports are an optional-auth backend route in the real UI. BYOK reports
  // therefore remain supported and must be labelled with the actual client mode.
  'report.submit': provider('unit', 'none'),
  'gemini.generate': provider('paired-raw'),
  'gemini.generateStream': provider('paired-raw'),
  'files.upload': provider('paired-first-lesson', 'provider-storage'),
  'files.status': provider('paired-first-lesson', 'provider-storage'),
  'files.delete': provider('paired-first-lesson', 'provider-storage'),
  'files.clear': provider('paired-first-lesson', 'provider-storage'),
} as const satisfies Record<string, HeadlessMethodAccessPolicy>;

export type HeadlessMethodName = keyof typeof HEADLESS_METHOD_ACCESS_POLICY;

export const HEADLESS_METHODS = Object.freeze(
  Object.keys(HEADLESS_METHOD_ACCESS_POLICY) as HeadlessMethodName[],
);

export const getHeadlessMethodAccessPolicy = (
  method: string,
): HeadlessMethodAccessPolicy | null => (
  Object.prototype.hasOwnProperty.call(HEADLESS_METHOD_ACCESS_POLICY, method)
    ? HEADLESS_METHOD_ACCESS_POLICY[method as HeadlessMethodName]
    : null
);

export const isHeadlessMethodAvailable = (
  method: string,
  accessMode: HeadlessAccessMode,
): boolean => Boolean(getHeadlessMethodAccessPolicy(method)?.accessModes.includes(accessMode));

export const assertHeadlessMethodAvailable = (
  method: string,
  accessMode: HeadlessAccessMode,
): void => {
  const policy = getHeadlessMethodAccessPolicy(method);
  if (!policy || policy.accessModes.includes(accessMode)) return;
  throw new Error(
    `${method} requires managed access because it operates on a Maestro account, managed-credit purchase or managed gateway ticket.`,
  );
};

export const describeHeadlessAccessPolicy = (accessMode: HeadlessAccessMode) => ({
  accessMode,
  availableMethods: HEADLESS_METHODS.filter(method => isHeadlessMethodAvailable(method, accessMode)),
  unavailableMethods: HEADLESS_METHODS.filter(method => !isHeadlessMethodAvailable(method, accessMode)),
  providerParityMethods: HEADLESS_METHODS.filter(
    method => HEADLESS_METHOD_ACCESS_POLICY[method].parityClass === 'provider-parity',
  ),
  managedAccountOnlyMethods: HEADLESS_METHODS.filter(
    method => HEADLESS_METHOD_ACCESS_POLICY[method].parityClass === 'managed-account-only',
  ),
});
