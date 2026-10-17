// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isFirebaseClientConfigured } from '../core/config/integrations';
import { firebaseAuthBridgeService, type ManagedAuthIdentity } from '../services/auth/firebaseAuthBridgeService';
import { maestroFirebaseService } from '../services/firebase/maestroFirebaseService';
import { checkLinkSignal, linkHttpsUrl, postQuestLink, QuestLinkError, withLinkAbort } from '../services/auth/questLinkProtocol';
export interface QuestApprovalAdapter {
  available(): boolean;
  identity(): Promise<ManagedAuthIdentity | null>;
  signIn(): Promise<ManagedAuthIdentity>;
  signOut(): Promise<void>;
  approve(code: string, expectedUserId: string, signal: AbortSignal): Promise<void>;
}
export const questApprovalService: QuestApprovalAdapter = {
  available: () => {
    try {
      const expected = new URL(linkHttpsUrl(MAESTRO_INTEGRATION_CONFIG.questAccountLinkVerificationUrl, true));
      linkHttpsUrl(MAESTRO_INTEGRATION_CONFIG.questAccountLinkUrl);
      return isFirebaseClientConfigured() && Boolean(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey)
        && window.top === window && window.location.origin === expected.origin && window.location.pathname === expected.pathname;
    } catch { return false; }
  },
  identity: () => firebaseAuthBridgeService.getCurrentIdentity(),
  signIn: () => firebaseAuthBridgeService.beginGoogleSignIn(),
  signOut: () => firebaseAuthBridgeService.signOut(),
  approve: async (code, expectedUserId, signal) => {
    if (!questApprovalService.available()) throw new QuestLinkError('not-configured', 'Account linking is not available on this page.');
    const identity = await withLinkAbort(firebaseAuthBridgeService.getCurrentIdentity(true), signal); checkLinkSignal(signal);
    if (!identity) throw new QuestLinkError('sign-in-required', 'Sign in with Google before linking your book.');
    if (identity.user.id !== expectedUserId) throw new QuestLinkError('account-changed', 'Your account changed in another tab. Sign in again and check the account before approving.');
    const proof = await withLinkAbort(maestroFirebaseService.getAppCheckToken(), signal); checkLinkSignal(signal);
    const result = await postQuestLink({ base: MAESTRO_INTEGRATION_CONFIG.questAccountLinkUrl, operation: 'approve',
      body: { code, confirm: true }, appCheck: proof || '', bearer: identity.firebaseIdToken, signal });
    if (result.approved !== true) throw new QuestLinkError('invalid-response', 'Maestro could not confirm your approval. Check the book before trying again.');
  },
};
