// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { ChatMessage, LanguagePair, ReplySuggestion } from '../../../core/types';
import type { GeminiProgressEvent } from '../../../core-sdk/gemini/generative';
import type { ReplySuggestionsInput, ReplySuggestionsOptions, ReplySuggestionsResult } from '../../../core-sdk/chat/suggestions';
import type { NormalizedSuggestionArtifact, NormalizedSuggestionToolRequest } from '../../../core-sdk/chat/suggestionAftersteps';
import { getVisibleAssistantMessageText } from '../../../core-sdk/chat/assistantMessageContext';
import { planSuggestionAftersteps } from '../../../core-sdk/chat/suggestionAfterstepPlan';
import { getGeminiModels } from '../../../core-sdk/modelRegistry';
import type { trackGeminiUsage as trackUsage } from '../../../shared/utils/costTracker';

type NewMessage = Omit<ChatMessage, 'id' | 'timestamp'> & Partial<Pick<ChatMessage, 'id' | 'timestamp'>>;

/** Browser application workflow. State is read at the same points as the hook's
 * former smart refs; persistence, UI activity and platform capabilities are ports.
 * Core owns provider composition/retries. This coordinator owns suggestion reuse,
 * progress, persistence order and artifact/tool completion. */
export interface SuggestionCoordinatorPorts {
  state: {
    getMessages(): ChatMessage[];
    getLanguagePair(): LanguagePair | undefined;
    getPairId(): string | null;
    isLoading(): boolean;
    setSuggestionOwner(id: string): void;
  };
  activity: { begin(): void; finish(): void };
  persistence: {
    getProfile(): Promise<{ text?: string } | null | undefined>;
    saveHistory(pairId: string, messages: ChatMessage[]): Promise<unknown>;
    saveProfile(text: string): Promise<unknown>;
    notifyProfileUpdated(): void;
  };
  runReplySuggestions(input: ReplySuggestionsInput, options: Pick<ReplySuggestionsOptions, 'lifecycleHooks'>): Promise<ReplySuggestionsResult>;
  normalizeSuggestionCreatorArtifact(artifact: unknown): NormalizedSuggestionArtifact | null;
  normalizeSuggestionCreatorToolRequest(tool: unknown, messageId: string): NormalizedSuggestionToolRequest | null;
  executeAssistantToolRequest(id: string, tool: NormalizedSuggestionToolRequest | null): Promise<void>;
  addMessage(message: NewMessage): string;
  updateMessage(id: string, patch: Partial<ChatMessage>): void;
  setReplySuggestions(suggestions: ReplySuggestion[]): void;
  setSuggestionsLoadingStreamText(text: string): void;
  getHistoryRespectingBookmark(history: ChatMessage[]): ChatMessage[];
  handleReengagementThresholdChange(seconds: number): void;
  formatGeminiStatusLine(event: GeminiProgressEvent): string | undefined;
  trackGeminiUsage: typeof trackUsage;
}

