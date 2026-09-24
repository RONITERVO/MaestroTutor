// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useRef } from 'react';
import type { LanguageChangePorts } from '../coordinators/contracts';
import { beginLanguageChangeReset } from '../coordinators/languageChange';
import { readSpeechRoutingState } from '../speechRoutingState';

type LanguageSessionResetConfig = Omit<LanguageChangePorts, 'readState' | 'delay'> & {
  selectedLanguagePairId: string | null;
};

/** Pair changes and unmount cancel a reset; refreshed feature callbacks do not.
 * The delayed restart must use the latest committed speech callback. */
export const useLanguageSessionReset = (config: LanguageSessionResetConfig) => {
  const { selectedLanguagePairId } = config;
  const latestConfigRef = useRef(config);
  useEffect(() => { latestConfigRef.current = config; });
  const previousLanguagePairIdRef = useRef<string | null>(selectedLanguagePairId);
  useEffect(() => {
    if (selectedLanguagePairId === previousLanguagePairIdRef.current) return;
    previousLanguagePairIdRef.current = selectedLanguagePairId;
    return beginLanguageChangeReset({
      ...latestConfigRef.current,
      startListening: language => latestConfigRef.current.startListening(language),
      readState: readSpeechRoutingState,
      delay: (callback, milliseconds) => window.setTimeout(callback, milliseconds),
    });
  }, [selectedLanguagePairId]);
};
