// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG } from '../../core/config/integrations';
import type {
  BackendGenerateContentResponse,
  ManagedBillingSummary,
} from '../../core/contracts/backend';
import type { EntitlementRecord } from '../../core/contracts/integrations';
import {
  createManagedBackendClient,
  readManagedGenerationStream as readCoreManagedGenerationStream,
} from '../../core-sdk/managedBackendClient';
import {
  loadManagedAccessSession,
  saveManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';
import { firebaseAuthBridgeService } from '../auth/firebaseAuthBridgeService';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { ServiceHttpError } from '../shared/serviceErrors';

/**
 * Every managed route is behind App Check, so a request that leaves without the
 * header is a round trip whose only possible answer is 401. Failing here keeps
 * the reason — the attestation error the device actually hit — attached to a
 * code the UI can explain, instead of letting the backend's internal wording
 * ("Missing Firebase App Check token.") become the message a user reads.
 */
export const APP_CHECK_UNAVAILABLE_CODE = 'app-check/unavailable';

const requireAppCheckHeader = async (): Promise<Record<string, string>> => {
  const appCheckToken = await maestroFirebaseService.getAppCheckToken(false);
  if (appCheckToken) return { 'X-Firebase-AppCheck': appCheckToken };
  throw new ServiceHttpError(
    maestroFirebaseService.getAppCheckFailureReason() || 'This device could not obtain a Firebase App Check token.',
    401,
    APP_CHECK_UNAVAILABLE_CODE,
  );
};

const updateStoredSession = async (updates: {
  billingSummary?: ManagedBillingSummary | null;
  entitlements?: EntitlementRecord[] | null;
}) => {
  const currentSession = await loadManagedAccessSession();
  if (!currentSession) return;
  await saveManagedAccessSession({
    ...currentSession,
    billingSummary: updates.billingSummary || currentSession.billingSummary,
    entitlements: updates.entitlements || currentSession.entitlements,
    lastSyncedAt: Date.now(),
  });
};

const getOptionalHeaders = async (): Promise<Record<string, string>> => {
  const headers: Record<string, string> = {};
  const session = await loadManagedAccessSession();
  if (session) {
    const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
    const token = identity?.firebaseIdToken || session.firebaseIdToken;
    if (token) headers.Authorization = `Bearer ${token}`;
    if (identity && identity.firebaseIdToken !== session.firebaseIdToken) {
      await saveManagedAccessSession({
        ...session,
        user: identity.user,
        firebaseIdToken: identity.firebaseIdToken,
        refreshToken: identity.refreshToken,
        expiresAt: identity.expiresAt,
        lastSyncedAt: Date.now(),
      });
    }
  }
  return { ...headers, ...await requireAppCheckHeader() };
};

const getManagedHeaders = async (): Promise<Record<string, string>> => {
  const session = await loadManagedAccessSession();
  if (!session?.user?.id) throw new Error('Managed access session is missing.');
  const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
  const token = identity?.firebaseIdToken || session.firebaseIdToken;
  if (!token) throw new Error('Managed access session is missing.');
  if (identity && identity.firebaseIdToken !== session.firebaseIdToken) {
    await saveManagedAccessSession({
      ...session,
      user: identity.user,
      firebaseIdToken: identity.firebaseIdToken,
      refreshToken: identity.refreshToken,
      expiresAt: identity.expiresAt,
      lastSyncedAt: Date.now(),
    });
  }
  return { Authorization: `Bearer ${token}`, ...await requireAppCheckHeader() };
};

export const maestroBackendService = createManagedBackendClient({
  baseUrl: MAESTRO_INTEGRATION_CONFIG.backendBaseUrl,
  credentials: { getManagedHeaders, getOptionalHeaders },
  session: { update: updateStoredSession },
});

// Preserve this browser-adapter helper for tests and diagnostics. Production
// generation uses the same Core SDK parser through generateContentStream.
export const readManagedGenerationStream = (
  response: Response,
): AsyncGenerator<unknown> => readCoreManagedGenerationStream(
  response,
  (result?: BackendGenerateContentResponse) => updateStoredSession({
    billingSummary: result?.billingSummary || null,
  }),
);
