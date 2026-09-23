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
export const runTutorTextTurn = (input: TutorTextTurnInput, options: BrowserOptions<TutorTextTurnOptions> = {}) =>
  runText(input, { ...options, ...browserClientSource(options.aiClient) });

export const runReplySuggestions = (input: ReplySuggestionsInput, options: BrowserOptions<ReplySuggestionsOptions> = {}) =>
  runSuggestions(input, { ...options, ...browserClientSource(options.aiClient), sanitizeSvg: sanitizeSvgAnimationStructure });

export const runMaestroImageGeneration = (input: MaestroImageGenerationInput, options: BrowserOptions<MaestroImageGenerationOptions> = {}) =>
  runImage(input, { ...options, ...browserClientSource(options.aiClient), onUsage: trackGeminiUsage });
