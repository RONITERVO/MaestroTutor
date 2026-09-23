// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { recordPendingManagedSettlement, settleManagedReservation, type ManagedSettlement } from '../managedBilling';

/** A completed provider operation may retry accounting, never provider generation.
 * The ordinary expiry sweeper settles this durable usage if retries still fail. */
export const settleCompletedManagedOperation = async (params: ManagedSettlement) => {
  await recordPendingManagedSettlement(params);
  try {
    return await settleManagedReservation(params);
  } catch (error) {
    if (Number((error as { status?: unknown })?.status) === 409) throw error;
    await new Promise(resolve => setTimeout(resolve, 100));
    return settleManagedReservation(params);
  }
};
