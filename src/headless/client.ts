// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { BackendAiContentReportRequest } from '../core/contracts/backend';
import { getGeminiModels } from '../core/config/models';
import { createCoreEventJournal, type CoreEventListener } from '../core-sdk/events';
import { createManagedAccountController } from '../core-sdk/managedAccount';
import { createManagedBackendClient } from '../core-sdk/managedBackendClient';
import { createManagedGeminiClient } from '../core-sdk/managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../core-sdk/runtime';
import {
  createDirectHeadlessAi,
  createDirectHeadlessFilePort,
  createManagedHeadlessFilePort,
  resolveHeadlessAccessMode,
  type HeadlessAccessMode,
  type HeadlessFilePort,
} from './access';
import { openHeadlessProfile, type HeadlessProfile, type HeadlessProfileState } from './profile';
import {
  createHeadlessCredentialProvider,
  type HeadlessCredentialOptions,
  type HeadlessCredentialProvider,
} from './credentials';
import {
  describeHeadlessAccessPolicy,
  HEADLESS_METHOD_ACCESS_POLICY,
  HEADLESS_METHODS,
  type HeadlessMethodName,
} from './accessPolicy';

export interface HeadlessClientOptions extends HeadlessCredentialOptions {
  profileName?: string;
  dataRoot?: string;
  backendBaseUrl?: string;
  firebaseIdToken?: string;
  appCheckToken?: string;
  accessMode?: HeadlessAccessMode;
  geminiApiKey?: string;
  onEvent?: CoreEventListener;
}

export interface HeadlessClient {
  accessMode: HeadlessAccessMode;
  profile: HeadlessProfile;
  state: HeadlessProfileState;
  events: ReturnType<typeof createCoreEventJournal>;
  backend: ReturnType<typeof createManagedBackendClient>;
  ai: ReturnType<typeof createManagedGeminiClient>;
  files: HeadlessFilePort;
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
  const accessMode = resolveHeadlessAccessMode(options.accessMode);

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
  const direct = accessMode === 'byok' ? createDirectHeadlessAi(options.geminiApiKey) : null;
  const ai = direct?.ai || createManagedGeminiClient(backend);
  const files = direct
    ? createDirectHeadlessFilePort(direct.direct, {
        list: () => state.byok.ownedFiles,
        add: async nameOrUri => {
          if (!state.byok.ownedFiles.includes(nameOrUri)) state.byok.ownedFiles.push(nameOrUri);
          await profile.save(state);
        },
        remove: async nameOrUri => {
          state.byok.ownedFiles = state.byok.ownedFiles.filter(candidate => candidate !== nameOrUri);
          await profile.save(state);
        },
      })
    : createManagedHeadlessFilePort(backend);

  return {
    accessMode,
    profile,
    state,
    events,
    backend,
    ai,
    files,
    account,
    credentials,
    runtime,
    lastNavigationUrl: () => navigationUrl,
    save: () => profile.save(state),
  };
};

