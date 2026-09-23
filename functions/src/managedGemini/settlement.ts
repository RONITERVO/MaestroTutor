// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { recordPendingManagedSettlement, settleManagedReservation, type ManagedSettlement } from '../managedBilling';

/** A completed provider operation may retry accounting, never provider generation.
 * The ordinary expiry sweeper settles this durable usage if retries still fail. */
const retryAccountingOnce = async <T>(operation: () => Promise<T>): Promise<T> => {
  try {
    return await operation();
  } catch (error) {
    if (Number((error as { status?: unknown })?.status) === 409) throw error;
    await new Promise(resolve => setTimeout(resolve, 100));
    return operation();
  }
};

export const settleCompletedManagedOperation = async (params: ManagedSettlement) => {
  await retryAccountingOnce(() => recordPendingManagedSettlement(params));
  return retryAccountingOnce(() => settleManagedReservation(params));
};
