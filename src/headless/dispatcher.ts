// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {
  BackendAiContentReportRequest,
} from '../core/contracts/backend';
import { createHash } from 'node:crypto';
import type { HeadlessClient } from './client';
import { describeHeadlessMethods } from './client';
import { listLanguagePairs, resolveLanguagePair } from '../core-sdk/chat/language';
import { runHeadlessChatTurn, runHeadlessSuggestions, selectHeadlessLanguage } from './chatJourney';
import { runHeadlessImageGeneration } from './mediaJourney';
import { runHeadlessMusicGeneration } from './musicJourney';
import { runHeadlessAttachmentTurn } from './attachmentJourney';
import type { SyntheticAttachmentKind } from './syntheticAttachments';
import { runHeadlessAudioNoteGeneration } from './audioNoteJourney';
import { createSyntheticPcmSource, decodePcm16LeBase64 } from '../core-sdk/media/pcmInput';
import { runSyntheticLiveJourney } from '../core-sdk/media/syntheticLiveJourney';
import { runStripeTestCheckoutJourney } from './billingJourney';
import { verifyHostedGoogleSignIn } from './hostedBrowser';
import { LIVE_OPEN_TRIGGER } from '../../shared/liveOpenReason';
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';
import { runHeadlessTranslation } from './translationJourney';
import { runHeadlessReengagement } from './reengagementJourney';
import { runHeadlessLiveTurn } from './liveJourney';
import { runHeadlessFirstLesson } from './firstLessonJourney';
import { assertHeadlessMethodAvailable } from './accessPolicy';

export class HeadlessDispatchError extends Error {
  constructor(public readonly rpcCode: -32601 | -32602, message: string) {
    super(message);
    this.name = 'HeadlessDispatchError';
  }
}

const asRecord = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  return value as Record<string, unknown>;
};

const requiredString = (record: Record<string, unknown>, key: string): string => {
  const value = record[key];
  if (typeof value !== 'string' || !value.trim()) {
    throw new HeadlessDispatchError(-32602, `Parameter "${key}" is required.`);
  }
  return value.trim();
};

const readPcm = (input: Record<string, unknown>, required = true): Int16Array | undefined => {
  if (typeof input.pcmBase64 !== 'string' || !input.pcmBase64.trim()) {
    if (required) throw new HeadlessDispatchError(-32602, 'Parameter "pcmBase64" is required.');
    return undefined;
  }
  return decodePcm16LeBase64(input.pcmBase64.trim().replace(/^data:audio\/[^;]+;base64,/i, ''));
};

const optionalNumber = (record: Record<string, unknown>, key: string, fallback: number): number => {
  const value = record[key];
  if (value === undefined) return fallback;
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new HeadlessDispatchError(-32602, `Parameter "${key}" must be a number.`);
  }
  return value;
};

const optionalBoundedInteger = (
  record: Record<string, unknown>,
  key: string,
  fallback: number,
  minimum: number,
  maximum: number,
): number => {
  const value = optionalNumber(record, key, fallback);
  if (!Number.isInteger(value) || value < minimum || value > maximum) {
    throw new HeadlessDispatchError(
      -32602,
      `Parameter "${key}" must be an integer between ${minimum} and ${maximum}.`,
    );
  }
  return value;
};

const compactLanguagePair = (pair: ReturnType<typeof resolveLanguagePair>) => ({
  id: pair.id,
  name: pair.name,
  targetLanguageName: pair.targetLanguageName,
  targetLanguageCode: pair.targetLanguageCode,
  nativeLanguageName: pair.nativeLanguageName,
  nativeLanguageCode: pair.nativeLanguageCode,
  isDefault: pair.isDefault === true,
});