export const describeHeadlessMethods = (accessMode: HeadlessAccessMode = 'managed') => {
  const methodInfo = {
    'system.describe': { mutates: false, params: [] },
    'profile.get': { mutates: false, params: ['includeState?'] },
    'auth.status': { mutates: false, params: [] },
    'auth.signIn': { mutates: true, params: ['operationId?'] },
    'auth.signOut': { mutates: true, params: ['operationId?'] },
    'auth.google.verifyHosted': { mutates: true, params: ['appUrl?', 'headless?', 'timeoutMs?'] },
    'language.list': { mutates: false, params: ['targetLanguageCode?', 'nativeLanguageCode?', 'limit? (1..500, default 100)'] },
    'language.select': { mutates: true, params: ['pairId? | targetLanguageCode + nativeLanguageCode'] },
    'chat.history': { mutates: false, params: ['languagePairId?'] },
    'chat.turn': { mutates: true, params: ['text', 'languagePairId?', 'useGoogleSearch?', 'requireInvariants?', 'fileParts?'] },
    'chat.attachment.turn': { mutates: true, external: true, params: ['text', 'fixture? (text|image|audio|pdf|svg|video|office) | dataUrl + mimeType', 'displayName?', 'languagePairId?', 'useGoogleSearch?', 'requireInvariants?', 'cleanup?'] },
    'suggestions.generate': { mutates: true, params: ['languagePairId?', 'assistantMessageId?', 'responseSource?', 'includeArtifactContent?'] },
    'suggestions.process': { mutates: true, external: true, params: ['languagePairId?', 'assistantMessageId?', 'responseSource?', 'syntheticDecision?', 'uploadGeneratedMedia?'] },
    'translation.create': { mutates: true, external: true, params: ['text', 'languagePairId?', 'from? (target|native)', 'attachToSuggestions?'] },
    'chat.reengage': { mutates: true, external: true, params: ['languagePairId?', 'runSuggestionAftersteps?'] },
    'media.image.generate': { mutates: true, params: ['contextText', 'languagePairId?', 'assistantMessageId?', 'maxAttempts? (1..7, default 2)', 'upload?', 'includeDataUrl?'] },
    'media.audioNote.generate': { mutates: true, external: true, params: ['text', 'langCode?', 'voiceName?', 'model?', 'upload?', 'includeDataUrl?'] },
    'media.music.generate': { mutates: true, external: true, params: ['prompt', 'durationSeconds? (8..20)', 'model?', 'upload?', 'includeDataUrl?'] },
    'speech.synthetic.live': { mutates: true, params: ['pcmBase64', 'sampleRate?', 'chunkDurationMs?', 'pace?', 'connectedTurns? (1..6, same PCM over one socket)', 'systemInstruction?', 'model?', 'gateInputOnSpeech?', 'semanticSpeech?', 'simulateUiSpeechHandoff?', 'requireRealtimeInputPacing?', 'playModelAudioRealtime?', 'includeVisual?', 'visualLabel?', 'timeoutMs?', 'includeModelAudio?'] },
    'speech.transcribe': { mutates: true, external: true, params: ['pcmBase64', 'sampleRate?', 'pace?', 'timeoutMs?', 'expectedTranscript?', 'minTranscriptWordRecall? (0..1, default 0.8)'] },
    'speech.tts.generate': { mutates: true, external: true, params: ['text', 'langCode?', 'voiceName?', 'model?', 'includeDataUrl?'] },
    'live.conversation.turn': { mutates: true, external: true, params: ['pcmBase64', 'sampleRate?', 'pace?', 'timeoutMs?', 'expectedTranscript?', 'minTranscriptWordRecall? (0..1, default 0.8)', 'includeVisual?', 'visualLabel?', 'instructionSuffix?', 'runSuggestionAftersteps?'] },
    'live.observer.turn': { mutates: true, external: true, params: ['pcmBase64', 'sampleRate?', 'pace?', 'timeoutMs?', 'expectedTranscript?', 'minTranscriptWordRecall? (0..1, default 0.8)', 'includeVisual?', 'visualLabel?', 'instructionSuffix?', 'runSuggestionAftersteps?'] },
    'journey.firstLesson': { mutates: true, external: true, params: ['languagePairId? | targetLanguageCode? + nativeLanguageCode?', 'pcmBase64?', 'expectedTranscript? (required with custom PCM)', 'minTranscriptWordRecall? (0..1, default 0.8)', 'paceLiveAudio?', 'timeoutMs?', 'includeSyntheticToolDecisions?', 'uploadGeneratedMedia? (default true in both access modes)'] },
    'account.summary': { mutates: false, params: ['operationId?'] },
    'account.ledgers': { mutates: false, params: ['limit?'] },
    'account.delete': { mutates: true, destructive: true, params: ['confirmation=DELETE', 'expectedUserId', 'operationId?'] },
    'billing.checkout.create': { mutates: true, external: true, params: ['packId'] },
    'billing.checkout.reconcile': { mutates: false, params: ['attempts?', 'intervalMs?'] },
    'billing.checkout.completeTest': { mutates: true, external: true, testOnly: true, params: ['packId', 'expectedCredits?', 'email?', 'headless?', 'timeoutMs?', 'attempts?', 'intervalMs?'] },
    'report.submit': { mutates: true, external: true, params: ['accessMode? (must match active mode)', 'messageId', 'reason', 'assistantText?', 'rawAssistantResponse?', 'notes?', 'surface?', 'model?', 'createdAtClient?'] },
    'gemini.generate': { mutates: true, external: true, params: ['model', 'contents', 'config?'] },
    'gemini.generateStream': { mutates: true, external: true, params: ['model', 'contents', 'config?'] },
    'files.upload': { mutates: true, external: true, params: ['dataUrl', 'mimeType', 'displayName?'] },
    'files.status': { mutates: false, params: ['uris'] },
    'files.delete': { mutates: true, destructive: true, params: ['nameOrUri'] },
    'files.clear': { mutates: true, destructive: true, params: [] },
  } satisfies Record<HeadlessMethodName, Record<string, unknown>>;
  const access = describeHeadlessAccessPolicy(accessMode);
  const describedMethodInfo = Object.fromEntries(HEADLESS_METHODS.map(method => [
    method,
    { ...methodInfo[method], ...HEADLESS_METHOD_ACCESS_POLICY[method] },
  ]));

  return {
  protocolVersion: '1.7.0',
  transport: 'json-rpc-2.0-ndjson',
  eventNotification: 'maestro.event',
  profileDefault: 'isolated-temporary',
  configuredModels: {
    text: getGeminiModels().text,
    image: getGeminiModels().image.generation,
    live: getGeminiModels().audio,
    music: getGeminiModels().music.generation,
  },
  methods: [...HEADLESS_METHODS],
  methodInfo: describedMethodInfo,
  access,
  releaseRequirements: [
    'Android external Stripe checkout stays disabled until Play programme enrollment is recorded.',
    'Every provider-parity method must be green in paired managed and BYOK release proof; BYOK keys come only from MAESTRO_GEMINI_API_KEY.',
    'BYOK cleanup is restricted to files recorded as owned by the active headless profile.',
    'Every Gemini Live transport requires a reviewed live-open reason; synthetic headless actions are audited as user.headless-live.',
    'Managed Live must use the server-observed gateway; provider-connected no-output sessions must create no charge and leave no reservation.',
  ],
  deferred: ['mcp'],
  };
};

export type HeadlessReportParams = BackendAiContentReportRequest;
