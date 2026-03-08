// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getGeminiModels } from '../../core/config/models';
import { ApiError, getAi } from './client';

const DEFAULT_TIMEOUT_MS = 600_000; // 10 minutes
const HIGH_DEMAND_MAX_RETRIES = 10;
const HIGH_DEMAND_RETRY_BASE_DELAY_MS = 1_000;
const HIGH_DEMAND_RETRY_MAX_DELAY_MS = 8_000;

const withTimeout = <T>(promise: Promise<T>, ms: number): Promise<T> => {
  let timeoutId: ReturnType<typeof setTimeout>;
  const timeout = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(() => reject(new Error(`Request timed out after ${ms / 1000}s`)), ms);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timeoutId));
};

const sleep = (ms: number): Promise<void> => new Promise(resolve => setTimeout(resolve, ms));

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

const buildRetryMeta = (attempt: number, retryInMs?: number) => ({
  attempt,
  totalAttempts: HIGH_DEMAND_MAX_RETRIES + 1,
  ...(typeof retryInMs === 'number' ? { retryInMs } : {}),
});

const withHighDemandRetry = async <T>(opts: {
  operation: string;
  model: string;
  requestPayload: any;
  run: () => Promise<T>;
  mapSuccess: (result: T) => any;
}): Promise<T> => {
  let lastError: any;

  for (let attempt = 0; attempt <= HIGH_DEMAND_MAX_RETRIES; attempt++) {
    const attemptNumber = attempt + 1;
    const attemptLog = debugLogService.logRequest(opts.operation, opts.model, {
      ...opts.requestPayload,
      retry: buildRetryMeta(attemptNumber),
    });

    try {
      const result = await opts.run();
      attemptLog.complete({ ...opts.mapSuccess(result), retry: buildRetryMeta(attemptNumber) });
      return result;
    } catch (error: any) {
      lastError = error;

      const canRetry = attempt < HIGH_DEMAND_MAX_RETRIES && isHighDemandError(error);
      let retryInMs: number | undefined;
      if (canRetry) {
        retryInMs = Math.min(
          HIGH_DEMAND_RETRY_MAX_DELAY_MS,
          HIGH_DEMAND_RETRY_BASE_DELAY_MS * (2 ** attempt)
        );
      }
      attemptLog.error({
        status: getErrorStatus(error),
        code: getErrorCode(error),
        message: getNestedErrorMessage(error) || error?.message || 'Gemini API failed',
        retry: buildRetryMeta(attemptNumber, retryInMs),
      });

      if (!canRetry) {
        throw error;
      }

      console.warn(
        `[Gemini] ${opts.operation} hit high-demand/unavailable response ` +
        `(attempt ${attempt + 1}/${HIGH_DEMAND_MAX_RETRIES + 1}), retrying in ${retryInMs}ms.`
      );
      await sleep(retryInMs!);
    }
  }

  throw lastError;
};

export const generateGeminiResponse = async (
  modelName: string,
  userPrompt: string,
  history: any[],
  systemInstruction?: string,
  imageBase64?: string,
  imageMimeType?: string,
  imageFileUri?: string,
  useGoogleSearch?: boolean,
  configOverrides?: any,
  timeoutMs: number = DEFAULT_TIMEOUT_MS
) => {
  const ai = await getAi();
  const contents: any[] = [];

  history.forEach(h => {
    const parts: any[] = [];
    const textContent = h.rawAssistantResponse || h.text;
    if (textContent) parts.push({ text: textContent });

    if (h.imageFileUri) {
      parts.push({ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } });
    } else if (h.imageUrl && h.imageMimeType) {
      const b64 = h.imageUrl.substring(h.imageUrl.indexOf(',') + 1);
      if (b64) parts.push({ inlineData: { data: b64, mimeType: h.imageMimeType } });
    }

    if (h.avatarFileUri) {
      parts.push({ fileData: { fileUri: h.avatarFileUri, mimeType: h.avatarMimeType || 'image/jpeg' } });
    }

    if (parts.length > 0) {
      const role = h.role === 'assistant' ? 'model' : 'user';
      contents.push({ role, parts });
    }
  });

  const currentParts: any[] = [{ text: userPrompt }];
  if (imageFileUri) {
    currentParts.push({ fileData: { fileUri: imageFileUri, mimeType: imageMimeType || 'image/jpeg' } });
  } else if (imageBase64 && imageMimeType) {
    const b64 = imageBase64.substring(imageBase64.indexOf(',') + 1);
    if (b64) currentParts.push({ inlineData: { data: b64, mimeType: imageMimeType } });
  }

  contents.push({ role: 'user', parts: currentParts });

  const config: any = { ...configOverrides };
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
    const result = await withHighDemandRetry(
      {
        operation: 'generateContent',
        model: modelName,
        requestPayload: { contents: redactedContents, config },
        run: () => withTimeout(
          ai.models.generateContent({
            model: modelName,
            contents,
            config,
          }),
          timeoutMs
        ),
        mapSuccess: result => ({ text: result.text, usage: result.usageMetadata }),
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

  try {
    const result = await withHighDemandRetry(
      {
        operation: 'translateText',
        model,
        requestPayload: { prompt },
        run: () => withTimeout(
          ai.models.generateContent({
            model,
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
