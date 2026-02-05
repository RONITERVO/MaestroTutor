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

  // Track the index of the first kept image (3rd from the end with an image)
  // Image generation expects strict alternation: user (text only) -> model (image only)
  let firstKeptImageIndex = -1;
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
          firstKeptImageIndex = i; // Update to earliest kept image index
        }
      }
    }
  }

  // Drop messages before the first kept image to reduce payload size.
  // Extract chatSummary from the first kept image message (or most recent before it) to preserve maximal context.
  let contextSummary: string | undefined;
  if (firstKeptImageIndex > 0 && imageCount >= 3) {
    // Find the most recent chatSummary at or before firstKeptImageIndex
    for (let i = firstKeptImageIndex; i >= 0; i--) {
      const msg = processedHistory[i];
      if (msg.role === 'assistant' && typeof msg.chatSummary === 'string' && msg.chatSummary.trim()) {
        contextSummary = msg.chatSummary.trim();
        break;
      }
    }
    processedHistory = processedHistory.slice(firstKeptImageIndex);
  }

  // Build contents array from processed history
  // Image generation payload must be: user (text only) -> model (image only) -> user (text only) -> model (image only) ...
  // Transform:
  // - User message with image: split into user turn (text only) + model turn (image only)
  // - Assistant message with text+image: split into user turn (text only) + model turn (image only)
  if (processedHistory.length > 0) {
    processedHistory.forEach((h, idx) => {
      let textContent = h.rawAssistantResponse || h.text;
      const hasImage = h.imageFileUri && (h.imageMimeType || '').toLowerCase().startsWith('image/');

      // Prepend contextSummary to the first user message when history was trimmed
      if (contextSummary && idx === 0 && h.role === 'user') {
        const summaryPrefix = `[Conversation Summary from earlier context]\n${contextSummary}\n\n`;
        textContent = summaryPrefix + (textContent || '');
      }

      if (h.role === 'user') {
        // User turn: text only (no image)
        if (textContent) {
          contents.push({ role: 'user', parts: [{ text: textContent }] });
        }
        // If user had an image, add it as a separate model turn after
        if (hasImage) {
          contents.push({ role: 'model', parts: [{ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } }] });
        }
      } else if (h.role === 'assistant') {
        // Assistant turn: if has text, add as user turn first
        if (textContent) {
          contents.push({ role: 'user', parts: [{ text: textContent }] });
        }
        // Assistant image goes as model turn (image only)
        if (hasImage) {
          contents.push({ role: 'model', parts: [{ fileData: { fileUri: h.imageFileUri, mimeType: h.imageMimeType || 'image/jpeg' } }] });
        }
      }
    });
  }

  // If history was trimmed and contextSummary exists but first message wasn't user, prepend summary as separate turn
  if (contextSummary && processedHistory.length > 0 && processedHistory[0]?.role !== 'user') {
    const summaryText = `[Conversation Summary from earlier context]\n${contextSummary}`;
    contents.unshift({ role: 'user', parts: [{ text: summaryText }] });
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
    // Always add the latest message as user turn (image gen thinks it's the assistant replying)
    contents.push({ role: 'user', parts: currentParts });
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
    // No valid image found in response - extract any text response for debugging
    let textResponse = '';
    for (const c of candidates) {
      for (const part of c.content?.parts || []) {
        if (part.text) textResponse += part.text;
      }
    }
    const errorMsg = textResponse ? `No image generated. Model responded: ${textResponse}` : 'No image generated';
    log.error(new Error(errorMsg));
    return { error: errorMsg };
  } catch (e: any) {
    log.error(e);
    return { error: e.message };
  }
};
