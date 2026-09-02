// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { ServiceHttpError } from '../../services/shared/serviceErrors';
import { describeManagedAccessError, isAppCheckFailure } from './managedAccessErrors';

const copy: Record<string, string> = {
  'managedAccess.appCheckFailed': 'Install from Google Play.',
  'managedAccess.appCheckMissing': 'Security token missing.',
  'managedAccess.appCheckInvalid': 'Security token rejected.',
};
const t = (key: string) => copy[key] || key;

describe('describeManagedAccessError', () => {
  it('uses the stable code even when the message is not a legacy match', () => {
    expect(describeManagedAccessError(
      new ServiceHttpError('Token rejected.', 401, 'app-check/unavailable'),
      t,
      'managedAccess.signInFailed',
    )).toBe('Install from Google Play.');
    expect(describeManagedAccessError(
      new ServiceHttpError('Token rejected.', 401, 'app-check/missing'),
      t,
      'managedAccess.signInFailed',
    )).toBe('Security token missing.');
    expect(describeManagedAccessError(
      new ServiceHttpError('Token rejected.', 401, 'app-check/invalid'),
      t,
      'managedAccess.signInFailed',
    )).toBe('Security token rejected.');
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
