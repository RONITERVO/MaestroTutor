// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { LanguagePair } from '../../core/types';
import { generateAllLanguagePairs } from '../../shared/utils/languageUtils';

let cachedPairs: LanguagePair[] | null = null;

export const listLanguagePairs = (): LanguagePair[] => {
  if (!cachedPairs) cachedPairs = generateAllLanguagePairs();
  return cachedPairs;
};

export const resolveLanguagePair = (input: {
  pairId?: string | null;
  targetLanguageCode?: string | null;
  nativeLanguageCode?: string | null;
}): LanguagePair => {
  const pairs = listLanguagePairs();
  const pairId = input.pairId?.trim();
  const target = input.targetLanguageCode?.trim().toLowerCase();
  const native = input.nativeLanguageCode?.trim().toLowerCase();
  const pair = pairId
    ? pairs.find(candidate => candidate.id === pairId)
    : pairs.find(candidate => (
      candidate.targetLanguageCode.toLowerCase() === target
      && candidate.nativeLanguageCode.toLowerCase() === native
    ));
  if (!pair) {
    throw new Error(pairId
      ? `Unknown language pair: ${pairId}`
      : `Unknown target/native language combination: ${input.targetLanguageCode || ''}/${input.nativeLanguageCode || ''}`);
  }
  return pair;
};
