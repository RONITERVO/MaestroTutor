// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getGeminiModels } from '../../core/config/models';
import { loadManagedAccessSession, saveManagedAccessSession } from '../../core/security/managedAccessSessionStorage';
import { collapseGeminiContents } from '../../shared/utils/conversationTurns';
import { getAi } from './client';
import { maestroAccessService } from '../../services/access/maestroAccessService';
import { maestroBackendService } from '../../services/backend/maestroBackendService';

const TIMEOUT_MS = 600_000; // 10 minutes

const withTimeout = <T>(promise: Promise<T>, ms: number): Promise<T> => {
  let timeoutId: ReturnType<typeof setTimeout>;
  const timeout = new Promise<never>((_, reject) => {
    timeoutId = setTimeout(() => reject(new Error(`Request timed out after ${ms / 1000}s`)), ms);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timeoutId));
};

// Add invisible noise to text without changing its meaning
const addNoise = (text: string): string => {
  const timestamp = Date.now();
  const randomId = Math.random().toString(36).substring(2, 8);
  return `${text} <!-- ${timestamp}_${randomId} -->`;
};

const updateManagedBillingSummary = async (billingSummary: unknown): Promise<void> => {
  if (!billingSummary || typeof billingSummary !== 'object') return;
  const session = await loadManagedAccessSession();
  if (!session) return;
  await saveManagedAccessSession({
    ...session,
    billingSummary: billingSummary as typeof session.billingSummary,
    lastSyncedAt: Date.now(),
  });
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
  const { prompt, latestMessageText, history, systemInstruction, maestroAvatarUri, maestroAvatarMimeType } = params;

  const rawContents: any[] = [];

  let processedHistory = history ? history.map(h => ({ ...h })) : [];

  const getImageFileParts = (item: any): Array<{ fileUri: string; mimeType: string }> => {
    const rawParts = Array.isArray(item?.fileParts) ? item.fileParts : [];
    return rawParts.filter((part: any) => {
      const fileUri = typeof part?.fileUri === 'string' ? part.fileUri.trim() : '';
      const mimeType = typeof part?.mimeType === 'string' ? part.mimeType.trim().toLowerCase() : '';
      return !!fileUri && mimeType.startsWith('image/');
    });
  };

  // Track the index of the first kept image (3rd from the end with an image)
  // Image generation expects strict alternation: user (text only) -> model (image only)
  let firstKeptImageIndex = -1;
  let imageCount = 0;
  for (let i = processedHistory.length - 1; i >= 0; i--) {
    const h = processedHistory[i];
    const imageParts = getImageFileParts(h);

    if (Array.isArray(h.fileParts) && h.fileParts.length > 0) {
      if (imageParts.length === 0) {
        const firstMime = typeof h.fileParts[0]?.mimeType === 'string' ? h.fileParts[0].mimeType.toLowerCase() : '';
        h.fileParts = undefined;
        const type = firstMime.split('/')[0] || 'File';
        const note = ` [${type} context omitted]`;
        if (h.rawAssistantResponse) h.rawAssistantResponse += note;
        else h.text = (h.text || '') + note;
      } else {
        if (imageCount >= 3) {
          h.fileParts = undefined;
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

  // Build the image-model payload in its stricter text/image alternation shape
  // first, then do one final same-role collapse at the end. This lets us keep
  // the image-specific transformation logic readable while still protecting
  // Gemini from adjacent same-side turns in the final payload.
  // Build contents array from processed history
  // Image generation payload must be: user (text only) -> model (image only) -> user (text only) -> model (image only) ...
  // Transform:
  // - User message with image: split into user turn (text only) + model turn (image only)
  // - Assistant message with text+image: split into user turn (text only) + model turn (image only)
  if (processedHistory.length > 0) {
    processedHistory.forEach((h, idx) => {
      let textContent = h.rawAssistantResponse || h.text;
      const imageParts = getImageFileParts(h);
      const firstImagePart = imageParts[0];
      const hasImage = !!firstImagePart;

      // Prepend contextSummary to the first user message when history was trimmed
      if (contextSummary && idx === 0 && h.role === 'user') {
        const summaryPrefix = `[Conversation Summary from earlier context]\n${contextSummary}\n\n`;
        textContent = summaryPrefix + (textContent || '');
      }

      if (h.role === 'user') {
        // User turn: text only (no image)
        if (textContent) {
          rawContents.push({ role: 'user', parts: [{ text: textContent }] });
        }
        // If user had an image, add it as a separate model turn after
        if (hasImage) {
          rawContents.push({ role: 'model', parts: [{ fileData: { fileUri: firstImagePart.fileUri, mimeType: firstImagePart.mimeType } }] });
        }
      } else if (h.role === 'assistant') {
        // Assistant turn: if has text, add as user turn first
        if (textContent) {
          rawContents.push({ role: 'user', parts: [{ text: textContent }] });
        }
        // Assistant image goes as model turn (image only)
        if (hasImage) {
          rawContents.push({ role: 'model', parts: [{ fileData: { fileUri: firstImagePart.fileUri, mimeType: firstImagePart.mimeType } }] });
        }
      }
    });
  }

  // If history was trimmed and contextSummary exists but first message wasn't user, prepend summary as separate turn
  if (contextSummary && processedHistory.length > 0 && processedHistory[0]?.role !== 'user') {
    const summaryText = `[Conversation Summary from earlier context]\n${contextSummary}`;
    rawContents.unshift({ role: 'user', parts: [{ text: summaryText }] });
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
    rawContents.push({ role: 'user', parts: currentParts });
  }

  // The image path also needs the same send-time collapse as text generation
  // because trimmed summaries, imported chats, or split assistant updates can
  // otherwise leave repeated user/model turns in the final request.
  const contents = collapseGeminiContents(rawContents);

  const model = getGeminiModels().image.generation;
  const config = { responseModalities: ['IMAGE'], systemInstruction };
  const log = debugLogService.logRequest('generateImage', model, { contents, config });

  try {
    const result = maestroBackendService.isConfigured() && await maestroAccessService.isUsingManagedAccess()
      ? await withTimeout(
          maestroBackendService.generateContent({
            model,
            contents,
            config: config as unknown as Record<string, unknown>,
            operation: 'generateImage',
          }),
          TIMEOUT_MS
        )
      : await withTimeout(
          (await getAi()).models.generateContent({
            model,
            contents,
            config: config as any,
          }),
          TIMEOUT_MS
        );

    await updateManagedBillingSummary((result as { billingSummary?: unknown }).billingSummary);

    log.complete({ candidates: result.candidates?.length });

    const candidates = Array.isArray(result.candidates) ? result.candidates : [];
    for (const c of candidates) {
      const parts = Array.isArray((c as any)?.content?.parts) ? (c as any).content.parts : [];
      for (const part of parts) {
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
      const parts = Array.isArray((c as any)?.content?.parts) ? (c as any).content.parts : [];
      for (const part of parts) {
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
