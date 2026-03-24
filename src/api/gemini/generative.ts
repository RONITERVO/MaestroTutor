// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getGeminiModels } from '../../core/config/models';
import { collapseGeminiContents } from '../../shared/utils/conversationTurns';
import { ApiError, getAi } from './client';

const DEFAULT_TIMEOUT_MS = 600_000; // 10 minutes
const HIGH_DEMAND_MAX_RETRIES = 10;
const HIGH_DEMAND_RETRY_BASE_DELAY_MS = 1_000;
const HIGH_DEMAND_RETRY_MAX_DELAY_MS = 8_000;
const PROCESSING_PROGRESS_INTERVAL_MS = 4_000;
const NO_MODEL_OUTPUT_FALLBACK_AFTER_MS = 24_000;

export type GeminiRetryReason = 'server-high-demand' | 'no-output-timeout';

export type GeminiProgressPhase =
  | 'attempt-start'
  | 'attempt-processing'
  | 'high-demand'
  | 'fallback-switch'
  | 'retry-scheduled'
  | 'success';

export interface GeminiProgressEvent {
  phase: GeminiProgressPhase;
  operation: string;
  model: string;
  attempt: number;
  totalAttempts: number;
  retryInMs?: number;
  elapsedMs?: number;
  reason?: GeminiRetryReason;
}

export interface GeminiRequestLifecycleHooks {
  onProgress?: (event: GeminiProgressEvent) => void;
  onTextDelta?: (deltaText: string, fullText: string) => void;
  onThoughtDelta?: (deltaThought: string, fullThought: string) => void;
}

export interface GenerateGeminiResponseOptions {
  systemInstruction?: string;
  currentFileParts?: Array<{ fileUri: string; mimeType: string }>;
  useGoogleSearch?: boolean;
  configOverrides?: any;
  timeoutMs?: number;
  lifecycleHooks?: GeminiRequestLifecycleHooks;
}

const withTimeout = <T>(promise: Promise<T>, ms: number): Promise<T> => {
  let timeoutId: ReturnType<typeof setTimeout>;
  const timeout = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(() => reject(new Error(`Request timed out after ${ms / 1000}s`)), ms);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timeoutId));
};

const sleep = (ms: number): Promise<void> => new Promise(resolve => setTimeout(resolve, ms));

const appendChunkWithPrefixDiff = (
  accumulated: string,
  previousChunk: string,
  currentChunk: string
): { delta: string; nextAccumulated: string; nextPreviousChunk: string } => {
  if (!currentChunk) {
    return { delta: '', nextAccumulated: accumulated, nextPreviousChunk: previousChunk };
  }

  let delta = currentChunk;
  if (previousChunk && currentChunk.startsWith(previousChunk)) {
    delta = currentChunk.slice(previousChunk.length);
  } else if (accumulated && accumulated.endsWith(currentChunk)) {
    delta = '';
  }

  return {
    delta,
    nextAccumulated: delta ? `${accumulated}${delta}` : accumulated,
    nextPreviousChunk: currentChunk,
  };
};

const extractThoughtText = (response: any): string => {
  const parts = response?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return '';
  return parts
    .filter((part: any) => part?.thought === true && typeof part?.text === 'string' && part.text.length > 0)
    .map((part: any) => part.text)
    .join('');
};

const tryParseJson = (value: string): any | undefined => {
  try {
    return JSON.parse(value);
  } catch {
    return undefined;
  }
};

const extractErrorPayload = (message?: string): any | undefined => {
  if (typeof message !== 'string') return undefined;
  const trimmed = message.trim();
  if (!trimmed) return undefined;

  const direct = tryParseJson(trimmed);
  if (direct) return direct;

  const firstBrace = trimmed.indexOf('{');
  const lastBrace = trimmed.lastIndexOf('}');
  if (firstBrace === -1 || lastBrace <= firstBrace) return undefined;

  return tryParseJson(trimmed.slice(firstBrace, lastBrace + 1));
};

