// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getGeminiModels } from '../../core/config/models';
import { ApiError, getAi } from './client';

export const generateGeminiResponse = async (
  modelName: string,
  userPrompt: string,
  history: any[],
  systemInstruction?: string,
  imageBase64?: string,
  imageMimeType?: string,
  imageFileUri?: string,
  useGoogleSearch?: boolean,
  configOverrides?: any
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
      const b64 = h.imageUrl.split(',')[1];
      if (b64) parts.push({ inlineData: { data: b64, mimeType: h.imageMimeType } });
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
    const b64 = imageBase64.split(',')[1];
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

  const log = debugLogService.logRequest('generateContent', modelName, { contents: redactedContents, config });

  try {
    const result = await ai.models.generateContent({
      model: modelName,
      contents,
      config,
    });
    log.complete({ text: result.text, usage: result.usageMetadata });
    return {
      text: result.text,
      candidates: result.candidates,
      usageMetadata: result.usageMetadata,
    };
  } catch (e: any) {
    console.error('Gemini API Error:', e);
    log.error(e);
    throw new ApiError(e.message || 'Gemini API failed', { status: e.status || 500, code: e.code });
  }
};

export const translateText = async (text: string, from: string, to: string) => {
  const ai = await getAi();
  const prompt = `Translate the following text from ${from} to ${to}. Return ONLY the translation. Text: "${text}"`;
  const model = getGeminiModels().text.translation;
  const log = debugLogService.logRequest('translateText', model, { prompt });

  try {
    const result = await ai.models.generateContent({
      model,
      contents: prompt,
    });
    log.complete({ text: result.text });
    return { translatedText: result.text || '' };
  } catch (e: any) {
    log.error(e);
    throw new ApiError(e.message || 'Translation failed', { status: e.status || 500, code: e.code });
  }
};
