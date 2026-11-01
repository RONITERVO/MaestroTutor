// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG } from '../../core/config/integrations';
import { currentQuestIntegrityClient } from '../../platform/quest/questIntegrityBridge';
import { sessionActivity } from '../../platform/browser/sessionActivity';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { QuestPairingClient } from './questPairingClient';
export const questPairingService = new QuestPairingClient({
  base: () => MAESTRO_INTEGRATION_CONFIG.questAccountLinkUrl,
  verificationUrl: () => MAESTRO_INTEGRATION_CONFIG.questAccountLinkVerificationUrl,
  getProof: () => maestroFirebaseService.getAppCheckToken(), activity: sessionActivity,
  owner: () => currentQuestIntegrityClient()?.lifetime.signal || null,
});
