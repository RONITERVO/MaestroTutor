// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Turns a managed-access failure into something a user can act on.
 *
 * Attestation is the one failure a user regularly meets and can do nothing
 * about from the message alone: the backend rejects the request with
 * "Missing Firebase App Check token." — accurate for a log, meaningless on a
 * phone. The codes below name the condition, so the app can say which build
 * and which store actually work instead of quoting server internals.
 */
import { ServiceHttpError } from '../../services/shared/serviceErrors';
import type { TranslationFunction } from '../../app/hooks/useTranslations';

const APP_CHECK_CODES = new Set([
  // Raised by the client when the device cannot attest at all.
  'app-check/unavailable',
  // Raised by the backend for a request with no, or a rejected, token.
  'app-check/missing',
  'app-check/invalid',
]);

/*
 * Backends deployed before the codes existed answer with prose only. Matching
 * it keeps older deployments explainable; the codes are what new ones use.
 */
const LEGACY_APP_CHECK_MESSAGES = new Set([
  'Missing Firebase App Check token.',
  'Invalid Firebase App Check token.',
]);

export const isAppCheckFailure = (error: unknown): boolean => {
  const code = error instanceof ServiceHttpError ? error.code : undefined;
  if (code && APP_CHECK_CODES.has(code)) return true;
  return error instanceof Error && LEGACY_APP_CHECK_MESSAGES.has(error.message);
};

export const describeManagedAccessError = (
  error: unknown,
  t: TranslationFunction,
  fallbackKey: string,
): string => {
  if (isAppCheckFailure(error)) return t('managedAccess.appCheckFailed');
  return (error instanceof Error && error.message) || t(fallbackKey);
};
