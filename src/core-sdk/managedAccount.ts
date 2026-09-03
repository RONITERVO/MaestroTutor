// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {
  AiContentReportReason,
  BackendAiContentReportRequest,
  BackendAiContentReportResponse,
  BackendDeleteManagedAccountResponse,
  ManagedAccountSummaryResponse,
  ManagedBillingLedgerResponse,
  ManagedUsageLedgerResponse,
} from '../core/contracts/backend';
import type { ChatMessage } from '../core/types';
import { createCoreRuntime, type CoreRuntime } from './runtime';

export interface ManagedAccountBackendPort {
  getAccountSummary(): Promise<ManagedAccountSummaryResponse>;
  listUsageLedger(limit?: number): Promise<ManagedUsageLedgerResponse>;
  listBillingLedger(limit?: number): Promise<ManagedBillingLedgerResponse>;
  createStripeCheckoutSession(packId: string): Promise<{ url: string; sessionId: string }>;
  submitAiContentReport(payload: BackendAiContentReportRequest): Promise<BackendAiContentReportResponse>;
  deleteManagedAccount(): Promise<BackendDeleteManagedAccountResponse>;
}

export interface ManagedIdentityPort {
  beginSignIn(): Promise<unknown>;
  signOut(): Promise<void>;
}

export interface ExternalNavigationPort {
  navigate(url: string): Promise<void> | void;
}

export interface StripeReturnPoll {
  operationId: string;
  completion: Promise<ManagedAccountSummaryResponse | null>;
  cancel(): void;
}

export interface ManagedAccountController {
  signIn(operationId?: string): Promise<ManagedAccountSummaryResponse>;
  signOut(operationId?: string): Promise<void>;
  refreshAccount(operationId?: string): Promise<ManagedAccountSummaryResponse>;
  listLedgers(limit?: number, operationId?: string): Promise<{
    usage: ManagedUsageLedgerResponse;
    billing: ManagedBillingLedgerResponse;
  }>;
  startStripeCheckout(packId: string, operationId?: string): Promise<{ url: string; sessionId: string }>;
  startStripeReturnPolling(options?: {
    attempts?: number;
    intervalMs?: number;
    operationId?: string;
    refresh?: () => Promise<ManagedAccountSummaryResponse | null>;
    onAttempt?: (attempt: number, result: ManagedAccountSummaryResponse | null, error: unknown | null) => void;
    isComplete?: (result: ManagedAccountSummaryResponse) => boolean;
  }): StripeReturnPoll;
  submitAiContentReport(
    payload: BackendAiContentReportRequest,
    operationId?: string,
  ): Promise<BackendAiContentReportResponse>;
  deleteAccount(options: {
    confirmation: string;
    expectedUserId?: string;
    actualUserId?: string;
    operationId?: string;
  }): Promise<BackendDeleteManagedAccountResponse>;
}

const errorData = (error: unknown): Record<string, unknown> => ({
  errorName: error instanceof Error ? error.name : 'UnknownError',
  errorMessage: error instanceof Error ? error.message : String(error),
});

const visibleAssistantText = (message: ChatMessage): string => {
  if (message.text?.trim()) return message.text.trim();
  if (message.translations?.length) {
    return message.translations
      .map(pair => [pair.target, pair.native].filter(Boolean).join(' / '))
      .filter(Boolean)
      .join('\n');
  }
  return '';
};

export const buildAiContentReportRequest = (options: {
  message: ChatMessage;
  accessMode: 'byok' | 'managed';
  reason: AiContentReportReason;
  notes?: string;
  surface?: string;
  model?: string;
}): BackendAiContentReportRequest => ({
  accessMode: options.accessMode,
  messageId: options.message.id,
  reason: options.reason,
  assistantText: visibleAssistantText(options.message) || undefined,
  rawAssistantResponse: options.message.llmRawResponse || options.message.rawAssistantResponse || undefined,
  notes: options.notes?.trim() || undefined,
  surface: options.surface || 'chat',
  model: options.model,
  createdAtClient: Number.isFinite(options.message.timestamp) ? options.message.timestamp : null,
});

