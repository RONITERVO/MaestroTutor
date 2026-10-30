// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * The worklet acknowledgement means its last render quantum was submitted, not
 * necessarily that Android's hardware buffer has emitted it. Retain the graph
 * for the reported device latency plus a small render margin.
 */
export const getAudioOutputTailDelayMs = (
  context: Pick<AudioContext, 'baseLatency'> & Partial<Pick<AudioContext, 'outputLatency'>>,
): number => {
  const baseLatency = Number.isFinite(context.baseLatency) ? Math.max(0, context.baseLatency) : 0;
  const outputLatency = Number.isFinite(context.outputLatency)
    ? Math.max(0, Number(context.outputLatency))
    : 0;
  return Math.min(1_000, Math.max(120, Math.ceil((baseLatency + outputLatency) * 1_000) + 50));
};
