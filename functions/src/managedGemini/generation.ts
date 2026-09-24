// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Managed generation admission, provider streaming and authoritative usage settlement. */

import type { Response } from 'express';
import type { AppUser } from '../auth';
import { appConfig } from '../config';
import {
  buildManagedPromptTokenCountInputs,
  prepareManagedGenerationConfig,
  requireAllowedManagedModel,
  requirePricedManagedGenerationModel,
  resolveManagedContentOperation,
  resolvePinnedManagedGenerationModel,
  usesManagedGoogleSearch
} from '../geminiPolicy';
import { createHttpError, getErrorMessage, getHttpErrorCode } from '../http';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  sweepExpiredReservationsForUser,
} from '../managedBilling';
import {
  estimateReservationUsd,
  usageMetadataToUsd,
  usdToCredits
} from '../pricing';
import { getGeminiClient } from './client';
import { settleCompletedManagedOperation } from './settlement';
import { requireOwnedManagedContentFiles } from './fileLifecycle';

const STREAM_CONTENT_TYPE = 'application/x-ndjson; charset=utf-8';

const countPromptTokens = async (
  model: string,
  contents: unknown,
  config?: Record<string, unknown>
): Promise<number> => {
  try {
    let totalTokens = 0;
    for (const countableInput of buildManagedPromptTokenCountInputs(contents, config)) {
      const result = await getGeminiClient().models.countTokens({
        model,
        contents: countableInput,
      } as any);
      const tokenCount = Number((result as any)?.totalTokens ?? (result as any)?.tokenCount);
      if (!Number.isFinite(tokenCount) || tokenCount < 0) {
        throw new Error('Gemini countTokens returned no usable token count.');
      }
      totalTokens += Math.floor(tokenCount);
    }
    return totalTokens;
  } catch (error) {
    console.error('[billing] Prompt token count failed; generation was not started.', error);
    throw createHttpError(502, 'The backend could not price this prompt before generation.');
  }
};

/**
 * How many images a response actually produced.
 *
 * Images are billed per image, not per output token, so this number is the
 * difference between charging cents and charging a rounding error. Counted from
 * the response rather than assumed from the operation, so a request that asked
 * for an image and got none is not billed for one.
 */
const countGeneratedImages = (response: unknown): number => {
  const candidates = (response as { candidates?: unknown })?.candidates;
  if (!Array.isArray(candidates)) return 0;
  let images = 0;
  for (const candidate of candidates) {
    const parts = (candidate as { content?: { parts?: unknown } })?.content?.parts;
    if (!Array.isArray(parts)) continue;
    for (const part of parts) {
      const inline = (part as { inlineData?: { mimeType?: string } })?.inlineData;
      if (inline?.mimeType && inline.mimeType.startsWith('image/')) images += 1;
    }
  }
  return images;
};

const countGoogleSearchQueries = (response: unknown): number => {
  const candidates = (response as { candidates?: unknown })?.candidates;
  if (!Array.isArray(candidates)) return 0;
  return candidates.reduce((total, candidate) => {
    const queries = (candidate as {
      groundingMetadata?: { webSearchQueries?: unknown };
    })?.groundingMetadata?.webSearchQueries;
    return total + (Array.isArray(queries) ? queries.length : 0);
  }, 0);
};

const serializeGenerateContentResponse = (
  response: any,
  billingSummary: unknown,
  modelVersionOverride?: string,
) => ({
  text: typeof response?.text === 'string' ? response.text : '',
  candidates: Array.isArray(response?.candidates) ? response.candidates : [],
  usageMetadata: response?.usageMetadata || undefined,
  modelVersion: modelVersionOverride
    || (typeof response?.modelVersion === 'string' ? response.modelVersion : undefined),
  promptFeedback: response?.promptFeedback || undefined,
  responseId: typeof response?.responseId === 'string' ? response.responseId : undefined,
  billingSummary,
});

const serializeGenerateContentChunk = (chunk: any): Record<string, unknown> => {
  const serialized = JSON.parse(JSON.stringify(chunk || {})) as Record<string, unknown>;
  if (typeof chunk?.text === 'string') {
    serialized.text = chunk.text;
  }
  return serialized;
};

