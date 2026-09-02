// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { ServiceHttpError } from '../../services/shared/serviceErrors';
import { describeManagedAccessError, isAppCheckFailure } from './managedAccessErrors';

const t = (key: string) => (key === 'managedAccess.appCheckFailed' ? 'Install from Google Play.' : key);

describe('describeManagedAccessError', () => {
  it('explains every attestation code the same way', () => {
    for (const code of ['app-check/unavailable', 'app-check/missing', 'app-check/invalid']) {
      const error = new ServiceHttpError('Missing Firebase App Check token.', 401, code);
      expect(describeManagedAccessError(error, t, 'managedAccess.signInFailed')).toBe('Install from Google Play.');
    }
  });

  it('recognises a backend deployed before the codes existed', () => {
    expect(isAppCheckFailure(new Error('Invalid Firebase App Check token.'))).toBe(true);
  });

  it('leaves an unrelated failure to speak for itself', () => {
    const error = new ServiceHttpError('Managed account has no credits.', 402, 'billing/insufficient-credits');
    expect(describeManagedAccessError(error, t, 'managedAccess.signInFailed'))
      .toBe('Managed account has no credits.');
  });

  it('falls back to the caller key when there is no message', () => {
    expect(describeManagedAccessError({}, t, 'managedAccess.signInFailed')).toBe('managedAccess.signInFailed');
  });
});