const getNestedErrorMessage = (error: any): string | undefined => {
  const payload = extractErrorPayload(error?.message);
  const nestedMessage = payload?.error?.message;
  if (typeof nestedMessage === 'string' && nestedMessage.trim()) {
    return nestedMessage.trim();
  }
  return undefined;
};

const getErrorStatus = (error: any): number | undefined => {
  const direct = Number(error?.status);
  if (Number.isFinite(direct)) return direct;

  const payload = extractErrorPayload(error?.message);
  const payloadCode = payload?.error?.code ?? payload?.code;
  const parsed = Number(payloadCode);
  if (Number.isFinite(parsed)) return parsed;

  return undefined;
};

const getErrorCode = (error: any): string | undefined => {
  if (typeof error?.code === 'string' && error.code.trim()) {
    return error.code.trim();
  }

  const payload = extractErrorPayload(error?.message);
  const payloadStatus = payload?.error?.status ?? payload?.status;
  if (typeof payloadStatus === 'string' && payloadStatus.trim()) {
    return payloadStatus.trim();
  }

  if (typeof payload?.error?.code === 'number') {
    return String(payload.error.code);
  }

  return undefined;
};

const isHighDemandError = (error: any): boolean => {
  if (getErrorStatus(error) === 503) return true;

  const code = (getErrorCode(error) || '').toLowerCase();
  if (code === 'unavailable') return true;

  const message = (getNestedErrorMessage(error) || error?.message || '').toLowerCase();
  return (
    message.includes('high demand') ||
    message.includes('spikes in demand are usually temporary') ||
    message.includes('"status":"unavailable"')
  );
};

const normalizeModelName = (model: string): string => model.trim();

const resolveFallbackTextModel = (primaryModel: string): string | undefined => {
  const normalizedPrimary = normalizeModelName(primaryModel);
  if (!normalizedPrimary) return undefined;
  const fallback = normalizeModelName(getGeminiModels().text.fallback);
  if (!fallback || fallback === normalizedPrimary) return undefined;
  return fallback;
};

const createNoOutputHighDemandError = (model: string, elapsedMs: number): Error & {
  status: number;
  code: string;
  syntheticHighDemandReason: GeminiRetryReason;
} => {
  const error = new Error(
    `No model output after ${Math.max(1, Math.floor(elapsedMs / 1000))}s on ${model}; likely queued due to high demand.`
  ) as Error & {
    status: number;
    code: string;
    syntheticHighDemandReason: GeminiRetryReason;
  };
  error.status = 503;
  error.code = 'UNAVAILABLE';
  error.syntheticHighDemandReason = 'no-output-timeout';
  return error;
};

const buildRetryMeta = (attempt: number, retryInMs?: number) => ({
  attempt,
  totalAttempts: HIGH_DEMAND_MAX_RETRIES + 1,
  ...(typeof retryInMs === 'number' ? { retryInMs } : {}),
});

