// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Cost controls shared by managed and BYOK Live sessions. The managed backend
 * reapplies them after request validation, so a modified client cannot enlarge
 * the provider context or camera token budget.
 */
export const LIVE_CONTEXT_COMPRESSION_TRIGGER_TOKENS = '25000';
export const LIVE_CONTEXT_COMPRESSION_TARGET_TOKENS = '8000';

export const getLiveCostControlConfig = (): Record<string, unknown> => ({
  contextWindowCompression: {
    triggerTokens: LIVE_CONTEXT_COMPRESSION_TRIGGER_TOKENS,
    slidingWindow: { targetTokens: LIVE_CONTEXT_COMPRESSION_TARGET_TOKENS },
  },
  mediaResolution: 'MEDIA_RESOLUTION_LOW',
});

export const applyLiveCostControls = (
  config: Record<string, unknown> | undefined,
): Record<string, unknown> => ({
  ...(config || {}),
  ...getLiveCostControlConfig(),
});
