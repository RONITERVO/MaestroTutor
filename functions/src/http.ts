// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface HttpError extends Error {
  status: number;
  code?: string;
}

export const createHttpError = (status: number, message: string, code?: string): HttpError => {
  const error = new Error(message) as HttpError;
  error.status = status;
  if (code) error.code = code;
  return error;
};

export const getHttpStatus = (error: unknown, fallback = 500): number => (
  typeof error === 'object' && error !== null && typeof (error as { status?: unknown }).status === 'number'
    ? (error as { status: number }).status
    : fallback
);

/**
 * Only errors this module created carry a client-facing code. Runtime and
 * library errors also have a `code` (`ENOENT`, `ECONNRESET`, Firestore's
 * numeric codes), and echoing those back would describe the backend's internals
 * to callers instead of naming a condition a client can act on. A numeric
 * `status` is the marker for "deliberately shaped for the client".
 */
export const getHttpErrorCode = (error: unknown): string | undefined => {
  if (typeof error !== 'object' || error === null) return undefined;
  const candidate = error as { status?: unknown; code?: unknown };
  if (typeof candidate.status !== 'number') return undefined;
  return typeof candidate.code === 'string' && candidate.code ? candidate.code : undefined;
};

export const getErrorMessage = (error: unknown, fallback = 'Unexpected error.'): string => (
  error instanceof Error && error.message ? error.message : fallback
);