const withHighDemandRetry = async <T>(opts: {
  operation: string;
  model: string;
  fallbackModel?: string;
  requestPayload: any;
  run: (model: string) => Promise<T>;
  mapSuccess: (result: T) => any;
  onProgress?: (event: GeminiProgressEvent) => void;
}): Promise<T> => {
  let lastError: any;
  let activeModel = normalizeModelName(opts.model);
  const fallbackModel = opts.fallbackModel
    ? normalizeModelName(opts.fallbackModel)
    : undefined;
  const canUseFallback = Boolean(fallbackModel && fallbackModel !== activeModel);
  let hasSwitchedToFallback = false;

  for (let attempt = 0; attempt <= HIGH_DEMAND_MAX_RETRIES; attempt++) {
    const attemptNumber = attempt + 1;
    const totalAttempts = HIGH_DEMAND_MAX_RETRIES + 1;
    opts.onProgress?.({
      phase: 'attempt-start',
      operation: opts.operation,
      model: activeModel,
      attempt: attemptNumber,
      totalAttempts,
    });
    const attemptLog = debugLogService.logRequest(opts.operation, activeModel, opts.requestPayload);

    const attemptStartedAt = Date.now();
    opts.onProgress?.({
      phase: 'attempt-processing',
      operation: opts.operation,
      model: activeModel,
      attempt: attemptNumber,
      totalAttempts,
      elapsedMs: 0,
    });
    const processingTimer = setInterval(() => {
      opts.onProgress?.({
        phase: 'attempt-processing',
        operation: opts.operation,
        model: activeModel,
        attempt: attemptNumber,
        totalAttempts,
        elapsedMs: Date.now() - attemptStartedAt,
      });
    }, PROCESSING_PROGRESS_INTERVAL_MS);

    try {
      const result = await opts.run(activeModel);
      clearInterval(processingTimer);
      opts.onProgress?.({
        phase: 'success',
        operation: opts.operation,
        model: activeModel,
        attempt: attemptNumber,
        totalAttempts,
        elapsedMs: Date.now() - attemptStartedAt,
      });
      attemptLog.complete({ ...opts.mapSuccess(result), retry: buildRetryMeta(attemptNumber) });
      return result;
    } catch (error: any) {
      clearInterval(processingTimer);
      lastError = error;

      const canRetry = attempt < HIGH_DEMAND_MAX_RETRIES && isHighDemandError(error);
      const retryReason: GeminiRetryReason =
        error?.syntheticHighDemandReason === 'no-output-timeout'
          ? 'no-output-timeout'
          : 'server-high-demand';
      let retryInMs: number | undefined;
      let switchedToFallbackForRetry = false;
      if (canRetry) {
        opts.onProgress?.({
          phase: 'high-demand',
          operation: opts.operation,
          model: activeModel,
          attempt: attemptNumber,
          totalAttempts,
          elapsedMs: Date.now() - attemptStartedAt,
          reason: retryReason,
        });
        if (canUseFallback && !hasSwitchedToFallback) {
          hasSwitchedToFallback = true;
          switchedToFallbackForRetry = true;
          activeModel = fallbackModel!;
          retryInMs = 0;
          opts.onProgress?.({
            phase: 'fallback-switch',
            operation: opts.operation,
            model: activeModel,
            attempt: attemptNumber,
            totalAttempts,
            elapsedMs: Date.now() - attemptStartedAt,
            reason: retryReason,
          });
        } else {
          retryInMs = Math.min(
            HIGH_DEMAND_RETRY_MAX_DELAY_MS,
            HIGH_DEMAND_RETRY_BASE_DELAY_MS * (2 ** attempt)
          );
          opts.onProgress?.({
            phase: 'retry-scheduled',
            operation: opts.operation,
            model: activeModel,
            attempt: attemptNumber,
            totalAttempts,
            retryInMs,
            elapsedMs: Date.now() - attemptStartedAt,
            reason: retryReason,
          });
        }
      }
      attemptLog.error({
        status: getErrorStatus(error),
        code: getErrorCode(error),
        message: getNestedErrorMessage(error) || error?.message || 'Gemini API failed',
        retry: buildRetryMeta(attemptNumber, retryInMs),
        ...(switchedToFallbackForRetry ? { switchedToFallbackModel: activeModel } : {}),
      });

      if (!canRetry) {
        throw error;
      }

      if (switchedToFallbackForRetry) {
        console.warn(
          `[Gemini] ${opts.operation} hit high-demand/unavailable response ` +
          `(attempt ${attempt + 1}/${HIGH_DEMAND_MAX_RETRIES + 1}), retrying immediately with fallback model ${activeModel}.`
        );
      } else {
        console.warn(
          `[Gemini] ${opts.operation} hit high-demand/unavailable response ` +
          `(attempt ${attempt + 1}/${HIGH_DEMAND_MAX_RETRIES + 1}), retrying in ${retryInMs}ms with model ${activeModel}.`
        );
      }

      if (retryInMs && retryInMs > 0) {
        await sleep(retryInMs);
      }
    }
  }

  throw lastError;
};

