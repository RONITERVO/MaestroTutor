// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { maestroBackendService } from '../backend/maestroBackendService';

export const maestroAccountService = {
  getManagedAccountSummary: () => maestroBackendService.getAccountSummary(),
  listManagedUsageLedger: (limit?: number) => maestroBackendService.listUsageLedger(limit),
  listManagedBillingLedger: (limit?: number) => maestroBackendService.listBillingLedger(limit),
} as const;
