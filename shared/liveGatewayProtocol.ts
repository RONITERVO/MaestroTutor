// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Small JSON protocol between a managed client and the server-observed Live
 * gateway. Provider configuration never crosses this socket: the gateway
 * reads the model and sanitized config from the one-use ticket record.
 */

export interface LiveGatewayBillingSummary {
  availableCredits: number;
  reservedCredits: number;
  lifetimePurchasedCredits: number;
  lifetimeSpentCredits: number;
  lifetimeSpentUsd: number;
  updatedAt: number | null;
  lastPurchaseAt: number | null;
  lastChargeAt: number | null;
  lastProductId: string | null;
}

export type LiveGatewayClientMessage =
  | { type: 'authenticate'; ticket: string }
  | { type: 'realtimeInput'; input: Record<string, unknown> }
  | { type: 'clientContent'; input: Record<string, unknown> }
  | { type: 'toolResponse'; input: Record<string, unknown> }
  | { type: 'close' };

export type LiveGatewayServerMessage =
  | { type: 'ready'; sessionId: string; deadlineAt: number }
  | {
      type: 'providerMessage';
      message: unknown;
      /** Empty provider envelope keeps older clients connected for the answer. */
      inputTurnEnded?: { reason: 'duration-limit' | 'reply-window' | 'buffer-limit' | 'model-reply'; maxDurationMs: number };
    }
  | {
      type: 'billing';
      status: 'finalizing' | 'settled' | 'released';
      billedCredits: number;
      billedUsd: number;
      usefulOutput: boolean;
      usageSource: string;
      billingSummary?: LiveGatewayBillingSummary;
    }
  | { type: 'error'; message: string; code?: string; retryable?: boolean };

export const LIVE_GATEWAY_AUTH_TIMEOUT_MS = 5_000;
export const LIVE_GATEWAY_CONNECT_TIMEOUT_MS = 20_000;
export const LIVE_GATEWAY_MAX_MESSAGE_BYTES = 2_000_000;
/** One minute of input, followed by time for the model to answer. */
export const LIVE_USER_TURN_MAX_MS = 60_000;
export const LIVE_GATEWAY_REPLY_RESERVE_MS = 30_000;
/** Bound retained work even when a client sends faster than microphone cadence. */
export const LIVE_GATEWAY_MAX_QUEUED_BYTES = 4 * 1024 * 1024;
export const LIVE_GATEWAY_MAX_QUEUED_MESSAGES = 1024;
/** A managed socket ends after this many complete user/model exchanges. */
export const LIVE_GATEWAY_MAX_TURNS = 1;
/** Camera input is admitted at the same one-frame-per-second cadence as the app. */
export const LIVE_GATEWAY_VIDEO_FRAME_INTERVAL_MS = 1_000;