export const createManagedAccountController = (dependencies: {
  backend: ManagedAccountBackendPort;
  identity: ManagedIdentityPort;
  navigation: ExternalNavigationPort;
  runtime?: CoreRuntime;
}): ManagedAccountController => {
  const runtime = dependencies.runtime || createCoreRuntime();
  const operationId = (given: string | undefined, prefix: string) => given || runtime.ids.create(prefix);
  const emit = (
    id: string,
    journey: 'access' | 'account' | 'billing' | 'report',
    phase: string,
    data?: Record<string, unknown>,
  ) => runtime.events.emit({ operationId: id, journey, phase, at: runtime.clock.now(), data });

  const refreshAccount = async (givenOperationId?: string) => {
    const id = operationId(givenOperationId, 'account-refresh');
    emit(id, 'account', 'refresh.started');
    try {
      const response = await dependencies.backend.getAccountSummary();
      emit(id, 'account', 'refresh.succeeded', {
        userId: response.account.user.id,
        availableCredits: response.account.billingSummary.availableCredits,
      });
      return response;
    } catch (error) {
      emit(id, 'account', 'refresh.failed', errorData(error));
      throw error;
    }
  };

  return {
    async signIn(givenOperationId) {
      const id = operationId(givenOperationId, 'access-sign-in');
      emit(id, 'access', 'signIn.started');
      try {
        await dependencies.identity.beginSignIn();
        emit(id, 'access', 'signIn.identityReady');
        const response = await refreshAccount(id);
        emit(id, 'access', 'signIn.succeeded', { userId: response.account.user.id });
        return response;
      } catch (error) {
        emit(id, 'access', 'signIn.failed', errorData(error));
        throw error;
      }
    },

    async signOut(givenOperationId) {
      const id = operationId(givenOperationId, 'access-sign-out');
      emit(id, 'access', 'signOut.started');
      try {
        await dependencies.identity.signOut();
        emit(id, 'access', 'signOut.succeeded');
      } catch (error) {
        emit(id, 'access', 'signOut.failed', errorData(error));
        throw error;
      }
    },

    refreshAccount,

    async listLedgers(limit = 50, givenOperationId) {
      const id = operationId(givenOperationId, 'account-ledgers');
      const boundedLimit = Math.max(1, Math.min(200, Math.floor(limit)));
      emit(id, 'account', 'ledgers.started', { limit: boundedLimit });
      try {
        const [usage, billing] = await Promise.all([
          dependencies.backend.listUsageLedger(boundedLimit),
          dependencies.backend.listBillingLedger(boundedLimit),
        ]);
        emit(id, 'account', 'ledgers.succeeded', {
          usageEntries: usage.entries.length,
          billingEntries: billing.entries.length,
        });
        return { usage, billing };
      } catch (error) {
        emit(id, 'account', 'ledgers.failed', errorData(error));
        throw error;
      }
    },

    async startStripeCheckout(packId, givenOperationId) {
      const id = operationId(givenOperationId, 'billing-checkout');
      const normalizedPackId = packId.trim();
      if (!normalizedPackId) throw new Error('A managed credit pack ID is required.');
      emit(id, 'billing', 'checkout.started', { packId: normalizedPackId });
      try {
        const checkout = await dependencies.backend.createStripeCheckoutSession(normalizedPackId);
        emit(id, 'billing', 'checkout.sessionCreated', {
          packId: normalizedPackId,
          sessionId: checkout.sessionId,
        });
        await dependencies.navigation.navigate(checkout.url);
        emit(id, 'billing', 'checkout.navigationRequested', { sessionId: checkout.sessionId });
        return checkout;
      } catch (error) {
        emit(id, 'billing', 'checkout.failed', errorData(error));
        throw error;
      }
    },

    startStripeReturnPolling(options) {
      const id = operationId(options?.operationId, 'billing-reconcile');
      const attempts = Math.max(1, Math.floor(options?.attempts ?? 5));
      const intervalMs = Math.max(1, Math.floor(options?.intervalMs ?? 2000));
      let attempt = 0;
      let settled = false;
      let intervalHandle: unknown;
      let resolveCompletion: (value: ManagedAccountSummaryResponse | null) => void = () => undefined;
      let lastSuccessfulResult: ManagedAccountSummaryResponse | null = null;
      const completion = new Promise<ManagedAccountSummaryResponse | null>(resolve => {
        resolveCompletion = resolve;
      });

      const finish = () => {
        if (settled) return;
        settled = true;
        runtime.clock.clearInterval(intervalHandle);
        emit(id, 'billing', 'reconcile.completed', {
          attempts: attempt,
          availableCredits: lastSuccessfulResult?.account.billingSummary.availableCredits,
        });
        resolveCompletion(lastSuccessfulResult);
      };

      emit(id, 'billing', 'reconcile.started', { attempts, intervalMs });
      intervalHandle = runtime.clock.setInterval(() => {
        if (settled) return;
        attempt += 1;
        const currentAttempt = attempt;
        emit(id, 'billing', 'reconcile.attempted', { attempt: currentAttempt });
        void (options?.refresh || dependencies.backend.getAccountSummary)()
          .then(result => {
            if (result) lastSuccessfulResult = result;
            emit(id, 'billing', 'reconcile.refreshSucceeded', {
              attempt: currentAttempt,
              availableCredits: result?.account.billingSummary.availableCredits,
            });
            options?.onAttempt?.(currentAttempt, result, null);
            if (result && options?.isComplete?.(result)) finish();
          })
          .catch(error => {
            emit(id, 'billing', 'reconcile.refreshFailed', { attempt: currentAttempt, ...errorData(error) });
            options?.onAttempt?.(currentAttempt, null, error);
          })
          .finally(() => {
            if (!settled && currentAttempt >= attempts) finish();
          });
      }, intervalMs);

      return {
        operationId: id,
        completion,
        cancel: finish,
      };
    },

    async submitAiContentReport(payload, givenOperationId) {
      const id = operationId(givenOperationId, 'report-content');
      emit(id, 'report', 'submit.started', {
        accessMode: payload.accessMode,
        messageId: payload.messageId,
        reason: payload.reason,
      });
      try {
        const result = await dependencies.backend.submitAiContentReport(payload);
        emit(id, 'report', 'submit.succeeded', { reportId: result.reportId });
        return result;
      } catch (error) {
        emit(id, 'report', 'submit.failed', errorData(error));
        throw error;
      }
    },

    async deleteAccount(options) {
      const id = operationId(options.operationId, 'account-delete');
      if (options.confirmation.trim().toUpperCase() !== 'DELETE') {
        throw new Error('Managed account deletion requires the confirmation DELETE.');
      }
      if (options.expectedUserId && options.expectedUserId !== options.actualUserId) {
        throw new Error('Refusing to delete an account that does not match the disposable test user.');
      }
      emit(id, 'account', 'delete.started', { userId: options.actualUserId });
      try {
        const result = await dependencies.backend.deleteManagedAccount();
        emit(id, 'account', 'delete.backendSucceeded', {
          deletedAt: result.deletedAt,
          deletedManagedFileCount: result.deletedManagedFileCount,
          deletedLiveGatewayTicketCount: result.deletedLiveGatewayTicketCount,
          deletedLiveGatewaySessionCount: result.deletedLiveGatewaySessionCount,
          anonymizedPurchaseCount: result.anonymizedPurchaseCount,
        });
        await dependencies.identity.signOut();
        emit(id, 'account', 'delete.succeeded');
        return result;
      } catch (error) {
        emit(id, 'account', 'delete.failed', errorData(error));
        throw error;
      }
    },
  };
};