export function createSuggestionCoordinator(ports: SuggestionCoordinatorPorts) {
  const { state, activity, persistence, runReplySuggestions, normalizeSuggestionCreatorArtifact,
    normalizeSuggestionCreatorToolRequest, executeAssistantToolRequest, addMessage, updateMessage,
    setReplySuggestions, setSuggestionsLoadingStreamText, getHistoryRespectingBookmark,
    handleReengagementThresholdChange, formatGeminiStatusLine, trackGeminiUsage } = ports;

  return async function fetchAndSetReplySuggestions(
    assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[],
    options?: { responseSource?: 'chat' | 'live' },
  ): Promise<void> {
    let resolvedArtifact: unknown = null;
    let resolvedToolRequest: ReturnType<typeof normalizeSuggestionCreatorToolRequest> = null;

    const finishReplySuggestionsRequest = async () => {
      setSuggestionsLoadingStreamText('');
      activity.finish();
      const normalizedArtifact = normalizeSuggestionCreatorArtifact(resolvedArtifact);
      const assistantMessage = state.getMessages().find(message => message.id === assistantMessageId);
      const plan = planSuggestionAftersteps({
        mode: options?.responseSource === 'live' ? 'browser-live' : 'browser-chat',
        contextText: getVisibleAssistantMessageText(assistantMessage) || lastTutorMessage,
        artifact: normalizedArtifact, toolRequest: resolvedToolRequest,
      });
      for (const patch of plan.assistantPatches) updateMessage(assistantMessageId, patch);
      if (resolvedToolRequest) {
        const toolMessageId = plan.splitToolMessage ? addMessage(plan.splitToolMessage) : assistantMessageId;
        await executeAssistantToolRequest(toolMessageId, resolvedToolRequest);
      }
    };

    // Check if already loading using token state
    if (state.isLoading()) {
      return;
    }
    if (!lastTutorMessage.trim() || !state.getLanguagePair()) {
      setReplySuggestions([]);
      await finishReplySuggestionsRequest();
      return;
    }

    const hasReusableReplySuggestions = (message: ChatMessage): boolean => {
      if (!Array.isArray(message.replySuggestions) || !message.replySuggestions.length) return false;
      const hasAttachment = Boolean(
        (message.imageUrl && message.imageMimeType)
        || (Array.isArray(message.uploadedFileVariants) && message.uploadedFileVariants.length),
      );
      const raw = message.llmRawResponse?.trim();
      const hasStructuredTail = Boolean(raw && raw !== getVisibleAssistantMessageText(message).trim());
      return hasAttachment || !hasStructuredTail;
    };

    // Check if suggestions already exist on message
    {
      const allMsgs = state.getMessages();
      const targetIdx = allMsgs.findIndex(m => m.id === assistantMessageId);
      if (targetIdx !== -1) {
        const target = allMsgs[targetIdx];
        if (hasReusableReplySuggestions(target)) {
          state.setSuggestionOwner(target.id);
          setReplySuggestions(target.replySuggestions!);
          await finishReplySuggestionsRequest();
          return;
        }

        // Suggestions are owned by a whole assistant-only block, not just one
        // message object. The assistant can emit multiple adjacent messages when
        // tools/artifacts are split out, and we must not regenerate suggestions
        // for every sibling in that block once one already has them.
        let previousUserIdx = -1;
        for (let i = targetIdx - 1; i >= 0; i--) {
          if (allMsgs[i].role === 'user') {
            previousUserIdx = i;
            break;
          }
        }
        let nextUserIdx = allMsgs.length;
        for (let i = targetIdx + 1; i < allMsgs.length; i++) {
          if (allMsgs[i].role === 'user') {
            nextUserIdx = i;
            break;
          }
        }

        let blockSuggestionOwner: ChatMessage | null = null;
        for (let i = nextUserIdx - 1; i > previousUserIdx; i--) {
          const candidate = allMsgs[i];
          if (
            i !== targetIdx
            && candidate.role === 'assistant'
            && hasReusableReplySuggestions(candidate)
          ) {
            blockSuggestionOwner = candidate;
            break;
          }
        }

        if (blockSuggestionOwner?.replySuggestions) {
          state.setSuggestionOwner(blockSuggestionOwner.id);
          setReplySuggestions(blockSuggestionOwner.replySuggestions);
          await finishReplySuggestionsRequest();
          return;
        }
      }
    }

    // Add token for loading suggestions
    activity.begin();
    setReplySuggestions([]);
    setSuggestionsLoadingStreamText('');

    const suggestionHistory = getHistoryRespectingBookmark(history);
    let existingGlobalProfile = '';
    try {
      existingGlobalProfile = (await persistence.getProfile())?.text || '';
    } catch {
      existingGlobalProfile = '';
    }

    // Retry and structured-response validation now live in the shared Core SDK.
    try {
      let thoughtText = '';
      let outputText = '';
      const flushSuggestionsLoadingText = () => {
        // Show whichever stream is latest, condensed to a short single-line status
        const condensedThought = thoughtText.replace(/\s+/g, ' ').trim();
        const condensedOutput = outputText.replace(/\s+/g, ' ').trim();
        // Prefer output stream if available, otherwise show thought stream
        const active = condensedOutput || condensedThought;
        if (active) {
          const label = condensedOutput ? '' : 'thinking: ';
          const display = active.length > 48 ? `\u2026${active.slice(-48)}` : active;
          setSuggestionsLoadingStreamText(`${label}${display}`);
        }
      };

      const parsedResponse = await runReplySuggestions(
        {
          assistantMessageId,
          lastTutorMessage,
          history: suggestionHistory,
          languagePair: state.getLanguagePair()!,
          existingGlobalProfile,
          responseSource: options?.responseSource,
        },
        {
          lifecycleHooks: {
            onProgress: (event) => {
              const progressLine = formatGeminiStatusLine(event);
              if (progressLine && !thoughtText.trim() && !outputText.trim()) {
                setSuggestionsLoadingStreamText(progressLine);
              }
            },
            onThoughtDelta: (_, fullThought) => {
              thoughtText = fullThought || thoughtText;
              flushSuggestionsLoadingText();
            },
            onTextDelta: (_, fullText) => {
              outputText = fullText || outputText;
              flushSuggestionsLoadingText();
            },
          },
        },
      );

      trackGeminiUsage({
        feature: 'suggestions',
        configuredModel: parsedResponse.modelUsed || getGeminiModels().text.aux,
        modelVersion: parsedResponse.modelVersion,
        usageMetadata: parsedResponse.usageMetadata,
      });
      resolvedArtifact = parsedResponse?.artifact ?? null;
      resolvedToolRequest = normalizeSuggestionCreatorToolRequest(parsedResponse?.toolRequest ?? null, assistantMessageId);

      if (Array.isArray(parsedResponse.suggestions) &&
        parsedResponse.suggestions.every((s: any) => typeof s === 'object' && s !== null && 'target' in s && 'native' in s && typeof s.target === 'string' && typeof s.native === 'string')) {
        const suggestions = parsedResponse.suggestions as ReplySuggestion[];
        setReplySuggestions(suggestions);
        updateMessage(assistantMessageId, { replySuggestions: suggestions });
        try {
          const pid = state.getPairId();
          if (pid) { await persistence.saveHistory(pid, state.getMessages()); }
        } catch { }
      } else {
        console.warn("Parsed suggestions not in expected format:", parsedResponse.suggestions);
        setReplySuggestions([]);
      }

      if (typeof parsedResponse.reengagementSeconds === 'number' && parsedResponse.reengagementSeconds >= 5) {
        handleReengagementThresholdChange(parsedResponse.reengagementSeconds);
      }

      // Update chat summary on the message
      const newChatSummary = typeof parsedResponse.chatSummary === 'string' ? parsedResponse.chatSummary.trim() : '';
      if (newChatSummary) {
        updateMessage(assistantMessageId, { chatSummary: newChatSummary });
      }

      // Update global profile directly from the single API response (no second API call needed)
      try {
        const newGlobalProfile = typeof parsedResponse.globalProfile === 'string' ? parsedResponse.globalProfile.trim().slice(0, 10000) : '';
        if (newGlobalProfile) {
          await persistence.saveProfile(newGlobalProfile);
          // Notify UI components that the global profile was updated
          try { persistence.notifyProfileUpdated(); } catch { }
        }
      } catch (e) {
        console.warn('Failed to update global profile:', e);
      }

    } catch (error) {
      console.error('Error fetching reply suggestions:', error);
      setReplySuggestions([]);
    }
    await finishReplySuggestionsRequest();
  };
}
