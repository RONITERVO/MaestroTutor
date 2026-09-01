// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';

/**
 * Stable, non-reversible Firebase-account binding placed on a Play purchase.
 * Google limits obfuscated account ids to 64 characters; a SHA-256 hex digest
 * fits exactly and avoids sending the Firebase UID to the storefront.
 */
export const makePlayAccountBinding = (uid: string): string => (
  createHash('sha256').update(uid).digest('hex')
);

export const playPurchaseBelongsToAccount = (
  obfuscatedExternalAccountId: unknown,
  uid: string,
): boolean => (
  typeof obfuscatedExternalAccountId === 'string'
  && obfuscatedExternalAccountId === makePlayAccountBinding(uid)
);
