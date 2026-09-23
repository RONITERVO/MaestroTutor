// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ConversationDiagnostics } from './conversationContracts';
import type { AppSettings, ChatMessage, GroundingChunk, LanguagePair } from '../../../core/types';
import type { GeminiProgressEvent } from '../../../core-sdk/gemini/generative';
import type { UseTutorConversationConfig, MutableValue } from './conversationContracts';
import type { runTutorTextTurn as runText } from '../../../api/gemini/journeys';
import type { trackGeminiUsage as trackUsage } from '../../../shared/utils/costTracker';
import { getGeminiModels } from '../../../core-sdk/modelRegistry';
import { formatStreamingTutorDraftText } from '../../../core-sdk/chat/tutorResponse';
const MAX_THINKING_TRACE_LINES = 8;
const THINKING_DRAFT_FLUSH_INTERVAL_MS = 120;

export interface TextResponsePorts extends Pick<UseTutorConversationConfig, 't' | 'setSettings' | 'updateMessage'> {
  diagnostics: Pick<ConversationDiagnostics, 'logSttFlow' | 'errorSttFlow'>;
  messagesRef: MutableValue<ChatMessage[]>;
  selectedLanguagePairRef: MutableValue<LanguagePair | undefined>;
  runTutorTextTurn: typeof runText;
  trackGeminiUsage: typeof trackUsage;
  setLatestGroundingChunks(chunks: GroundingChunk[] | undefined): void;
  formatGeminiPhaseLabel(event: GeminiProgressEvent): string | undefined;
  formatGeminiStatusLine(event: GeminiProgressEvent): string | undefined;
}

/** Streaming projection and completion: preserves throttle windows, visible
 * thinking state, usage order and the raw response retained for later prompts. */