interface ManagedGenerationAdmission {
  uid: string;
  user: AppUser;
  operation: string;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
}

/** One pricing/reservation policy for direct and streaming generation. */
const reserveGenerationCredits = async (params: ManagedGenerationAdmission) => {
  await sweepExpiredReservationsForUser(params.uid);

  const promptTokens = await countPromptTokens(params.model, params.contents, params.config);
  const reservedSearchQueries = usesManagedGoogleSearch(params.config)
    ? appConfig.managedSearchReservationQueries
    : 0;
  const estimatedUsd = estimateReservationUsd({
    model: params.model,
    promptTokens,
    operation: params.operation,
    searchQueries: reservedSearchQueries,
    expectedOutputTokens: Number(params.config?.maxOutputTokens),
  });
  const estimatedCredits = usdToCredits(estimatedUsd);

  const reservation = await reserveManagedCredits({
    uid: params.uid,
    user: params.user,
    operation: params.operation,
    model: params.model,
    estimatedCredits,
    estimatedUsd,
    metadata: {
      promptTokens,
      reservedSearchQueries,
      outputReservation: 'published-model-ceiling',
      maxOutputTokens: params.config?.maxOutputTokens,
    },
  });

  return reservation;
};

const withManagedReservation = async <T>(params: ManagedGenerationAdmission & {
  execute: (reservationId: string) => Promise<T>;
  finalize: (reservationId: string, result: T) => Promise<{ result: T; billingSummary: unknown }>;
}): Promise<{ result: T; billingSummary: unknown }> => {
  const reservation = await reserveGenerationCredits(params);
  let result: T;
  try {
    result = await params.execute(reservation.reservationId);
  } catch (error) {
    try { await releaseManagedReservation(params.uid, reservation.reservationId, 'request-failed'); }
    catch { /* Preserve the provider failure; expiry can release an uncompleted request. */ }
    throw error;
  }
  // Provider completion and accounting failure are different outcomes. The
  // finalizer persists actual usage; never refund this as a provider failure.
  return params.finalize(reservation.reservationId, result);
};

export const generateManagedContent = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
}) => {
  const model = requirePricedManagedGenerationModel(
    requireAllowedManagedModel(
      resolvePinnedManagedGenerationModel(params.model),
      appConfig.managedAllowedGeminiModels,
      'generation',
    ),
  );
  const config = prepareManagedGenerationConfig(params.config, model);
  const operation = resolveManagedContentOperation(config, false, model);
  await requireOwnedManagedContentFiles(params.uid, params.contents, config);

  const finalized = await withManagedReservation({
    ...params,
    model,
    operation,
    config,
    execute: async () => (
      getGeminiClient().models.generateContent({
        model,
        contents: params.contents,
        ...(config ? { config } : {}),
      } as any)
    ),
    finalize: async (reservationId, response) => {
      const usageMetadata = response?.usageMetadata as Record<string, unknown> | undefined;
      const resolvedModelVersion = typeof response?.modelVersion === 'string'
        ? response.modelVersion.trim() || undefined
        : undefined;
      const billedUsd = usageMetadataToUsd(
        model,
        usageMetadata,
        operation,
        countGeneratedImages(response),
        countGoogleSearchQueries(response),
        resolvedModelVersion,
      );
      const billedCredits = usdToCredits(billedUsd);
      const billingSummary = await settleCompletedManagedOperation({
        uid: params.uid,
        reservationId,
        billedCredits,
        billedUsd,
        operation,
        model: resolvedModelVersion || model,
        metadata: {
          requestedModel: model,
          resolvedModelVersion: resolvedModelVersion || null,
          promptTokenCount: usageMetadata?.promptTokenCount,
          candidatesTokenCount: usageMetadata?.candidatesTokenCount,
          searchQueries: countGoogleSearchQueries(response),
        },
      });
      return { result: response, billingSummary };
    },
  });

  return serializeGenerateContentResponse(finalized.result, finalized.billingSummary);
};

