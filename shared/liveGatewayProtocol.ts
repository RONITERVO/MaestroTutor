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
  | { type: 'providerMessage'; message: unknown }
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
