// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHttpError } from './http';

/** A non-refundable UTC-day admission allowance, in integer microdollars. */
export const admitManagedSpend = (used: unknown, requestedUsd: number, limitUsd: number): number => {
  const current = used === undefined ? 0 : Number(used);
  const requested = Math.ceil(requestedUsd * 1_000_000);
  const limit = Math.floor(limitUsd * 1_000_000);
  if (!Number.isSafeInteger(current) || current < 0
    || !Number.isSafeInteger(requested) || requested <= 0
    || !Number.isSafeInteger(limit) || limit <= 0) {
    throw createHttpError(503, 'Managed spending protection is unavailable. Please try again later.');
  }
  if (current + requested > limit) {
    throw createHttpError(503, 'Maestro has reached its daily managed usage allowance. Please try again after 00:00 UTC.');
  }
  return current + requested;
};
