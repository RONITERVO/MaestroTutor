// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface HttpError extends Error {
  status: number;
}

export const createHttpError = (status: number, message: string): HttpError => {
  const error = new Error(message) as HttpError;
  error.status = status;
  return error;
};

export const getHttpStatus = (error: unknown, fallback = 500): number => (
  typeof error === 'object' && error !== null && typeof (error as { status?: unknown }).status === 'number'
    ? (error as { status: number }).status
    : fallback
);

export const getErrorMessage = (error: unknown, fallback = 'Unexpected error.'): string => (
  error instanceof Error && error.message ? error.message : fallback
);
