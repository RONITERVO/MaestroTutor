// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  HEADLESS_METHOD_ACCESS_POLICY,
  HEADLESS_METHODS,
  isHeadlessMethodAvailable,
} from './accessPolicy';

describe('headless access policy', () => {
  it('classifies every advertised method and never makes a provider parity method mode-specific', () => {
    expect(Object.keys(HEADLESS_METHOD_ACCESS_POLICY)).toEqual(HEADLESS_METHODS);
    for (const method of HEADLESS_METHODS) {
      const policy = HEADLESS_METHOD_ACCESS_POLICY[method];
      expect(policy.releaseProof).toBeTruthy();
      if (policy.parityClass === 'provider-parity') {
        expect(isHeadlessMethodAvailable(method, 'managed')).toBe(true);
        expect(isHeadlessMethodAvailable(method, 'byok')).toBe(true);
      }
      if (policy.parityClass === 'managed-account-only') {
        expect(policy.accessModes).toEqual(['managed']);
      }
    }
  });

  it('keeps every cost-bearing shared provider operation in paired release proof', () => {
    const unpaired = HEADLESS_METHODS.filter(method => {
      const policy = HEADLESS_METHOD_ACCESS_POLICY[method];
      return policy.parityClass === 'provider-parity'
        && policy.costClass !== 'none'
        && !['paired-first-lesson', 'paired-raw'].includes(policy.releaseProof);
    });
    expect(unpaired).toEqual([]);
  });

  it('does not expose raw managed bearer-token or gateway-ticket plumbing', () => {
    expect(HEADLESS_METHODS).not.toContain('live.token.create');
    expect(HEADLESS_METHODS).not.toContain('live.token.release');
    expect(HEADLESS_METHODS).not.toContain('live.gateway.ticket.create');
  });
});
