// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { BackendAiContentReportRequest } from '../core/contracts/backend';
import { createCoreEventJournal, type CoreEventListener } from '../core-sdk/events';
import { createManagedAccountController } from '../core-sdk/managedAccount';
import { createManagedBackendClient } from '../core-sdk/managedBackendClient';
import { createManagedGeminiClient } from '../core-sdk/managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../core-sdk/runtime';
import { openHeadlessProfile, type HeadlessProfile, type HeadlessProfileState } from './profile';
import {
  createHeadlessCredentialProvider,
  type HeadlessCredentialOptions,
  type HeadlessCredentialProvider,
} from './credentials';

export interface HeadlessClientOptions extends HeadlessCredentialOptions {
  profileName?: string;
  dataRoot?: string;
  backendBaseUrl?: string;
  firebaseIdToken?: string;
  appCheckToken?: string;
  onEvent?: CoreEventListener;
}

export interface HeadlessClient {
  profile: HeadlessProfile;
  state: HeadlessProfileState;
  events: ReturnType<typeof createCoreEventJournal>;
  backend: ReturnType<typeof createManagedBackendClient>;
  ai: ReturnType<typeof createManagedGeminiClient>;
  account: ReturnType<typeof createManagedAccountController>;
  credentials: HeadlessCredentialProvider;
  runtime: CoreRuntime;
  lastNavigationUrl(): string | null;
  save(): Promise<void>;
}

export const createHeadlessClient = async (options: HeadlessClientOptions = {}): Promise<HeadlessClient> => {
  const profile = await openHeadlessProfile({ name: options.profileName, dataRoot: options.dataRoot });
  const state = await profile.load();
  const events = createCoreEventJournal({ onEvent: options.onEvent });
  const runtime = createCoreRuntime({ events });
  let navigationUrl: string | null = null;
  const credentials = createHeadlessCredentialProvider(options);

  const backend = createManagedBackendClient({
    baseUrl: options.backendBaseUrl
      || process.env.MAESTRO_BACKEND_BASE_URL
      || process.env.VITE_BACKEND_BASE_URL
      || '',
    credentials: {
      getManagedHeaders: () => credentials.getManagedHeaders(true),
      getOptionalHeaders: () => credentials.getManagedHeaders(false),
    },
    session: {
      update: async updates => {
        if (updates.billingSummary) state.managed.billingSummary = updates.billingSummary;
        if (updates.entitlements) state.managed.entitlements = updates.entitlements;
        await profile.save(state);
      },
    },
  });

  const account = createManagedAccountController({
    backend,
    identity: {
      beginSignIn: () => credentials.signIn(),
      signOut: async () => {
        credentials.signOut();
      },
    },
    navigation: {
      navigate: url => {
        navigationUrl = url;
      },
    },
    runtime,
  });
  const ai = createManagedGeminiClient(backend);

  return {
    profile,
    state,
    events,
    backend,
    ai,
    account,
    credentials,
    runtime,
    lastNavigationUrl: () => navigationUrl,
    save: () => profile.save(state),
  };
};

const HEADLESS_METHODS = [
  'system.describe',
  'profile.get',
  'auth.status',
  'auth.signIn',
  'auth.signOut',
  'auth.google.verifyHosted',
  'language.list',
  'language.select',
  'chat.history',
  'chat.turn',
  'suggestions.generate',
  'media.image.generate',
  'speech.synthetic.live',
  'account.summary',
  'account.ledgers',
  'account.delete',
  'billing.checkout.create',
  'billing.checkout.reconcile',
  'billing.checkout.completeTest',
  'report.submit',
  'gemini.generate',
  'gemini.generateStream',
  'files.upload',
  'files.status',
  'files.delete',
  'files.clear',
  'live.token.create',
  'live.token.release',
] as const;

export const describeHeadlessMethods = () => ({
  protocolVersion: '1.0.0',
  transport: 'json-rpc-2.0-ndjson',
  eventNotification: 'maestro.event',
  profileDefault: 'isolated-temporary',
  methods: [...HEADLESS_METHODS],
  methodInfo: {
    'profile.get': { mutates: false, params: ['includeState?'] },
    'auth.signIn': { mutates: true, params: ['operationId?'] },
    'auth.signOut': { mutates: true, params: ['operationId?'] },
    'auth.google.verifyHosted': { mutates: true, params: ['appUrl?', 'headless?', 'timeoutMs?'] },
    'language.list': { mutates: false, params: ['targetLanguageCode?', 'nativeLanguageCode?', 'limit? (1..500, default 100)'] },
    'language.select': { mutates: true, params: ['pairId? | targetLanguageCode + nativeLanguageCode'] },
    'chat.turn': { mutates: true, params: ['text', 'languagePairId?', 'useGoogleSearch?', 'requireInvariants?', 'fileParts?'] },
    'suggestions.generate': { mutates: true, params: ['languagePairId?', 'assistantMessageId?', 'responseSource?', 'includeArtifactContent?'] },
    'media.image.generate': { mutates: true, params: ['contextText', 'languagePairId?', 'assistantMessageId?', 'maxAttempts? (1..7, default 2)', 'upload?', 'includeDataUrl?'] },
    'speech.synthetic.live': { mutates: true, params: ['pcmBase64', 'sampleRate?', 'chunkDurationMs?', 'pace?', 'systemInstruction?', 'model?', 'gateInputOnSpeech?', 'semanticSpeech?', 'timeoutMs?', 'includeModelAudio?'] },
    'account.ledgers': { mutates: false, params: ['limit?'] },
    'account.delete': { mutates: true, destructive: true, params: ['confirmation=DELETE', 'expectedUserId', 'operationId?'] },
    'billing.checkout.create': { mutates: true, external: true, params: ['packId'] },
    'billing.checkout.reconcile': { mutates: false, params: ['attempts?', 'intervalMs?'] },
    'billing.checkout.completeTest': { mutates: true, external: true, testOnly: true, params: ['packId', 'expectedCredits?', 'email?', 'headless?', 'timeoutMs?', 'attempts?', 'intervalMs?'] },
    'report.submit': { mutates: true, external: true, params: ['accessMode', 'messageId', 'reason', 'assistantText?', 'rawAssistantResponse?', 'notes?', 'surface?', 'model?', 'createdAtClient?'] },
    'gemini.generate': { mutates: true, external: true, params: ['model', 'contents', 'config?'] },
    'gemini.generateStream': { mutates: true, external: true, params: ['model', 'contents', 'config?'] },
    'files.upload': { mutates: true, external: true, params: ['dataUrl', 'mimeType', 'displayName?'] },
    'files.status': { mutates: false, params: ['uris'] },
    'files.delete': { mutates: true, destructive: true, params: ['nameOrUri'] },
    'files.clear': { mutates: true, destructive: true, params: [] },
    'live.token.create': { mutates: true, external: true, params: ['model', 'purpose?', 'config?', 'durationSeconds?'] },
    'live.token.release': { mutates: true, params: ['leaseId'] },
  },
  deferred: ['android.playBilling', 'mcp'],
});

export type HeadlessReportParams = BackendAiContentReportRequest;
