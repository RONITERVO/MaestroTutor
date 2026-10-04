// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHttpError } from './http';

/** Keep Quest's unapproved commerce route closed, independently of UI visibility. */
export function assertStripeCheckoutAllowed(
  origin: string | undefined, verifiedAppId: string | null | undefined, questAppId: string,
): void {
  // Origin can deny but never authorize: callers can omit or forge it. Only the
  // server-verified app identity distinguishes Quest when Origin is absent.
  if (origin === 'https://appassets.androidplatform.net'
    || (questAppId && verifiedAppId === questAppId)) {
    throw createHttpError(403, 'Credit purchases are not available in the Quest app.', 'billing/checkout-unavailable');
  }
  // An App Check incident rollback must not also turn off the Quest purchase
  // boundary. Other managed services retain their existing rollback behavior.
  if (questAppId && !verifiedAppId) {
    throw createHttpError(503, 'Credit purchases require app verification.', 'billing/checkout-unavailable');
  }
}
