// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { AppUser, EntitlementRecord } from './integrations';

export interface ManagedBillingSummary {
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

export interface ManagedAccessSession {
  provider: 'firebase';
  user: AppUser;
  firebaseIdToken: string;
  refreshToken: string | null;
  expiresAt: string | null;
  entitlements: EntitlementRecord[];
  billingSummary: ManagedBillingSummary;
  lastSyncedAt: number;
}

export interface ManagedSessionResponse {
  session: Omit<ManagedAccessSession, 'firebaseIdToken' | 'refreshToken' | 'expiresAt' | 'lastSyncedAt'>;
}

export interface ManagedAccountSummary {
  user: AppUser;
  billingSummary: ManagedBillingSummary;
  entitlements: EntitlementRecord[];
}

export interface ManagedAccountSummaryResponse {
  account: ManagedAccountSummary;
}

export interface ManagedUsageLedgerEntry {
  id: string;
  operation: string;
  model: string;
  billedCredits: number;
  billedUsd: number;
  createdAt: number;
  metadata?: Record<string, unknown>;
}

export interface ManagedUsageLedgerResponse {
  entries: ManagedUsageLedgerEntry[];
}

export interface ManagedBillingLedgerEntry {
  id: string;
  kind: 'purchase' | 'charge' | 'reservation-release';
  credits: number;
  usd: number;
  productId: string | null;
  createdAt: number;
  metadata?: Record<string, unknown>;
}

export interface ManagedBillingLedgerResponse {
  entries: ManagedBillingLedgerEntry[];
}

export interface BackendGenerateContentRequest {
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
  operation?: string;
}

export interface BackendGenerateContentResponse {
  text?: string;
  candidates?: unknown[];
  usageMetadata?: Record<string, unknown>;
  billingSummary?: ManagedBillingSummary;
}

export interface BackendMediaUploadRequest {
  dataUrl: string;
  mimeType: string;
  displayName?: string;
}

export interface BackendMediaUploadResponse {
  uri: string;
  mimeType: string;
  billingSummary?: ManagedBillingSummary;
}

export interface BackendFileStatus {
  deleted: boolean;
  active: boolean;
}

export interface BackendFileStatusesRequest {
  uris: string[];
}

export interface BackendFileStatusesResponse {
  statuses: Record<string, BackendFileStatus>;
}

export interface BackendDeleteFileRequest {
  nameOrUri: string;
}

export interface BackendDeleteFileResponse {
  ok: boolean;
}

export interface BackendClearFilesResponse {
  deletedCount: number;
  failedCount: number;
  failedNames: string[];
}

export interface BackendLiveTokenRequest {
  purpose?: 'live' | 'music';
  durationSeconds?: number;
}

export interface BackendLiveTokenResponse {
  leaseId: string;
  token: string;
  expiresAt: string | null;
  uses: number;
  billingSummary?: ManagedBillingSummary;
}

export interface BackendReleaseLiveTokenLeaseRequest {
  leaseId: string;
}

export interface BackendReleaseLiveTokenLeaseResponse {
  ok: boolean;
}