export function createTextResponseCoordinator(ports: TextResponsePorts) {
  const { logSttFlow, errorSttFlow } = ports.diagnostics;
  const { t, setSettings, updateMessage, messagesRef, selectedLanguagePairRef, runTutorTextTurn,
    trackGeminiUsage, setLatestGroundingChunks, formatGeminiPhaseLabel, formatGeminiStatusLine } = ports;
  const appendThinkingTrace = (messageId: string, line: string) => {
    const cleanedLine = line.trim();
    if (!cleanedLine) return;

    const current = messagesRef.current.find(m => m.id === messageId);
    if (!current || !current.thinking) return;

    const prevTrace = Array.isArray(current.thinkingTrace)
      ? current.thinkingTrace.filter(item => typeof item === 'string' && item.trim().length > 0)
      : [];
    if (prevTrace[prevTrace.length - 1] === cleanedLine) return;

    const nextTrace = [...prevTrace, cleanedLine].slice(-MAX_THINKING_TRACE_LINES);
    updateMessage(messageId, { thinkingTrace: nextTrace });
  };

  const setThinkingStatusLine = (messageId: string, line?: string) => {
    const cleanedLine = typeof line === 'string' ? line.trim() : '';
    const current = messagesRef.current.find(m => m.id === messageId);
    if (!current || !current.thinking) return;

    const nextValue = cleanedLine || undefined;
    if ((current.thinkingStatusLine || undefined) === nextValue) return;
    updateMessage(messageId, { thinkingStatusLine: nextValue });
  };

  const handleGeminiResponse = async (params: {
    thinkingMessageId: string;
    geminiPromptText: string;
    sanitizedDerivedHistory: any[];
    systemInstructionForGemini: string;
    imageForGeminiContextFileUri?: Array<{ fileUri: string; mimeType: string }>;
    currentSettingsVal: AppSettings;
  }) => {
    let geminiStage = 'gemini.prepare.start';
    const markGeminiStage = (stage: string, details?: Record<string, unknown>) => {
      geminiStage = stage;
      logSttFlow(stage, details);
    };
    let lastProcessingBucket = -1;
    let streamingDraftText = '';
    let lastDraftFlushAt = 0;
    let thoughtBuffer = '';
    let lastThoughtFlushAt = 0;
    let currentPhaseLabel = '';
    let hasVisibleModelOutput = false;

    const flushThinkingDraft = (force = false) => {
      const now = Date.now();
      if (!force && now - lastDraftFlushAt < THINKING_DRAFT_FLUSH_INTERVAL_MS) return;
      lastDraftFlushAt = now;
      const draftToShow = formatStreamingTutorDraftText(
        streamingDraftText,
        selectedLanguagePairRef.current?.nativeLanguageCode
      );
      const current = messagesRef.current.find(m => m.id === params.thinkingMessageId);
      if (!current || !current.thinking) return;
      if ((current.thinkingDraftText || '') === draftToShow) return;
      updateMessage(params.thinkingMessageId, { thinkingDraftText: draftToShow });
    };

    const flushThoughtTrace = (force = false) => {
      const condensed = thoughtBuffer.replace(/\s+/g, ' ').trim();
      if (!condensed) return;
      const now = Date.now();
      if (!force && condensed.length < 80 && now - lastThoughtFlushAt < 2000) return;
      appendThinkingTrace(params.thinkingMessageId, condensed.slice(0, 220));
      thoughtBuffer = '';
      lastThoughtFlushAt = now;
    };

    try {
      markGeminiStage('gemini.request.start', {
        thinkingMessageId: params.thinkingMessageId,
        promptLength: params.geminiPromptText.length,
        historyCount: params.sanitizedDerivedHistory.length,
        filePartCount: params.imageForGeminiContextFileUri?.length || 0,
        useGoogleSearch: params.currentSettingsVal.enableGoogleSearch,
      });
      const turn = await runTutorTextTurn(
        {
          model: getGeminiModels().text.default,
          prompt: params.geminiPromptText,
          history: params.sanitizedDerivedHistory,
          nativeLanguageCode: selectedLanguagePairRef.current?.nativeLanguageCode || '',
          systemInstruction: params.systemInstructionForGemini,
          currentFileParts: params.imageForGeminiContextFileUri,
          useGoogleSearch: params.currentSettingsVal.enableGoogleSearch,
        },
        {
          onGoogleSearchUnavailable: () => {
            setSettings(prev => prev.enableGoogleSearch
              ? { ...prev, enableGoogleSearch: false }
              : prev
            );
          },
          lifecycleHooks: {
            onProgress: (event) => {
              if (event.phase === 'attempt-processing') {
                const bucket = Math.floor((event.elapsedMs || 0) / 12000);
                if (bucket > 0 && bucket !== lastProcessingBucket) {
                  lastProcessingBucket = bucket;
                  markGeminiStage('gemini.request.processing', {
                    thinkingMessageId: params.thinkingMessageId,
                    elapsedMs: event.elapsedMs || 0,
                    bucket,
                  });
                }
              }
              const phaseLabel = formatGeminiPhaseLabel(event);
              if (phaseLabel && phaseLabel !== currentPhaseLabel) {
                currentPhaseLabel = phaseLabel;
                updateMessage(params.thinkingMessageId, { thinkingPhase: phaseLabel });
              }
              if (!hasVisibleModelOutput) {
                const line = formatGeminiStatusLine(event);
                if (line) {
                  setThinkingStatusLine(params.thinkingMessageId, line);
                }
              }
            },
            onTextDelta: (_, fullText) => {
              hasVisibleModelOutput = true;
              streamingDraftText = fullText || streamingDraftText;
              setThinkingStatusLine(params.thinkingMessageId, undefined);
              flushThinkingDraft(false);
            },
            onThoughtDelta: (deltaThought) => {
              hasVisibleModelOutput = true;
              thoughtBuffer += deltaThought;
              setThinkingStatusLine(params.thinkingMessageId, undefined);
              if (currentPhaseLabel !== 'Thinking') {
                currentPhaseLabel = 'Thinking';
                updateMessage(params.thinkingMessageId, { thinkingPhase: t('streaming.phaseThinking') || 'Thinking' });
              }
              flushThoughtTrace(false);
            },
          },
        },
      );
      const response = turn.response;

      markGeminiStage('gemini.request.done', {
        thinkingMessageId: params.thinkingMessageId,
        responseTextLength: response.text?.length || 0,
      });
      flushThinkingDraft(true);
      flushThoughtTrace(true);

      const searchQueries = turn.searchQueryCount;
      trackGeminiUsage({
        feature: 'tutor',
        configuredModel: response.modelUsed || getGeminiModels().text.default,
        modelVersion: response.modelVersion,
        usageMetadata: response.usageMetadata,
        searchQueries,
      });

      const accumulatedFullText = turn.rawResponse;
      const strictParsedResponse = turn.parsed;
      const responseTextForConversation = strictParsedResponse.visibleText;
      const parsedTranslationsOnComplete = strictParsedResponse.translations;
      const groundingChunks = response.candidates?.[0]?.groundingMetadata?.groundingChunks as GroundingChunk[] | undefined;
      if (groundingChunks?.length) {
        setLatestGroundingChunks(groundingChunks);
      }

      const finalMessageUpdates: Partial<ChatMessage> = {
        thinking: false,
        thinkingTrace: undefined,
        thinkingDraftText: undefined,
        thinkingPhase: undefined,
        thinkingStatusLine: undefined,
        translations: parsedTranslationsOnComplete.length > 0 ? parsedTranslationsOnComplete : undefined,
        llmRawResponse: accumulatedFullText,
        rawAssistantResponse: responseTextForConversation || undefined,
        text: parsedTranslationsOnComplete.length === 0 ? (responseTextForConversation || undefined) : undefined,
        isLoadingArtifact: strictParsedResponse.hasSkippedNonLanguageContent,
        artifactLoadStartTime: strictParsedResponse.hasSkippedNonLanguageContent ? Date.now() : undefined,
      };
      updateMessage(params.thinkingMessageId, finalMessageUpdates);
      markGeminiStage('gemini.response.applied', {
        thinkingMessageId: params.thinkingMessageId,
        hasAttachmentCandidate: strictParsedResponse.hasSkippedNonLanguageContent,
        translationCount: parsedTranslationsOnComplete.length,
      });

      return {
        accumulatedFullText: responseTextForConversation,
        finalMessageUpdates,
        hasAttachmentCandidate: strictParsedResponse.hasSkippedNonLanguageContent,
      };
    } catch (error) {
      errorSttFlow('gemini.request.error', {
        stage: geminiStage,
        thinkingMessageId: params.thinkingMessageId,
        message: error instanceof Error ? error.message : String(error),
      });
      throw error;
    }
  };
  return handleGeminiResponse;
}
