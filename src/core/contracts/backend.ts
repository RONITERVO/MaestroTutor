// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { AppUser, EntitlementRecord } from './integrations';
import type { LiveOpenReason } from '../../../shared/liveOpenReason';

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
  /** Credits actually removed from the prepaid balance. */
  chargedCredits?: number;
  /** Provider cost not covered by the reservation and remaining balance. */
  shortfallCredits?: number;
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
  /** Full provider-derived cost before balance protection, for charge rows. */
  billedCredits?: number;
  /** Provider cost deliberately not taken past a zero balance. */
  shortfallCredits?: number;
  usd: number;
  productId: string | null;
  createdAt: number;
  metadata?: Record<string, unknown>;
}

export interface ManagedBillingLedgerResponse {
  entries: ManagedBillingLedgerEntry[];
}

export interface BackendDeleteManagedAccountResponse {
  ok: true;
  deletedAt: number;
  releasedReservationCount: number;
  deletedReservationCount: number;
  deletedManagedFileCount: number;
  deletedLiveGatewayTicketCount: number;
  deletedLiveGatewaySessionCount: number;
  anonymizedPurchaseCount: number;
  anonymizedReportCount: number;
  remoteManagedFileFailures: number;
  queuedRemoteCleanupCount: number;
  deletedStripeCustomerCount: number;
}

export type AiContentReportReason =
  | 'sexual'
  | 'hate'
  | 'harassment'
  | 'self-harm'
  | 'violent'
  | 'deceptive'
  | 'spam'
  | 'other';

export interface BackendAiContentReportRequest {
  accessMode: 'byok' | 'managed';
  messageId: string;
  reason: AiContentReportReason;
  assistantText?: string;
  rawAssistantResponse?: string;
  notes?: string;
  surface?: string;
  model?: string;
  createdAtClient?: number | null;
}

export interface BackendAiContentReportResponse {
  ok: true;
  reportId: string;
  createdAt: number;
}

export interface BackendGenerateContentRequest {
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
}

export interface BackendGenerateContentResponse {
  text?: string;
  candidates?: unknown[];
  usageMetadata?: Record<string, unknown>;
  modelVersion?: string;
  promptFeedback?: unknown;
  responseId?: string;
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

export interface BackendMusicGenerationRequest {
  model: string;
  prompt: string;
  durationSeconds?: number;
}

export interface BackendMusicGenerationResponse {
  pcmBase64: string;
  durationSeconds: number;
  sampleRate: number;
  channels: number;
  sampleCount: number;
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
  purpose?: 'live';
  model: string;
  liveOpenReason: LiveOpenReason;
  config?: Record<string, unknown>;
  durationSeconds?: number;
}

export interface BackendLiveTokenResponse {
  leaseId: string;
  token: string;
  expiresAt: string | null;
  uses: number;
  billingSummary?: ManagedBillingSummary;
}

export interface BackendLiveGatewayTicketResponse {
  transport: 'gateway';
  gatewayUrl: string;
  ticket: string;
  ticketExpiresAt: string;
  sessionExpiresAt: string;
  billingSummary?: ManagedBillingSummary;
}

export interface BackendReleaseLiveTokenLeaseRequest {
  leaseId: string;
}

export interface BackendReleaseLiveTokenLeaseResponse {
  ok: boolean;
}
