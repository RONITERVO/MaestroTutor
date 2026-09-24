// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage } from '../../core/types';
import { buildCompactAssistantRawText } from './assistantMessageContext';
import type { NormalizedSuggestionArtifact, NormalizedSuggestionToolRequest } from './suggestionAftersteps';

/** These policies describe existing application behavior, not interchangeable
 * renderers. Headless retains legacy attachment fields and fallback raw text;
 * browser Live records compact model context and browser clears stale uploads. */
export type SuggestionAfterstepMode = 'browser-chat' | 'browser-live' | 'headless';
type SplitToolMessage = Pick<ChatMessage, 'role' | 'llmRawResponse' | 'rawAssistantResponse'>;

function buildLiveToolRawText(baseText: string, toolRequest: NormalizedSuggestionToolRequest): string {
  const normalizedBaseText = baseText.trim();
  const promptText = toolRequest.tool === 'image' ? (toolRequest.prompt || '').trim() : '';
  const rawSegments = [normalizedBaseText];
  if (promptText && !rawSegments.includes(promptText)) rawSegments.push(promptText);
  return buildCompactAssistantRawText(rawSegments.filter(Boolean).join('\n\n'), {
    toolRequest: { ...toolRequest, source: 'live-suggestion-creator' },
  });
}

/** Pure decisions only. Callers retain token release, ID/time allocation,
 * sequential message updates, media execution and persistence lifetimes. */
export function planSuggestionAftersteps(input: {
  mode: SuggestionAfterstepMode;
  contextText: string;
  artifact: NormalizedSuggestionArtifact | null;
  toolRequest: NormalizedSuggestionToolRequest | null;
}): { assistantPatches: Partial<ChatMessage>[]; splitToolMessage: SplitToolMessage | null } {
  const { mode, contextText, artifact, toolRequest } = input;
  const assistantPatches: Partial<ChatMessage>[] = [];
  const isLive = mode === 'browser-live';

  if (isLive && (artifact || toolRequest)) {
    const raw = artifact
      ? buildCompactAssistantRawText(contextText, { artifact: { mimeType: artifact.mimeType, fileName: artifact.fileName, dataUrl: artifact.dataUrl, source: 'live-suggestion-creator' } })
      : (toolRequest ? buildLiveToolRawText(contextText, toolRequest) : '');
    if (raw) assistantPatches.push({ llmRawResponse: raw });
  }

  if (artifact || !toolRequest) {
    const patch: Partial<ChatMessage> = { isLoadingArtifact: false };
    if (mode !== 'headless') patch.artifactLoadStartTime = undefined;
    if (artifact) {
      patch.imageUrl = artifact.dataUrl;
      patch.imageMimeType = artifact.mimeType;
      patch.attachmentName = artifact.fileName;
      if (mode !== 'headless') {
        patch.storageOptimizedImageUrl = undefined;
        patch.storageOptimizedImageMimeType = undefined;
        patch.uploadedFileVariants = undefined;
      }
    }
    assistantPatches.push(patch);
  }

  const splitToolMessage: SplitToolMessage | null = artifact && toolRequest
    ? mode === 'headless'
      ? { role: 'assistant', rawAssistantResponse: contextText || undefined }
      : { role: 'assistant', llmRawResponse: isLive ? buildLiveToolRawText(contextText, toolRequest) : undefined }
    : null;
  return { assistantPatches, splitToolMessage };
}