export const streamManagedContent = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
  response: Response;
}) => {
  const response = params.response;
  const model = requirePricedManagedGenerationModel(
    requireAllowedManagedModel(
      resolvePinnedManagedGenerationModel(params.model),
      appConfig.managedAllowedGeminiModels,
      'streaming generation',
    ),
  );
  const config = prepareManagedGenerationConfig(params.config, model);
  const operation = resolveManagedContentOperation(config, true, model);
  await requireOwnedManagedContentFiles(params.uid, params.contents, config);
  const reservation = await reserveGenerationCredits({ ...params, model, operation, config });

  response.setHeader('Content-Type', STREAM_CONTENT_TYPE);
  response.setHeader('Cache-Control', 'no-store, no-transform');
  response.setHeader('X-Accel-Buffering', 'no');

  let latestChunk: any = null;
  let resolvedModelVersion: string | undefined;
  let deliveredAnyChunk = false;
  // Images arrive spread across chunks, so they are tallied as they stream
  // rather than read off the final one.
  let streamedImageCount = 0;
  let streamedSearchQueryCount = 0;
  let clientDisconnected = false;
  let streamFinished = false;
  let providerCompleted = false;

  const markDisconnected = () => {
    if (!streamFinished) {
      clientDisconnected = true;
    }
  };

  response.once('close', markDisconnected);
  response.once('error', markDisconnected);

  try {
    const stream = await getGeminiClient().models.generateContentStream({
      model,
      contents: params.contents,
      ...(config ? { config } : {}),
    } as any);

    for await (const chunk of stream) {
      latestChunk = chunk;
      if (typeof chunk?.modelVersion === 'string' && chunk.modelVersion.trim()) {
        resolvedModelVersion = chunk.modelVersion.trim();
      }
      streamedImageCount += countGeneratedImages(chunk);
      streamedSearchQueryCount = Math.max(
        streamedSearchQueryCount,
        countGoogleSearchQueries(chunk),
      );
      if (clientDisconnected || response.destroyed || !response.writable) {
        clientDisconnected = true;
        // Keep consuming the provider stream to obtain final usage metadata.
        // Otherwise a client can avoid exact settlement by disconnecting, and
        // honest network drops get charged the worst-case reservation.
        continue;
      }

      response.write(`${JSON.stringify({
        type: 'chunk',
        chunk: serializeGenerateContentChunk(chunk),
      })}\n`);
      deliveredAnyChunk = true;
    }

    providerCompleted = true;
    const usageMetadata = latestChunk?.usageMetadata as Record<string, unknown> | undefined;
    const billedUsd = usageMetadataToUsd(
      model,
      usageMetadata,
      operation,
      streamedImageCount,
      streamedSearchQueryCount,
      resolvedModelVersion,
    );
    const billedCredits = usdToCredits(billedUsd);
    const billingSummary = await settleCompletedManagedOperation({
      uid: params.uid,
      reservationId: reservation.reservationId,
      billedCredits,
      billedUsd,
      operation,
      model: resolvedModelVersion || model,
      metadata: {
        requestedModel: model,
        resolvedModelVersion: resolvedModelVersion || null,
        promptTokenCount: usageMetadata?.promptTokenCount,
        candidatesTokenCount: usageMetadata?.candidatesTokenCount,
        disconnectRecovered: clientDisconnected,
        searchQueries: streamedSearchQueryCount,
      },
    });

    if (!clientDisconnected && !response.destroyed && response.writable) {
      response.write(`${JSON.stringify({
        type: 'final',
        result: serializeGenerateContentResponse(
          latestChunk || {},
          billingSummary,
          resolvedModelVersion,
        ),
      })}\n`);
      streamFinished = true;
      response.end();
    }
  } catch (error) {
    if (!providerCompleted) {
      try { await releaseManagedReservation(params.uid, reservation.reservationId, 'provider-stream-failed'); }
      catch (releaseError) { console.error('Managed stream reservation release failed:', releaseError); }
    }
    if (deliveredAnyChunk || clientDisconnected || response.headersSent) {
      if (!response.destroyed && response.writable && !response.writableEnded) {
        response.write(`${JSON.stringify({
          type: 'error',
          message: getErrorMessage(error),
          status: Number((error as { status?: unknown })?.status) || 500,
          code: getHttpErrorCode(error),
        })}\n`);
        streamFinished = true;
        response.end();
      }
      return;
    }
    throw error;
  } finally {
    streamFinished = true;
    response.off('close', markDisconnected);
    response.off('error', markDisconnected);
  }
};
