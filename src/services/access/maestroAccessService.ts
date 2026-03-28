// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { loadApiKey } from '../../core/security/apiKeyStorage';
import {
  hasManagedCredits,
  hasManagedSession,
  loadManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';

export type AppAccessMode = 'none' | 'byok' | 'managed';

export const maestroAccessService = {
  resolveAccessMode: async (): Promise<AppAccessMode> => {
    const apiKey = await loadApiKey();
    if (apiKey) return 'byok';

    const managedSession = await loadManagedAccessSession();
    if (hasManagedSession(managedSession)) return 'managed';

    return 'none';
  },

  isUsingManagedAccess: async (): Promise<boolean> => (
    (await maestroAccessService.resolveAccessMode()) === 'managed'
  ),

  hasManagedCredits: async (): Promise<boolean> => {
    const session = await loadManagedAccessSession();
    return hasManagedCredits(session);
  },
} as const;
