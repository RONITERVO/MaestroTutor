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
  return status === 403 || status === 404 || message.includes('not found') || message.includes('forbidden');
};
