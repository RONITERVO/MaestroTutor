// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { ManagedAccessSession } from '../../core/contracts/backend';
import {
  clearManagedAccessSession,
  loadManagedAccessSession,
  saveManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';
import { getManagedSessionForIdentity, maestroBackendService } from '../backend/maestroBackendService';
import { firebaseAuthBridgeService, type ManagedAuthIdentity } from './firebaseAuthBridgeService';

const DEFAULT_BILLING_SUMMARY = {
  availableCredits: 0,
  reservedCredits: 0,
  lifetimePurchasedCredits: 0,
  lifetimeSpentCredits: 0,
  lifetimeSpentUsd: 0,
  updatedAt: null,
  lastPurchaseAt: null,
  lastChargeAt: null,
  lastProductId: null,
} as const;

const persistSession = async (params: {
  firebaseIdToken: string;
  refreshToken: string | null;
  expiresAt: string | null;
  user: ManagedAccessSession['user'];
}): Promise<ManagedAccessSession> => {
  const existing = await loadManagedAccessSession();
  const session: ManagedAccessSession = {
    provider: 'firebase',
    user: params.user,
    firebaseIdToken: params.firebaseIdToken,
    refreshToken: params.refreshToken,
    expiresAt: params.expiresAt,
    entitlements: existing?.user.id === params.user.id ? existing.entitlements : [],
    billingSummary: existing?.user.id === params.user.id ? existing.billingSummary : { ...DEFAULT_BILLING_SUMMARY },
    lastSyncedAt: Date.now(),
  };
  await saveManagedAccessSession(session);
  return session;
};

export const googleAuthService = {
  beginSignIn: async (signal?: AbortSignal): Promise<ManagedAccessSession> => {
    let identity: ManagedAuthIdentity | undefined;
    try {
      identity = await firebaseAuthBridgeService.beginGoogleSignIn(signal);
      identity.assertCurrent?.();
      if (!identity.commit) await persistSession(identity);
      const backendSession = identity.commit
        ? await getManagedSessionForIdentity(identity) : await maestroBackendService.getManagedSession();
      identity.assertCurrent?.();
      const nextSession: ManagedAccessSession = {
        provider: 'firebase', user: backendSession.session.user,
        firebaseIdToken: identity.firebaseIdToken, refreshToken: identity.refreshToken, expiresAt: identity.expiresAt,
        entitlements: backendSession.session.entitlements, billingSummary: backendSession.session.billingSummary,
        lastSyncedAt: Date.now(),
      };
      if (identity.commit) await saveManagedAccessSession(nextSession, { requirePersistence: true });
      else await saveManagedAccessSession(nextSession);
      identity.assertCurrent?.();
      identity.commit?.();
      return nextSession;
    } catch (error) {
      if (identity?.rollback) await identity.rollback();
      throw error;
    }
  },

  restoreManagedSession: async (): Promise<ManagedAccessSession | null> => {
    const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
    if (!identity) {
      await clearManagedAccessSession();
      return null;
    }
    await persistSession(identity);
    const backendSession = await maestroBackendService.getManagedSession();
    const nextSession: ManagedAccessSession = {
      provider: 'firebase',
      user: backendSession.session.user,
      firebaseIdToken: identity.firebaseIdToken,
      refreshToken: identity.refreshToken,
      expiresAt: identity.expiresAt,
      entitlements: backendSession.session.entitlements,
      billingSummary: backendSession.session.billingSummary,
      lastSyncedAt: Date.now(),
    };
    await saveManagedAccessSession(nextSession);
    return nextSession;
  },

  refreshManagedSessionToken: async (): Promise<ManagedAccessSession | null> => {
    const identity = await firebaseAuthBridgeService.getCurrentIdentity(true);
    if (!identity) return null;
    return persistSession(identity);
  },

  signOutManagedSession: async (): Promise<void> => {
    try {
      await firebaseAuthBridgeService.signOut();
    } finally {
      await clearManagedAccessSession();
    }
  },
} as const;
