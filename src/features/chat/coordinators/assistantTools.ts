// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { truncateForToolPrompt } from './toolPromptContext';
import type { AppSettings, ChatMessage, LanguagePair } from '../../../core/types';
import type { UseTutorConversationConfig, MutableValue } from './conversationContracts';
import type { createGeneratedImages } from './generatedImages';
import type { createMediaPersistence } from './mediaPersistence';
import type { synthesizeGeminiAudioNote as synthesizeAudio } from '../../speech/services/geminiLiveAudioNote';
import type { generateMusic as generateMusicApi } from '../../../api/gemini/music';
import { executeSuggestionToolRequest, type NormalizedSuggestionToolRequest } from '../../../core-sdk/chat/suggestionAftersteps';
import { getVisibleAssistantMessageText } from '../../../core-sdk/chat/assistantMessageContext';
import { getPrimarySubtag } from '../../../shared/utils/languageUtils';
type ToolAttachmentPhase = NonNullable<ChatMessage['toolAttachmentPhase']>;
export interface AssistantToolPorts extends Pick<UseTutorConversationConfig, 'updateMessage'> {
  messagesRef: MutableValue<ChatMessage[]>;
  settingsRef: MutableValue<AppSettings>;
  selectedLanguagePairRef: MutableValue<LanguagePair | undefined>;
  runAssistantImageGeneration: ReturnType<typeof createGeneratedImages>['runAssistantImageGeneration'];
  attachGeneratedToolMedia: ReturnType<typeof createMediaPersistence>['attachGeneratedToolMedia'];
  synthesizeGeminiAudioNote: typeof synthesizeAudio;
  generateMusic: typeof generateMusicApi;
}
/** Tool execution and visible attachment phases. Existing attachments suppress
 * duplicate work; afterstep planning and persistence belong to separate owners. */
export function createAssistantTools(ports: AssistantToolPorts) {
  const { updateMessage, messagesRef, selectedLanguagePairRef, settingsRef, runAssistantImageGeneration,
    attachGeneratedToolMedia, synthesizeGeminiAudioNote, generateMusic } = ports;
  async function executeAssistantToolRequest(
    assistantMessageId: string,
    toolRequest: NormalizedSuggestionToolRequest | null
  ) {
    const existing = messagesRef.current.find(message => message.id === assistantMessageId);
    updateMessage(assistantMessageId, {
      isLoadingArtifact: false,
      artifactLoadStartTime: undefined,
    });

    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileVariants && existing.uploadedFileVariants.length > 0))) {
      return;
    }

    if (!toolRequest) {
      return;
    }

    try {
      await executeSuggestionToolRequest(toolRequest, {
        image: async request => {
          const assistantMessage = messagesRef.current.find(m => m.id === assistantMessageId);
          const fullRawText = assistantMessage?.llmRawResponse
            || request.prompt
            || assistantMessage?.rawAssistantResponse
            || getVisibleAssistantMessageText(assistantMessage);
          await runAssistantImageGeneration({
            thinkingMessageId: assistantMessageId,
            accumulatedFullText: fullRawText,
          });
        },
        audioNote: async request => {
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: true,
            toolAttachmentStartTime: Date.now(),
            toolAttachmentPhase: 'pending' as ToolAttachmentPhase,
            maestroToolKind: 'audio-note',
          });
          const selectedLanguagePair = selectedLanguagePairRef.current;
          const langCode = getPrimarySubtag(selectedLanguagePair?.targetLanguageCode || settingsRef.current.stt.language || 'en');
          const audioNote = await synthesizeGeminiAudioNote({
            text: truncateForToolPrompt(request.text, 500),
            langCode,
            voiceName: settingsRef.current.tts.voiceName || 'Kore',
          });
          await attachGeneratedToolMedia({
            messageId: assistantMessageId,
            toolKind: 'audio-note',
            dataUrl: audioNote.dataUrl,
            mimeType: audioNote.mimeType,
            attachmentName: 'maestro-audio-note.wav',
          });
        },
        music: async request => {
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: true,
            toolAttachmentStartTime: Date.now(),
            toolAttachmentPhase: 'pending' as ToolAttachmentPhase,
            maestroToolKind: 'music',
          });
          const music = await generateMusic({
            prompt: request.prompt,
            durationSeconds: request.durationSeconds,
            onStreamPlaybackStart: () => {
              updateMessage(assistantMessageId, {
                isGeneratingToolAttachment: false,
                toolAttachmentStartTime: undefined,
                toolAttachmentPhase: 'streaming' as ToolAttachmentPhase,
                maestroToolKind: 'music',
              });
            },
          });
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: false,
            toolAttachmentStartTime: undefined,
            toolAttachmentPhase: 'finalizing' as ToolAttachmentPhase,
            maestroToolKind: 'music',
          });
          await attachGeneratedToolMedia({
            messageId: assistantMessageId,
            toolKind: 'music',
            dataUrl: music.dataUrl,
            mimeType: music.mimeType,
            attachmentName: 'maestro-music.wav',
          });
        },
      });
    } catch (error) {
      console.warn(`[MaestroTool] ${toolRequest.tool} generation failed.`, error);
      updateMessage(assistantMessageId, {
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
      });
    }
  }
  return { executeAssistantToolRequest };
}
