// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';

/** A provider-scoped idempotency id that never exposes the store credential. */
export const makePurchaseClaimId = (platform: string, externalId: string): string => (
  `${platform}_${createHash('sha256').update(externalId).digest('hex')}`
);
