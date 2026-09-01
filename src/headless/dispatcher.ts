// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {
  BackendAiContentReportRequest,
  BackendGenerateContentRequest,
} from '../core/contracts/backend';
import { createHash } from 'node:crypto';
import type { HeadlessClient } from './client';
import { describeHeadlessMethods } from './client';
import { listLanguagePairs, resolveLanguagePair } from '../core-sdk/chat/language';
import { runHeadlessChatTurn, runHeadlessSuggestions, selectHeadlessLanguage } from './chatJourney';
import { runHeadlessImageGeneration } from './mediaJourney';
import { createSyntheticPcmSource, decodePcm16LeBase64 } from '../core-sdk/media/pcmInput';
import { runSyntheticLiveJourney } from '../core-sdk/media/syntheticLiveJourney';
import { runStripeTestCheckoutJourney } from './billingJourney';
import { verifyHostedGoogleSignIn } from './hostedBrowser';

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
  switch (method) {
    case 'system.describe':
      return describeHeadlessMethods();
    case 'profile.get': {
      const chatSummaries = Object.entries(client.state.chats).map(([languagePairId, messages]) => ({
        languagePairId,
        messageCount: messages.length,
        lastMessageId: messages[messages.length - 1]?.id || null,
      }));
      return {
        name: client.profile.name,
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
      return client.credentials.describe();
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
    case 'media.image.generate':
      return runHeadlessImageGeneration(client, {
        contextText: requiredString(input, 'contextText'),
        languagePairId: typeof input.languagePairId === 'string' ? input.languagePairId : undefined,
        assistantMessageId: typeof input.assistantMessageId === 'string' ? input.assistantMessageId : undefined,
        maxAttempts: optionalBoundedInteger(input, 'maxAttempts', 2, 1, 7),
        upload: typeof input.upload === 'boolean' ? input.upload : undefined,
        includeDataUrl: input.includeDataUrl === true,
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
        source,
        systemInstruction: typeof input.systemInstruction === 'string' ? input.systemInstruction : undefined,
        model: typeof input.model === 'string' ? input.model : undefined,
        gateInputOnSpeech: typeof input.gateInputOnSpeech === 'boolean' ? input.gateInputOnSpeech : undefined,
        semanticSpeech: typeof input.semanticSpeech === 'boolean' ? input.semanticSpeech : undefined,
        timeoutMs: optionalNumber(input, 'timeoutMs', 45_000),
        includeModelAudio: input.includeModelAudio === true,
      }, { runtime: client.runtime });
    }
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
        const accessMode = requiredString(input, 'accessMode');
        if (accessMode !== 'byok' && accessMode !== 'managed') {
          throw new HeadlessDispatchError(-32602, 'Parameter "accessMode" must be "byok" or "managed".');
        }
        return {
          accessMode,
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
      return client.backend.generateContent({
        model: requiredString(input, 'model'),
        contents: input.contents,
        ...(input.config && typeof input.config === 'object' && !Array.isArray(input.config)
          ? { config: input.config as Record<string, unknown> }
          : {}),
      } as BackendGenerateContentRequest);
    case 'gemini.generateStream': {
      const chunks: unknown[] = [];
      for await (const chunk of await client.backend.generateContentStream({
        model: requiredString(input, 'model'),
        contents: input.contents,
        ...(input.config && typeof input.config === 'object' && !Array.isArray(input.config)
          ? { config: input.config as Record<string, unknown> }
          : {}),
      } as BackendGenerateContentRequest)) {
        chunks.push(chunk);
      }
      return { chunks };
    }
    case 'files.upload':
      return client.backend.uploadMedia({
        dataUrl: requiredString(input, 'dataUrl'),
        mimeType: requiredString(input, 'mimeType'),
        displayName: typeof input.displayName === 'string' ? input.displayName : undefined,
      });
    case 'files.status':
      return client.backend.checkFileStatuses({
        uris: Array.isArray(input.uris) ? input.uris.filter((uri): uri is string => typeof uri === 'string') : [],
      });
    case 'files.delete':
      return client.backend.deleteFile({ nameOrUri: requiredString(input, 'nameOrUri') });
    case 'files.clear':
      return client.backend.clearFiles();
    case 'live.token.create':
      return client.backend.createLiveToken({
        model: requiredString(input, 'model'),
        ...(input.purpose === 'music' ? { purpose: 'music' as const } : { purpose: 'live' as const }),
        ...(input.config && typeof input.config === 'object' && !Array.isArray(input.config)
          ? { config: input.config as Record<string, unknown> }
          : {}),
        ...(typeof input.durationSeconds === 'number' ? { durationSeconds: input.durationSeconds } : {}),
      });
    case 'live.token.release':
      return client.backend.releaseLiveTokenLease({ leaseId: requiredString(input, 'leaseId') });
    default:
      throw new HeadlessDispatchError(-32601, `Unknown headless method: ${method}`);
  }
};
