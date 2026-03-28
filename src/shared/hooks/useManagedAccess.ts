// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useState } from 'react';
import { googleAuthService } from '../../services/auth/googleAuthService';
import {
  MANAGED_ACCESS_CHANGED_EVENT,
  hasManagedCredits,
  hasManagedSession,
  loadManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';
import type { ManagedAccessSession } from '../../core/contracts/backend';
import { maestroBackendService } from '../../services/backend/maestroBackendService';

interface ManagedAccessState {
  session: ManagedAccessSession | null;
  hasManagedSession: boolean;
  hasManagedAccess: boolean;
  isLoading: boolean;
}

export const useManagedAccess = (): ManagedAccessState & { refresh: () => Promise<void> } => {
  const [session, setSession] = useState<ManagedAccessSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    if (maestroBackendService.isConfigured()) {
      try {
        const restored = await googleAuthService.restoreManagedSession();
        setSession(restored);
        setIsLoading(false);
        return;
      } catch {
        // Fall through to cached session for offline/temporary failures.
      }
    }
    setSession(await loadManagedAccessSession());
    setIsLoading(false);
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const handleChange = () => {
      void loadManagedAccessSession().then(setSession);
    };
    window.addEventListener(MANAGED_ACCESS_CHANGED_EVENT, handleChange);
    return () => {
      window.removeEventListener(MANAGED_ACCESS_CHANGED_EVENT, handleChange);
    };
  }, []);

  return {
    session,
    hasManagedSession: hasManagedSession(session),
    hasManagedAccess: hasManagedCredits(session),
    isLoading,
    refresh,
  };
};
