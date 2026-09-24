// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Provider file identity and missing-file compatibility rules shared by file owners. */

export const normalizeGeminiFileName = (nameOrUri: string): string | null => {
  const trimmed = (nameOrUri || '').trim();
  if (!trimmed) return null;
  if (trimmed.startsWith('files/')) return trimmed;
  const uriMatch = /\/files\/([^?\s]+)/.exec(trimmed);
  if (uriMatch?.[1]) {
    return `files/${uriMatch[1]}`;
  }
  return null;
};

export const isNotFoundError = (error: unknown): boolean => {
  const status = Number((error as { status?: unknown })?.status);
  const message = error instanceof Error ? error.message.toLowerCase() : '';
  // Permission/billing failures do not prove that the owned remote file is gone.
  return status === 404 || (status !== 403 && message.includes('not found'));
};

// Gemini Files expire after 48 hours. Persist the provider expiry when supplied;
// creation time provides the documented upper bound for legacy owned records.
const FILE_RETENTION_MS = 48 * 60 * 60 * 1000;
export const managedFileExpiresAt = (file: { expirationTime?: unknown; createdAt?: unknown }): number => {
  const reported = typeof file.expirationTime === 'string' ? Date.parse(file.expirationTime) : NaN;
  if (Number.isFinite(reported)) return reported;
  const created = Number(file.createdAt);
  return Number.isFinite(created) && created > 0 ? created + FILE_RETENTION_MS : Infinity;
};
export const hasManagedFileExpired = (file: { expirationTime?: unknown; createdAt?: unknown }): boolean => (
  Date.now() >= managedFileExpiresAt(file)
);
