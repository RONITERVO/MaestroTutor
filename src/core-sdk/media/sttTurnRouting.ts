// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export type SttTurnDestination = 'message' | 'translation';

export interface SttStartOptions {
  language?: string;
  lastAssistantMessage?: string;
  replySuggestions?: string[];
  /** Captures where the utterance should go when listening starts. */
  destination?: SttTurnDestination;
}

/**
 * A translation utterance must never fall through to normal chat submission.
 * The captured destination survives async speech detection; the live UI mode
 * remains a compatibility fallback for sessions started by older call sites.
 */
export const resolveSttTurnDestination = (
  capturedDestination: SttTurnDestination | undefined,
  isTranslationViewOpen: boolean,
): SttTurnDestination => (
  capturedDestination === 'translation' || isTranslationViewOpen
    ? 'translation'
    : 'message'
);
