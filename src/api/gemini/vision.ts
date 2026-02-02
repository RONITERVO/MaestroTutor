// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getGeminiModels } from '../../core/config/models';
import { getAi } from './client';

// Add invisible noise to text without changing its meaning
const addNoise = (text: string): string => {
  const timestamp = Date.now();
  const randomId = Math.random().toString(36).substring(2, 8);
  return `${text} <!-- ${timestamp}_${randomId} -->`;
};

export const generateImage = async (params: {
  prompt?: string;
  history?: any[];
  latestMessageText?: string;
  latestMessageRole?: 'user' | 'assistant';
  systemInstruction?: string;
  maestroAvatarUri?: string;
  maestroAvatarMimeType?: string;
}) => {
  const ai = await getAi();
  const { prompt, latestMessageText, history, systemInstruction, maestroAvatarUri, maestroAvatarMimeType } = params;

  const contents: any[] = [];

  let processedHistory = history ? history.map(h => ({ ...h })) : [];

  let imageCount = 0;
  for (let i = processedHistory.length - 1; i >= 0; i--) {
    const h = processedHistory[i];
    const mime = (h.imageMimeType || '').toLowerCase();
    const isImage = mime.startsWith('image/');

    if (h.imageFileUri) {
      if (!isImage) {
        h.imageFileUri = undefined;
        const type = mime.split('/')[0] || 'File';
        const note = ` [${type} context omitted]`;
        if (h.rawAssistantResponse) h.rawAssistantResponse += note;
        else h.text = (h.text || '') + note;
      } else {
        if (imageCount >= 3) {
          h.imageFileUri = undefined;
          const note = ' [Previous image context omitted]';
          if (h.rawAssistantResponse) h.rawAssistantResponse += note;
          else h.text = (h.text || '') + note;
        } else {
          imageCount++;
        }
      }
    }
  }

  if (processedHistory.length > 0) {
    processedHistory.forEach(h => {
      const parts: any[] = [];
      const textContent = h.rawAssistantResponse || h.text;
      if (textContent) parts.push({ text: textContent });

      if (h.imageFileUri) {
        const m = (h.imageMimeType || '').toLowerCase();
        if (m.startsWith('image/')) {
          parts.push({ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } });
        }
      }
      if (parts.length) contents.push({ role: h.role === 'assistant' ? 'model' : 'user', parts });
    });
  }

  const currentParts: any[] = [];
  if (prompt) {
    currentParts.push({ text: addNoise(prompt) });
  } else if (latestMessageText) {
    currentParts.push({ text: addNoise(latestMessageText) });
  }

  if (maestroAvatarUri) {
    const m = (maestroAvatarMimeType || '').toLowerCase();
    if (!m || m.startsWith('image/')) {
      currentParts.push({ fileData: { fileUri: maestroAvatarUri, mimeType: maestroAvatarMimeType || 'image/png' } });
    }
  }

  if (currentParts.length) {
    // Map role: 'assistant' -> 'model', default to 'user'
    const role = params.latestMessageRole === 'assistant' ? 'model' : (params.latestMessageRole || 'user');
    contents.push({ role, parts: currentParts });
  }

  const model = getGeminiModels().image.generation;
  const config = { responseModalities: ['IMAGE'], systemInstruction };
  const log = debugLogService.logRequest('generateImage', model, { contents, config });

  try {
    const result = await ai.models.generateContent({
      model,
      contents,
      config: config as any,
    });

    log.complete({ candidates: result.candidates?.length });

    const candidates = result.candidates || [];
    for (const c of candidates) {
      for (const part of c.content?.parts || []) {
        const inlineData = part.inlineData;
        if (inlineData && inlineData.mimeType?.startsWith('image/')) {
          if (typeof inlineData.data === 'string' && inlineData.data.trim() !== '') {
            return { base64Image: `data:${inlineData.mimeType};base64,${inlineData.data}`, mimeType: inlineData.mimeType };
          }
        }
      }
    }
    // No valid image found in response
    log.error(new Error('No image generated'));
    return { error: 'No image generated' };
  } catch (e: any) {
    log.error(e);
    return { error: e.message };
  }
};
