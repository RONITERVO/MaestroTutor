// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { runTutorTextTurn as runText, type TutorTextTurnInput, type TutorTextTurnOptions } from '../../core-sdk/chat/tutorTextTurn';
import { runReplySuggestions as runSuggestions, type ReplySuggestionsInput, type ReplySuggestionsOptions } from '../../core-sdk/chat/suggestions';
import { runMaestroImageGeneration as runImage, type MaestroImageGenerationInput, type MaestroImageGenerationOptions } from '../../core-sdk/chat/imageGeneration';
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import { trackGeminiUsage } from '../../shared/utils/costTracker';
import { sanitizeSvgAnimationStructure } from '../../platform/browser/sanitizeSvgAnimationStructure';
import { browserClientSource } from './browserClientSource';

type BrowserOptions<T> = Omit<T, 'aiClient' | 'resolveAiClient' | 'onUsage' | 'sanitizeSvg'> & { aiClient?: CoreGeminiClient };

/** Composition boundary: the UI supplies browser access and persistence, Core owns the journey. */
export const runTutorTextTurn = async (input: TutorTextTurnInput, options: BrowserOptions<TutorTextTurnOptions> & { isCurrent?: () => boolean } = {}) => {
  const { isCurrent, ...coreOptions } = options;
  const configured = { ...coreOptions, ...browserClientSource(options.aiClient) };
  if (!isCurrent) return runText(input, configured);

  const controller = new AbortController();
  const cancel = () => controller.abort();
  const current = () => {
    if (options.signal?.aborted || !isCurrent()) cancel();
    return !controller.signal.aborted;
  };
  options.signal?.addEventListener('abort', cancel, { once: true });
  current();
  // A stalled stream or retry delay emits no UI callbacks, so watch ownership
  // independently and forward cancellation to both managed and BYOK transports.
  const watcher = setInterval(current, 100);
  try {
    return await runText(input, { ...configured, signal: controller.signal,
      onGoogleSearchUnavailable: () => { if (current()) options.onGoogleSearchUnavailable?.(); },
      lifecycleHooks: {
        onProgress: event => { if (current()) options.lifecycleHooks?.onProgress?.(event); },
        onTextDelta: (delta, full) => { if (current()) options.lifecycleHooks?.onTextDelta?.(delta, full); },
        onThoughtDelta: (delta, full) => { if (current()) options.lifecycleHooks?.onThoughtDelta?.(delta, full); },
      },
    });
  } finally {
    clearInterval(watcher);
    options.signal?.removeEventListener('abort', cancel);
  }
};

export const runReplySuggestions = (input: ReplySuggestionsInput, options: BrowserOptions<ReplySuggestionsOptions> = {}) =>
  runSuggestions(input, { ...options, ...browserClientSource(options.aiClient), sanitizeSvg: sanitizeSvgAnimationStructure });

export const runMaestroImageGeneration = (input: MaestroImageGenerationInput, options: BrowserOptions<MaestroImageGenerationOptions> = {}) =>
  runImage(input, { ...options, ...browserClientSource(options.aiClient), onUsage: trackGeminiUsage });
