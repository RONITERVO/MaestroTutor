// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { TRIGGER_AUDIO_PCM_24K } from '../core-sdk/media/triggerAudioAsset';
import { decodePcm16LeBase64 } from '../core-sdk/media/pcmInput';
import type { HeadlessClient } from './client';
import { runHeadlessAttachmentTurn } from './attachmentJourney';
import { runHeadlessAudioNoteGeneration } from './audioNoteJourney';
import { runHeadlessChatTurn, selectHeadlessLanguage } from './chatJourney';
import { runHeadlessLiveTurn } from './liveJourney';
import { runHeadlessReengagement } from './reengagementJourney';
import { runHeadlessSuggestionAftersteps, type HeadlessSyntheticAfterstepDecision } from './suggestionJourney';
import { runHeadlessTranslation } from './translationJourney';
import {
  evaluateFirstLessonCoverage,
  FIRST_LESSON_ATTACHMENT_KINDS,
} from './firstLessonCoverage';
import {
  captureManagedJourneyBilling,
  evaluateManagedJourneyBilling,
  evaluateManagedJourneyFailureBilling,
  waitForManagedJourneyBillingSettlement,
} from './managedJourneyBilling';

const defaultSpeechPcm = (): Int16Array => {
  const source = decodePcm16LeBase64(TRIGGER_AUDIO_PCM_24K);
  const speech = new Int16Array(Math.floor(source.length * 2 / 3));
  for (let index = 0; index < speech.length; index += 1) {
    speech[index] = source[Math.min(source.length - 1, Math.floor(index * 3 / 2))];
  }
  // The bundled "Play" clip contains only about 0.5 seconds of VAD-active
  // speech. Repeat it so the release journey clears the same sustained-speech
  // guard as a real user while retaining deterministic, known words.
  const gapSamples = 1_600;
  const withSilence = new Int16Array((speech.length * 3) + (gapSamples * 2) + 12_000);
  withSilence.set(speech, 0);
  withSilence.set(speech, speech.length + gapSamples);
  withSilence.set(speech, (speech.length * 2) + (gapSamples * 2));
  return withSilence;
};

const summarizeTurn = (turn: any, kind: string) => ({
  kind,
  operationId: turn?.operationId || turn?.turn?.operationId || null,
  userMessageId: turn?.userMessage?.id || turn?.turn?.userMessage?.id || null,
  assistantMessageId: turn?.assistantMessage?.id || turn?.turn?.assistantMessage?.id || null,
  searchQueryCount: turn?.searchQueryCount ?? turn?.turn?.searchQueryCount ?? null,
  translationCount: turn?.assistantMessage?.translations?.length
    ?? turn?.turn?.assistantMessage?.translations?.length
    ?? null,
  streaming: turn?.streaming ?? turn?.turn?.streaming ?? null,
  inputTranscriptDeltaCount: turn?.inputTranscriptDeltaCount ?? null,
  outputTranscriptDeltaCount: turn?.outputTranscriptDeltaCount ?? null,
  sentVideoFrameCount: turn?.sentVideoFrameCount ?? null,
  capturedInputSamples: turn?.capturedInputSamples ?? null,
  capturedInputSha256: turn?.capturedInputSha256 ?? null,
  capturedModelSamples: turn?.capturedModelSamples ?? null,
  capturedModelSha256: turn?.capturedModelSha256 ?? null,
  transcriptEvidencePassed: turn?.transcriptEvidence?.passed ?? null,
  transcriptWordRecall: turn?.transcriptEvidence?.wordRecall ?? null,
  providerInputPacingPassed: turn?.realtimeEvidence?.providerInputPacingPassed ?? null,
  providerInputPacingElapsedMs: turn?.packetizer?.outputPacingElapsedMs ?? null,
  providerInputPacingWaitMs: turn?.packetizer?.outputPacingWaitMs ?? null,
  cleanedUp: turn?.cleanedUp ?? null,
  cleanupFailureCount: Array.isArray(turn?.cleanupFailures) ? turn.cleanupFailures.length : null,
});

