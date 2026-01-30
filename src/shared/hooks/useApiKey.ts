// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useState } from 'react';
import { clearApiKey, isLikelyApiKey, loadApiKey, normalizeApiKey, setApiKey } from '../../core/security/apiKeyStorage';

export const useApiKey = () => {
  const [apiKey, setApiKeyState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    loadApiKey()
      .then((value) => {
        if (!mounted) return;
        setApiKeyState(value);
        setIsLoading(false);
      })
      .catch(() => {
        if (!mounted) return;
        setApiKeyState(null);
        setIsLoading(false);
      });
    return () => {
      mounted = false;
    };
  }, []);

  const save = useCallback(async (rawValue: string) => {
    const value = normalizeApiKey(rawValue);
    if (!value) {
      setError('Please paste your Gemini API key.');
      return false;
    }
    if (!isLikelyApiKey(value)) {
      setError('That key looks too short. Please paste the full Gemini API key.');
      return false;
    }
    await setApiKey(value);
    setApiKeyState(value);
    setError(null);
    return true;
  }, []);

  const clear = useCallback(async () => {
    await clearApiKey();
    setApiKeyState(null);
    setError(null);
  }, []);

  const maskedKey = apiKey ? `${apiKey.slice(0, 4)}••••${apiKey.slice(-4)}` : null;

  return {
    apiKey,
    maskedKey,
    isLoading,
    hasKey: !!apiKey,
    error,
    setError,
    saveApiKey: save,
    clearApiKey: clear,
  };
};
