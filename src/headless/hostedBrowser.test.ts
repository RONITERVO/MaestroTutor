// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { completeStripeTestCheckout, verifyHostedGoogleSignIn } from './hostedBrowser';

const base = {
  profileDirectory: '.',
  email: 'ci@example.test',
};

describe('hosted Stripe adapter safety', () => {
  it('rejects non-Stripe destinations before launching a browser', async () => {
    await expect(completeStripeTestCheckout({
      ...base,
      checkoutUrl: 'https://attacker.example/cs_test_123',
      sessionId: 'cs_test_123',
    })).rejects.toThrow('outside checkout.stripe.com');
  });

  it('rejects a session that cannot be proven to be Stripe test mode', async () => {
    await expect(completeStripeTestCheckout({
      ...base,
      checkoutUrl: 'https://checkout.stripe.com/c/pay/cs_live_123',
      sessionId: 'cs_live_123',
    })).rejects.toThrow('provably in test mode');
  });

  it('rejects Google verification outside the dedicated staging app', async () => {
    await expect(verifyHostedGoogleSignIn({
      appUrl: 'https://attacker.example',
      profileDirectory: '.',
    })).rejects.toThrow('dedicated staging app');
  });
});