export const runHeadlessFirstLesson = async (client: HeadlessClient, input: {
  languagePairId?: string;
  targetLanguageCode?: string;
  nativeLanguageCode?: string;
  pcm?: Int16Array;
  expectedTranscript?: string;
  minTranscriptWordRecall?: number;
  paceLiveAudio?: boolean;
  timeoutMs?: number;
  includeSyntheticToolDecisions?: boolean;
  uploadGeneratedMedia?: boolean;
}) => {
  const pair = await selectHeadlessLanguage(client, input.languagePairId
    ? { pairId: input.languagePairId }
    : {
        targetLanguageCode: input.targetLanguageCode || 'es-ES',
        nativeLanguageCode: input.nativeLanguageCode || 'en-US',
      });
  const operationId = client.runtime.ids.create('first-lesson');
  const managedBillingBefore = client.accessMode === 'managed'
    ? await captureManagedJourneyBilling(client, operationId)
    : null;
  if (managedBillingBefore?.account.account.billingSummary.reservedCredits) {
    throw new Error('Refusing to start managed parity proof while another operation has credits reserved.');
  }
  const pcm = input.pcm || defaultSpeechPcm();
  const expectedTranscript = input.expectedTranscript?.trim() || (input.pcm ? '' : 'Play');
  if (!expectedTranscript) {
    throw new Error('journey.firstLesson with custom pcmBase64 requires expectedTranscript so speech understanding is proved.');
  }
  const turns: Array<Record<string, unknown>> = [];
  const aftersteps: Array<Record<string, unknown>> = [];
  const uploadGeneratedMedia = input.uploadGeneratedMedia ?? true;
  const initialUserTurnCount = (client.state.chats[pair.id] || [])
    .filter(message => message.role === 'user').length;

  const runAftersteps = async (
    assistantMessageId: string,
    responseSource: 'chat' | 'live',
    syntheticDecision?: HeadlessSyntheticAfterstepDecision,
  ) => {
    const result = await runHeadlessSuggestionAftersteps(client, {
      languagePairId: pair.id,
      assistantMessageId,
      responseSource,
      syntheticDecision: input.includeSyntheticToolDecisions === false ? undefined : syntheticDecision,
      uploadGeneratedMedia,
    });
    const toolResult = result.toolResult && typeof result.toolResult === 'object'
      ? result.toolResult as { uploaded?: { uri?: unknown } | null }
      : null;
    aftersteps.push({
      assistantMessageId,
      responseSource,
      decisionSource: result.decisionSource,
      suggestionCount: result.suggestionResult.suggestions.length,
      visiblyStreamed: result.suggestionResult.streaming.visiblyStreamed,
      hasArtifact: Boolean(result.artifact),
      tool: result.toolRequest?.tool || null,
      toolMessageId: result.toolMessageId,
      uploaded: result.toolRequest ? Boolean(toolResult?.uploaded?.uri) : null,
    });
    return result;
  };
  const runLiveWithRetry = async (params: Parameters<typeof runHeadlessLiveTurn>[1]) => {
    let lastError: unknown;
    const failedLiveRequestIds: string[] = [];
    for (let attempt = 1; attempt <= 2; attempt += 1) {
      try {
        return await runHeadlessLiveTurn(client, { ...params, runSuggestionAftersteps: false });
      } catch (error) {
        lastError = error;
        const failedRequestId = error && typeof error === 'object'
          && typeof (error as { operationId?: unknown }).operationId === 'string'
          ? (error as { operationId: string }).operationId
          : '';
        if (failedRequestId) failedLiveRequestIds.push(failedRequestId);
        client.runtime.events.emit({
          operationId, journey: 'live', phase: attempt < 2 ? 'firstLesson.retrying' : 'firstLesson.failed',
          data: { attempt, mode: params.mode, hasVisual: params.includeVisual === true },
        });
      }
    }
    if (managedBillingBefore) {
      try {
        const managedBillingEvidence = evaluateManagedJourneyFailureBilling(
          managedBillingBefore,
          await waitForManagedJourneyBillingSettlement(client, operationId),
          failedLiveRequestIds,
        );
        client.runtime.events.emit({
          operationId,
          journey: 'billing',
          phase: 'firstLesson.failureReconciled',
          data: { ...managedBillingEvidence },
        });
        if (lastError && typeof lastError === 'object') {
          Object.assign(lastError, { managedBillingEvidence });
        }
      } catch (billingError) {
        client.runtime.events.emit({
          operationId,
          journey: 'billing',
          phase: 'firstLesson.failureReconciliationFailed',
          data: {
            errorMessage: billingError instanceof Error ? billingError.message : String(billingError),
          },
        });
      }
    }
    throw lastError;
  };

  client.runtime.events.emit({
    operationId, journey: 'chat', phase: 'firstLesson.started',
    data: { accessMode: client.accessMode, languagePairId: pair.id },
  });

  const first = await runHeadlessChatTurn(client, {
    text: 'Start my first lesson with one friendly, very short greeting.',
    languagePairId: pair.id,
    useGoogleSearch: false,
  });
  turns.push(summarizeTurn(first, 'text'));
  await runAftersteps(first.assistantMessage.id, 'chat');

  const second = await runHeadlessChatTurn(client, {
    text: 'Use Google Search and teach me one current, harmless fact about today in one short sentence.',
    languagePairId: pair.id,
    useGoogleSearch: true,
  });
  turns.push(summarizeTurn(second, 'google-search'));
  await runAftersteps(second.assistantMessage.id, 'chat', {
    toolRequest: { tool: 'image', prompt: 'A simple illustrated vocabulary flashcard based on this lesson.' },
  });

  for (const fixture of FIRST_LESSON_ATTACHMENT_KINDS) {
    const attachment = await runHeadlessAttachmentTurn(client, {
      text: `Teach me one useful phrase inspired by this ${fixture} attachment.`,
      fixture,
      languagePairId: pair.id,
      // The preceding Search turn persists the user's Search toggle. Each
      // attachment case is intentionally a plain multimodal tutor request.
      useGoogleSearch: false,
      cleanup: true,
      requireInvariants: true,
    });
    turns.push(summarizeTurn(attachment, `attachment-${fixture}`));
    const syntheticDecision = fixture === 'image'
      ? { toolRequest: { tool: 'audio-note', text: 'Repeat this lesson phrase slowly and clearly.' } }
      : fixture === 'pdf'
        ? { toolRequest: { tool: 'music', prompt: 'A calm original language-practice backing track.', durationSeconds: 8 } }
        : undefined;
    await runAftersteps(attachment.turn.assistantMessage.id, 'chat', syntheticDecision);
  }

  const stt = await runLiveWithRetry({
    pcm,
    mode: 'stt',
    languagePairId: pair.id,
    pace: input.paceLiveAudio ?? true,
    timeoutMs: input.timeoutMs,
    expectedTranscript,
    minTranscriptWordRecall: input.minTranscriptWordRecall,
  });
  const spokenTurn = await runHeadlessChatTurn(client, {
    text: stt.transcript || 'Play',
    languagePairId: pair.id,
    useGoogleSearch: false,
  });
  turns.push({ ...summarizeTurn(spokenTurn, 'speech-to-text-chat'), sttOperationId: stt.operationId });
  await runAftersteps(spokenTurn.assistantMessage.id, 'chat');

  const liveAudio = await runLiveWithRetry({
    pcm,
    mode: 'conversation',
    languagePairId: pair.id,
    pace: input.paceLiveAudio ?? true,
    timeoutMs: input.timeoutMs,
    expectedTranscript,
    minTranscriptWordRecall: input.minTranscriptWordRecall,
  });
  turns.push(summarizeTurn(liveAudio, 'live-audio'));
  await runAftersteps(liveAudio.assistantMessage.id, 'live');

  const liveVisual = await runLiveWithRetry({
    pcm,
    mode: 'conversation',
    languagePairId: pair.id,
    pace: input.paceLiveAudio ?? true,
    timeoutMs: input.timeoutMs,
    expectedTranscript,
    minTranscriptWordRecall: input.minTranscriptWordRecall,
    includeVisual: true,
    visualLabel: 'RED APPLE',
    instructionSuffix: 'When the learner says Play, briefly use the visible red apple in the lesson.',
  });
  turns.push(summarizeTurn(liveVisual, 'live-audio-video'));
  await runAftersteps(liveVisual.assistantMessage.id, 'live');

  const observerAudio = await runLiveWithRetry({
    pcm,
    mode: 'observer',
    languagePairId: pair.id,
    pace: input.paceLiveAudio ?? true,
    timeoutMs: input.timeoutMs,
    expectedTranscript,
    minTranscriptWordRecall: input.minTranscriptWordRecall,
    instructionSuffix: 'Act as the silent observer. Respond only after detected speech.',
  });
  turns.push(summarizeTurn(observerAudio, 'silent-observer-audio'));
  await runAftersteps(observerAudio.assistantMessage.id, 'live');

  const observer = await runLiveWithRetry({
    pcm,
    mode: 'observer',
    languagePairId: pair.id,
    pace: input.paceLiveAudio ?? true,
    timeoutMs: input.timeoutMs,
    expectedTranscript,
    minTranscriptWordRecall: input.minTranscriptWordRecall,
    includeVisual: true,
    visualLabel: 'RED APPLE',
    instructionSuffix: 'Act as the silent observer. Respond only after detected speech, and use the visible red apple.',
  });
  turns.push(summarizeTurn(observer, 'silent-observer-audio-video'));
  await runAftersteps(observer.assistantMessage.id, 'live');

  const translation = await runHeadlessTranslation(client, {
    text: 'Thank you, Maestro.', languagePairId: pair.id, from: 'native', attachToSuggestions: true,
  });
  const tts = await runHeadlessAudioNoteGeneration(client, {
    text: translation.translatedText,
    langCode: pair.targetLanguageCode,
    upload: false,
    exactTts: true,
  });
  const reengagement = await runHeadlessReengagement(client, {
    languagePairId: pair.id,
    runSuggestionAftersteps: true,
  });

  const managedBillingEvidence = managedBillingBefore
    ? evaluateManagedJourneyBilling(
        managedBillingBefore,
        await waitForManagedJourneyBillingSettlement(client, operationId),
      )
    : {
        applicable: false as const,
        passed: true,
        payer: 'byok-api-key-owner' as const,
        reason: 'Direct Gemini billing is owned by the supplied API-key project, outside Maestro credits.',
      };

  const history = client.state.chats[pair.id] || [];
  const totalUserTurnCount = history.filter(message => message.role === 'user').length;
  const userTurnCount = totalUserTurnCount - initialUserTurnCount;
  const coverage = evaluateFirstLessonCoverage({
    accessMode: client.accessMode,
    userTurnCount,
    requireToolUploads: uploadGeneratedMedia,
    turns,
    aftersteps,
    stt,
    liveAudio,
    liveVisual,
    observerAudio,
    observerVisual: observer,
    translation,
    tts,
    reengagement,
    managedBillingEvidence,
  });
  const passed = Object.values(coverage).every(Boolean);
  client.runtime.events.emit({
    operationId, journey: 'chat', phase: passed ? 'firstLesson.completed' : 'firstLesson.failedCoverage',
    data: { accessMode: client.accessMode, userTurnCount, totalUserTurnCount, ...coverage },
  });
  if (!passed) {
    const error = new Error('The first-lesson journey completed but did not prove every required coverage invariant.') as Error & { coverage?: unknown };
    error.coverage = coverage;
    throw error;
  }
  return {
    operationId,
    accessMode: client.accessMode,
    uploadGeneratedMedia,
    managedBillingEvidence,
    languagePairId: pair.id,
    userTurnCount,
    totalUserTurnCount,
    turns,
    aftersteps,
    translation: {
      operationId: translation.operationId,
      translatedTextLength: translation.translatedText.length,
      attachedToAssistantMessageId: translation.assistantMessageId,
    },
    tts: {
      operationId: tts.operationId,
      sampleCount: tts.sampleCount,
      triggerAudioSamplesSent: tts.triggerAudioSamplesSent,
      triggerPacketCount: tts.triggerPacketCount,
      dataSha256: tts.dataSha256,
    },
    reengagement: {
      operationId: reengagement.operationId,
      emptyUserRequest: reengagement.emptyUserRequest,
      userMessagePersisted: reengagement.userMessagePersisted,
      streaming: reengagement.turn.streaming,
    },
    coverage,
    passed,
  };
};