export const generateGeminiResponse = async (
  modelName: string,
  userPrompt: string,
  history: any[],
  options: GenerateGeminiResponseOptions = {}
) => {
  const {
    systemInstruction,
    currentFileParts,
    useGoogleSearch,
    configOverrides,
    timeoutMs = DEFAULT_TIMEOUT_MS,
    lifecycleHooks,
  } = options;
  const ai = await getAi();
  const rawContents: any[] = [];

  const normalizeFileParts = (parts: unknown): Array<{ fileUri: string; mimeType: string }> => {
    if (!Array.isArray(parts)) return [];
    return parts
      .map((part) => {
        const candidate = part as { fileUri?: string; mimeType?: string } | null | undefined;
        const fileUri = typeof candidate?.fileUri === 'string' ? candidate.fileUri.trim() : '';
        const mimeType = typeof candidate?.mimeType === 'string' ? candidate.mimeType.trim() : '';
        if (!fileUri || !mimeType) return null;
        return { fileUri, mimeType };
      })
      .filter((part): part is { fileUri: string; mimeType: string } => Boolean(part));
  };

  // Build a lossless "raw" payload first, then collapse adjacent user/model
  // turns right before send. Keeping the pre-collapse form here makes it easier
  // to attach all text/files/avatar parts without accidentally dropping context.
  history.forEach(h => {
    const parts: any[] = [];
    const textContent = h.rawAssistantResponse || h.text;
    if (textContent) parts.push({ text: textContent });

    const historyFileParts = normalizeFileParts(h.fileParts);
    historyFileParts.forEach((part) => {
      parts.push({ fileData: { fileUri: part.fileUri, mimeType: part.mimeType } });
    });

    if (h.avatarFileUri) {
      parts.push({ fileData: { fileUri: h.avatarFileUri, mimeType: h.avatarMimeType || 'image/jpeg' } });
    }

    if (parts.length > 0) {
      const role = h.role === 'assistant' ? 'model' : 'user';
      rawContents.push({ role, parts });
    }
  });

  const currentParts: any[] = [{ text: userPrompt }];
  const normalizedCurrentFileParts = normalizeFileParts(currentFileParts);
  normalizedCurrentFileParts.forEach((part) => {
    currentParts.push({ fileData: { fileUri: part.fileUri, mimeType: part.mimeType } });
  });

  rawContents.push({ role: 'user', parts: currentParts });
  // Collapse only for this request. We do not mutate the source history because
  // the UI/persistence layer still needs the original message granularity.
  const contents = collapseGeminiContents(rawContents);

  const config: any = { ...configOverrides };
  const existingThinkingConfig =
    config?.thinkingConfig && typeof config.thinkingConfig === 'object'
      ? config.thinkingConfig
      : undefined;
  // Enable thought-part streaming by default so UI can surface thinking traces.
  if (existingThinkingConfig?.includeThoughts !== false) {
    config.thinkingConfig = {
      ...(existingThinkingConfig || {}),
      includeThoughts: true,
    };
  }
  if (systemInstruction) config.systemInstruction = systemInstruction;

  if (useGoogleSearch) {
    config.tools = [{ googleSearch: {} }];
  }

  // Redact inlineData from debug logs to prevent logging large base64 payloads
  const redactInlineData = (obj: any): any => {
    if (!obj || typeof obj !== 'object') return obj;
    if (Array.isArray(obj)) return obj.map(redactInlineData);
    const result: any = {};
    for (const key of Object.keys(obj)) {
      if (key === 'inlineData') {
        result[key] = '[REDACTED]';
      } else {
        result[key] = redactInlineData(obj[key]);
      }
    }
    return result;
  };
  const redactedContents = redactInlineData(contents);

  try {
    const fallbackModel = resolveFallbackTextModel(modelName);
    const result = await withHighDemandRetry(
      {
        operation: 'generateContent',
        model: modelName,
        fallbackModel,
        requestPayload: { contents: redactedContents, config },
        run: activeModel => withTimeout(
          (async () => {
            const abortController = new AbortController();
            let latestChunk: any | undefined;
            let accumulatedText = '';
            let previousChunkText = '';
            let accumulatedThought = '';
            let previousChunkThought = '';
            let hasVisibleModelOutput = false;
            const attemptStartedAt = Date.now();
            const clearNoOutputTimer = (() => {
              let timerId: ReturnType<typeof setTimeout> | null = setTimeout(() => {
                if (!hasVisibleModelOutput) {
                  abortController.abort();
                }
              }, NO_MODEL_OUTPUT_FALLBACK_AFTER_MS);
              return () => {
                if (timerId !== null) {
                  clearTimeout(timerId);
                  timerId = null;
                }
              };
            })();

            const markVisibleModelOutput = () => {
              if (hasVisibleModelOutput) return;
              hasVisibleModelOutput = true;
              clearNoOutputTimer();
            };

            try {
              const stream = await ai.models.generateContentStream({
                model: activeModel,
                contents,
                config: {
                  ...config,
                  abortSignal: abortController.signal,
                },
              });

              for await (const chunk of stream) {
                latestChunk = chunk;

                const chunkText = typeof chunk?.text === 'string' ? chunk.text : '';
                if (chunkText) {
                  markVisibleModelOutput();
                  const appended = appendChunkWithPrefixDiff(accumulatedText, previousChunkText, chunkText);
                  accumulatedText = appended.nextAccumulated;
                  previousChunkText = appended.nextPreviousChunk;
                  if (appended.delta) {
                    lifecycleHooks?.onTextDelta?.(appended.delta, accumulatedText);
                  }
                }

                const chunkThought = extractThoughtText(chunk);
                if (chunkThought) {
                  markVisibleModelOutput();
                  const appendedThought = appendChunkWithPrefixDiff(accumulatedThought, previousChunkThought, chunkThought);
                  accumulatedThought = appendedThought.nextAccumulated;
                  previousChunkThought = appendedThought.nextPreviousChunk;
                  if (appendedThought.delta) {
                    lifecycleHooks?.onThoughtDelta?.(appendedThought.delta, accumulatedThought);
                  }
                }
              }

              return {
                text: accumulatedText || latestChunk?.text || '',
                candidates: latestChunk?.candidates,
                usageMetadata: latestChunk?.usageMetadata,
              };
            } catch (error: any) {
              if (!hasVisibleModelOutput && abortController.signal.aborted) {
                throw createNoOutputHighDemandError(activeModel, Date.now() - attemptStartedAt);
              }
              throw error;
            } finally {
              clearNoOutputTimer();
            }
          })(),
          timeoutMs
        ),
        mapSuccess: result => ({ text: result.text, usage: result.usageMetadata }),
        onProgress: lifecycleHooks?.onProgress,
      }
    );
    return {
      text: result.text,
      candidates: result.candidates,
      usageMetadata: result.usageMetadata,
    };
  } catch (e: any) {
    console.error('Gemini API Error:', e);
    throw new ApiError(e.message || 'Gemini API failed', { status: getErrorStatus(e) || 500, code: getErrorCode(e) });
  }
};

export const translateText = async (text: string, from: string, to: string) => {
  const ai = await getAi();
  const prompt = `Translate the following text from ${from} to ${to}. Return ONLY the translation. Text: "${text}"`;
  const model = getGeminiModels().text.translation;
  const fallbackModel = resolveFallbackTextModel(model);

  try {
    const result = await withHighDemandRetry(
      {
        operation: 'translateText',
        model,
        fallbackModel,
        requestPayload: { prompt },
        run: activeModel => withTimeout(
          ai.models.generateContent({
            model: activeModel,
            contents: prompt,
          }),
          DEFAULT_TIMEOUT_MS
        ),
        mapSuccess: res => ({ text: res.text, usage: res.usageMetadata }),
      }
    );
    return { translatedText: result.text || '', usageMetadata: result.usageMetadata };
  } catch (e: any) {
    throw new ApiError(e.message || 'Translation failed', { status: getErrorStatus(e) || 500, code: getErrorCode(e) });
  }
};