export const summarizeHeadlessArtifact = (artifact: unknown) => {
  if (!artifact || typeof artifact !== 'object' || Array.isArray(artifact)) return null;
  const record = artifact as Record<string, unknown>;
  const content = typeof record.content === 'string' ? record.content : '';
  return {
    mimeType: typeof record.mimeType === 'string' ? record.mimeType : null,
    fileName: typeof record.fileName === 'string' ? record.fileName : null,
    encoding: typeof record.encoding === 'string' ? record.encoding : null,
    contentLength: content.length,
    contentSha256: content ? createHash('sha256').update(content).digest('hex') : null,
  };
};

export const dispatchHeadlessMethod = async (
  client: HeadlessClient,
  method: string,
  params?: unknown,
): Promise<unknown> => {
  const input = asRecord(params);
  if (method === 'system.describe') return describeHeadlessMethods(client.accessMode);
  assertHeadlessMethodAvailable(method, client.accessMode);
  switch (method) {
    case 'system.describe':
      return describeHeadlessMethods(client.accessMode);
    case 'profile.get': {
      const chatSummaries = Object.entries(client.state.chats).map(([languagePairId, messages]) => ({
        languagePairId,
        messageCount: messages.length,
        lastMessageId: messages[messages.length - 1]?.id || null,
      }));
      return {
        name: client.profile.name,
        accessMode: client.accessMode,
        directory: client.profile.directory,
        isolated: client.profile.isolated,
        stateSummary: {
          selectedLanguagePairId: client.state.settings.selectedLanguagePairId || null,
          chatCount: chatSummaries.length,
          chats: chatSummaries,
          globalProfileLength: client.state.globalProfile.length,
          updatedAt: client.state.updatedAt,
        },
        ...(input.includeState === true ? { state: client.state } : {}),
      };
    }
    case 'auth.status':
      return { accessMode: client.accessMode, ...(await client.credentials.describe()) };
    case 'auth.signIn': {
      const response = await client.account.signIn(
        typeof input.operationId === 'string' ? input.operationId : undefined,
      );
      return { user: response.account.user, billingSummary: response.account.billingSummary };
    }
    case 'auth.signOut':
      await client.account.signOut(typeof input.operationId === 'string' ? input.operationId : undefined);
      return { signedOut: true };
    case 'auth.google.verifyHosted':
      return verifyHostedGoogleSignIn({
        appUrl: typeof input.appUrl === 'string' ? input.appUrl : undefined,
        profileDirectory: client.profile.directory,
        headless: input.headless === true,
        timeoutMs: optionalNumber(input, 'timeoutMs', 240_000),
      });
    case 'language.list': {
      const targetLanguageCode = typeof input.targetLanguageCode === 'string'
        ? input.targetLanguageCode.trim().toLowerCase()
        : '';
      const nativeLanguageCode = typeof input.nativeLanguageCode === 'string'
        ? input.nativeLanguageCode.trim().toLowerCase()
        : '';
      const limit = optionalBoundedInteger(input, 'limit', 100, 1, 500);
      const matches = listLanguagePairs().filter(pair => (
        (!targetLanguageCode || pair.targetLanguageCode.toLowerCase() === targetLanguageCode)
        && (!nativeLanguageCode || pair.nativeLanguageCode.toLowerCase() === nativeLanguageCode)
      ));
      return {
        total: matches.length,
        returned: Math.min(matches.length, limit),
        truncated: matches.length > limit,
        pairs: matches.slice(0, limit).map(compactLanguagePair),
      };
    }
    case 'language.select': {
      const pair = await selectHeadlessLanguage(client, {
        pairId: typeof input.pairId === 'string' ? input.pairId : undefined,
        targetLanguageCode: typeof input.targetLanguageCode === 'string' ? input.targetLanguageCode : undefined,
        nativeLanguageCode: typeof input.nativeLanguageCode === 'string' ? input.nativeLanguageCode : undefined,
      });
      return compactLanguagePair(pair);
    }
    case 'chat.history': {
      const pair = resolveLanguagePair({
        pairId: typeof input.languagePairId === 'string'
          ? input.languagePairId
          : client.state.settings.selectedLanguagePairId,
      });
      return { languagePairId: pair.id, messages: client.state.chats[pair.id] || [] };
    }
    case 'chat.turn':
      return runHeadlessChatTurn(client, {
        text: requiredString(input, 'text'),
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        useGoogleSearch: typeof input.useGoogleSearch === 'boolean' ? input.useGoogleSearch : undefined,
        requireInvariants: typeof input.requireInvariants === 'boolean' ? input.requireInvariants : undefined,
        fileParts: Array.isArray(input.fileParts)
          ? input.fileParts.map(value => {
            const part = asRecord(value);
            return {
              fileUri: requiredString(part, 'fileUri'),
              mimeType: requiredString(part, 'mimeType'),
            };
          })
          : undefined,
      });
    case 'chat.attachment.turn': {
      const fixtureKinds = new Set<SyntheticAttachmentKind>([
        'text', 'image', 'audio', 'pdf', 'svg', 'video', 'office',
      ]);
      const hasFixture = Object.prototype.hasOwnProperty.call(input, 'fixture');
      const fixtureValue = hasFixture ? input.fixture : undefined;
      const fixture = typeof fixtureValue === 'string' && fixtureValue.trim()
        ? fixtureValue.trim() as SyntheticAttachmentKind
        : undefined;
      if (hasFixture && (!fixture || !fixtureKinds.has(fixture))) {
        throw new HeadlessDispatchError(
          -32602,
          'Parameter "fixture" must be text, image, audio, pdf, svg, video or office.',
        );
      }
      const hasDataUrl = Object.prototype.hasOwnProperty.call(input, 'dataUrl');
      const hasMimeType = Object.prototype.hasOwnProperty.call(input, 'mimeType');
      const dataUrl = typeof input.dataUrl === 'string' && input.dataUrl.trim()
        ? input.dataUrl.trim()
        : undefined;
      const mimeType = typeof input.mimeType === 'string' && input.mimeType.trim()
        ? input.mimeType.trim()
        : undefined;
      if (!fixture && (!dataUrl || !mimeType)) {
        throw new HeadlessDispatchError(
          -32602,
          'Provide a supported "fixture" or both non-empty "dataUrl" and "mimeType" parameters.',
        );
      }
      if ((hasDataUrl && !dataUrl) || (hasMimeType && !mimeType) || (hasDataUrl !== hasMimeType && !fixture)) {
        throw new HeadlessDispatchError(
          -32602,
          'Parameters "dataUrl" and "mimeType" must be non-empty strings and supplied together.',
        );
      }
      return runHeadlessAttachmentTurn(client, {
        text: requiredString(input, 'text'),
        fixture,
        dataUrl,
        mimeType,
        displayName: typeof input.displayName === 'string' ? input.displayName : undefined,
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        useGoogleSearch: typeof input.useGoogleSearch === 'boolean' ? input.useGoogleSearch : undefined,
        requireInvariants: typeof input.requireInvariants === 'boolean' ? input.requireInvariants : undefined,
        cleanup: input.cleanup === true,
      });
    }
    case 'suggestions.generate': {
      const result = await runHeadlessSuggestions(client, {
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
        responseSource: input.responseSource === 'live' ? 'live' : 'chat',
      });
      return {
        ...result,
        artifact: input.includeArtifactContent === true ? result.artifact : undefined,
        artifactSummary: summarizeHeadlessArtifact(result.artifact),
      };
    }
    case 'suggestions.process':
      return runHeadlessSuggestionAftersteps(client, {
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
        responseSource: input.responseSource === 'live' ? 'live' : 'chat',
        syntheticDecision: input.syntheticDecision && typeof input.syntheticDecision === 'object' && !Array.isArray(input.syntheticDecision)
          ? input.syntheticDecision as any
          : undefined,
        uploadGeneratedMedia: typeof input.uploadGeneratedMedia === 'boolean' ? input.uploadGeneratedMedia : undefined,
      });
    case 'translation.create': {
      const from = input.from === 'target' || input.from === 'native' ? input.from : undefined;
      if (input.from !== undefined && !from) throw new HeadlessDispatchError(-32602, 'Parameter "from" must be target or native.');
      return runHeadlessTranslation(client, {
        text: requiredString(input, 'text'),
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        from,
        attachToSuggestions: typeof input.attachToSuggestions === 'boolean' ? input.attachToSuggestions : undefined,
      });
    }
    case 'chat.reengage':
      return runHeadlessReengagement(client, {
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        runSuggestionAftersteps: typeof input.runSuggestionAftersteps === 'boolean' ? input.runSuggestionAftersteps : undefined,
      });
    case 'media.image.generate':
      return runHeadlessImageGeneration(client, {
        contextText: requiredString(input, 'contextText'),
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
        maxAttempts: optionalBoundedInteger(input, 'maxAttempts', 2, 1, 7),
        upload: typeof input.upload === 'boolean' ? input.upload : undefined,
        includeDataUrl: input.includeDataUrl === true,
      });
    case 'media.music.generate':
      return runHeadlessMusicGeneration(client, {
        prompt: requiredString(input, 'prompt'),
        durationSeconds: optionalBoundedInteger(input, 'durationSeconds', 12, 8, 20),
        model: typeof input.model === 'string' ? input.model : undefined,
        upload: input.upload === true,
        includeDataUrl: input.includeDataUrl === true,
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
      });
    case 'media.audioNote.generate':
      return runHeadlessAudioNoteGeneration(client, {
        text: requiredString(input, 'text'),
        langCode: typeof input.langCode === 'string' ? input.langCode : undefined,
        voiceName: typeof input.voiceName === 'string' ? input.voiceName : undefined,
        model: typeof input.model === 'string' ? input.model : undefined,
        upload: input.upload === true,
        includeDataUrl: input.includeDataUrl === true,
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
      });
    case 'speech.synthetic.live': {
      const sampleRate = optionalNumber(input, 'sampleRate', 16_000);
      const rawBase64 = requiredString(input, 'pcmBase64').replace(/^data:audio\/[^;]+;base64,/i, '');
      const source = createSyntheticPcmSource({
        pcm: decodePcm16LeBase64(rawBase64),
        sampleRate,
        chunkDurationMs: optionalNumber(input, 'chunkDurationMs', 20),
        pace: input.pace === true,
        runtime: client.runtime,
      });
      return runSyntheticLiveJourney(client.ai, {
        liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
        source,
        systemInstruction: typeof input.systemInstruction === 'string' ? input.systemInstruction : undefined,
        model: typeof input.model === 'string' ? input.model : undefined,
        gateInputOnSpeech: typeof input.gateInputOnSpeech === 'boolean' ? input.gateInputOnSpeech : undefined,
        semanticSpeech: typeof input.semanticSpeech === 'boolean' ? input.semanticSpeech : undefined,
        simulateUiSpeechHandoff: input.simulateUiSpeechHandoff === true,
        requireRealtimeInputPacing: input.requireRealtimeInputPacing === true,
        playModelAudioRealtime: input.playModelAudioRealtime === true,
        timeoutMs: optionalNumber(input, 'timeoutMs', 45_000),
        includeModelAudio: input.includeModelAudio === true,
      }, { runtime: client.runtime });
    }
    case 'speech.transcribe':
      return runHeadlessLiveTurn(client, {
        pcm: readPcm(input)!,
        sampleRate: optionalNumber(input, 'sampleRate', 16_000),
        mode: 'stt',
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        pace: input.pace === true,
        timeoutMs: optionalNumber(input, 'timeoutMs', 45_000),
        expectedTranscript: typeof input.expectedTranscript === 'string' ? input.expectedTranscript : undefined,
        minTranscriptWordRecall: typeof input.minTranscriptWordRecall === 'number' ? input.minTranscriptWordRecall : undefined,
      });
    case 'speech.tts.generate':
      return runHeadlessAudioNoteGeneration(client, {
        text: requiredString(input, 'text'),
        langCode: typeof input.langCode === 'string' ? input.langCode : undefined,
        voiceName: typeof input.voiceName === 'string' ? input.voiceName : undefined,
        model: typeof input.model === 'string' ? input.model : undefined,
        upload: false,
        includeDataUrl: input.includeDataUrl === true,
        exactTts: true,
      });
    case 'live.conversation.turn':
    case 'live.observer.turn':
      return runHeadlessLiveTurn(client, {
        pcm: readPcm(input)!,
        sampleRate: optionalNumber(input, 'sampleRate', 16_000),
        mode: method === 'live.observer.turn' ? 'observer' : 'conversation',
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        pace: input.pace === true,
        timeoutMs: optionalNumber(input, 'timeoutMs', 45_000),
        includeVisual: input.includeVisual === true,
        visualLabel: typeof input.visualLabel === 'string' ? input.visualLabel : undefined,
        instructionSuffix: typeof input.instructionSuffix === 'string' ? input.instructionSuffix : undefined,
        expectedTranscript: typeof input.expectedTranscript === 'string' ? input.expectedTranscript : undefined,
        minTranscriptWordRecall: typeof input.minTranscriptWordRecall === 'number' ? input.minTranscriptWordRecall : undefined,
        runSuggestionAftersteps: typeof input.runSuggestionAftersteps === 'boolean' ? input.runSuggestionAftersteps : undefined,
        uploadVisual: input.uploadVisual === true,
      });
    case 'journey.firstLesson':
      return runHeadlessFirstLesson(client, {
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        targetLanguageCode: typeof input.targetLanguageCode === 'string' ? input.targetLanguageCode : undefined,
        nativeLanguageCode: typeof input.nativeLanguageCode === 'string' ? input.nativeLanguageCode : undefined,
        pcm: readPcm(input, false),
        expectedTranscript: typeof input.expectedTranscript === 'string' ? input.expectedTranscript : undefined,
        minTranscriptWordRecall: typeof input.minTranscriptWordRecall === 'number' ? input.minTranscriptWordRecall : undefined,
        paceLiveAudio: typeof input.paceLiveAudio === 'boolean' ? input.paceLiveAudio : undefined,
        timeoutMs: optionalNumber(input, 'timeoutMs', 60_000),
        includeSyntheticToolDecisions: typeof input.includeSyntheticToolDecisions === 'boolean'
          ? input.includeSyntheticToolDecisions
          : undefined,
        uploadGeneratedMedia: typeof input.uploadGeneratedMedia === 'boolean'
          ? input.uploadGeneratedMedia
          : undefined,
      });
    case 'account.summary':
      return client.account.refreshAccount(typeof input.operationId === 'string' ? input.operationId : undefined);
    case 'account.ledgers':
      return client.account.listLedgers(optionalNumber(input, 'limit', 50));
    case 'account.delete': {
      const actualUserId = await client.credentials.getUserId();
      if (!actualUserId) throw new Error('Unable to resolve the authenticated Firebase user ID.');
      return client.account.deleteAccount({
        confirmation: requiredString(input, 'confirmation'),
        expectedUserId: requiredString(input, 'expectedUserId'),
        actualUserId,
        operationId: typeof input.operationId === 'string' ? input.operationId : undefined,
      });
    }
    case 'billing.checkout.create': {
      const result = await client.account.startStripeCheckout(requiredString(input, 'packId'));
      return { ...result, navigationUrl: client.lastNavigationUrl() };
    }
    case 'billing.checkout.reconcile': {
      const poll = client.account.startStripeReturnPolling({
        attempts: optionalNumber(input, 'attempts', 5),
        intervalMs: optionalNumber(input, 'intervalMs', 2000),
      });
      return poll.completion;
    }
    case 'billing.checkout.completeTest':
      return runStripeTestCheckoutJourney(client, {
        packId: requiredString(input, 'packId'),
        expectedCredits: optionalNumber(input, 'expectedCredits', 1_000),
        email: typeof input.email === 'string' ? input.email : undefined,
        headless: input.headless !== false,
        timeoutMs: optionalNumber(input, 'timeoutMs', 120_000),
        attempts: optionalNumber(input, 'attempts', 15),
        intervalMs: optionalNumber(input, 'intervalMs', 2_000),
        operationId: typeof input.operationId === 'string' ? input.operationId : undefined,
      });
    case 'report.submit':
      return client.account.submitAiContentReport((() => {
        const reason = requiredString(input, 'reason');
        const reasons = new Set(['sexual', 'hate', 'harassment', 'self-harm', 'violent', 'deceptive', 'spam', 'other']);
        if (!reasons.has(reason)) throw new HeadlessDispatchError(-32602, 'Parameter "reason" is not supported.');
        const requestedAccessMode = typeof input.accessMode === 'string' ? input.accessMode.trim() : client.accessMode;
        if (requestedAccessMode !== 'byok' && requestedAccessMode !== 'managed') {
          throw new HeadlessDispatchError(-32602, 'Parameter "accessMode" must be "byok" or "managed".');
        }
        if (requestedAccessMode !== client.accessMode) {
          throw new HeadlessDispatchError(-32602, 'Parameter "accessMode" must match the active headless access mode.');
        }
        return {
          accessMode: client.accessMode,
          messageId: requiredString(input, 'messageId'),
          reason,
          ...(typeof input.assistantText === 'string' ? { assistantText: input.assistantText } : {}),
          ...(typeof input.rawAssistantResponse === 'string' ? { rawAssistantResponse: input.rawAssistantResponse } : {}),
          ...(typeof input.notes === 'string' ? { notes: input.notes } : {}),
          ...(typeof input.surface === 'string' ? { surface: input.surface } : {}),
          ...(typeof input.model === 'string' ? { model: input.model } : {}),
          ...(typeof input.createdAtClient === 'number' || input.createdAtClient === null
            ? { createdAtClient: input.createdAtClient }
            : {}),
        } as BackendAiContentReportRequest;
      })());
    case 'gemini.generate':
      return client.ai.models.generateContent({
        model: requiredString(input, 'model'),
        contents: input.contents,
        ...(input.config && typeof input.config === 'object' && !Array.isArray(input.config)
          ? { config: input.config as Record<string, unknown> }
          : {}),
      });
    case 'gemini.generateStream': {
      const chunks: unknown[] = [];
      for await (const chunk of await client.ai.models.generateContentStream({
        model: requiredString(input, 'model'),
        contents: input.contents,
        ...(input.config && typeof input.config === 'object' && !Array.isArray(input.config)
          ? { config: input.config as Record<string, unknown> }
          : {}),
      })) {
        chunks.push(chunk);
      }
      return { chunks };
    }
    case 'files.upload':
      return client.files.upload({
        dataUrl: requiredString(input, 'dataUrl'),
        mimeType: requiredString(input, 'mimeType'),
        displayName: typeof input.displayName === 'string' ? input.displayName : undefined,
      });
    case 'files.status':
      return { statuses: await client.files.statuses(
        Array.isArray(input.uris) ? input.uris.filter((uri): uri is string => typeof uri === 'string') : [],
      ) };
    case 'files.delete':
      return client.files.delete(requiredString(input, 'nameOrUri'));
    case 'files.clear':
      return client.files.clear();
    default:
      throw new HeadlessDispatchError(-32601, `Unknown headless method: ${method}`);
  }
};
