// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Baselines recorded against ab2923d BEFORE prompt centralization. See docs/PROMPT_CONTRACTS.md.
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as prompts from '../../core/config/prompts';
import { ALL_LANGUAGES } from '../../core/config/languages';
import { createLanguagePairObject } from '../../shared/utils/languageUtils';
import type { ChatMessage } from '../../core/types';
import { generateGeminiResponse, translateText } from '../../api/gemini/generative';
import { runReplySuggestions } from '../chat/suggestions';
import { deriveHistoryForApi } from '../chat/history';
import { ART_STYLE_REFERENCE_TEXT } from '../chat/artStyleReference';
import { buildCompactAssistantHistoryText } from '../chat/assistantMessageContext';
import { buildLiveSystemInstruction } from '../../features/live/utils/liveSystemInstruction';
import { buildLiveSttSystemInstruction } from '../media/liveSessionInstructions';
import { buildTriggeredTtsSystemInstruction } from '../media/triggeredTts';
import { runCoreAudioNoteGeneration } from '../media/audioNoteGeneration';
import { runSyntheticLiveJourney } from '../media/syntheticLiveJourney';
import { createSyntheticPcmSource } from '../media/pcmInput';
import { runMaestroImageGeneration } from '../chat/imageGeneration';
import { createCoreRuntime } from '../runtime';
import { createCoreEventJournal } from '../events';
import { createEmptyHeadlessProfileState } from '../../headless/profile';
import { runHeadlessLiveTurn } from '../../headless/liveJourney';
import { runHeadlessChatTurn } from '../../headless/chatJourney';
import { runHeadlessReengagement } from '../../headless/reengagementJourney';
import type { HeadlessClient } from '../../headless/client';
import { LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

vi.mock('../../features/session', () => ({ getGlobalProfileDB: async () => ({ text: 'Likes music and apples.' }) }));
vi.mock('../diagnostics', () => ({ debugLogService: { logRequest: () => ({ complete() {}, error() {} }) } }));
vi.mock('../../shared/utils/costTracker', () => ({ trackGeminiUsage() {} }));

const pairFor = (target: string, native: string) => createLanguagePairObject(
  ALL_LANGUAGES.find(language => language.langCode === target)!,
  ALL_LANGUAGES.find(language => language.langCode === native)!,
);
const profile = 'Likes music and apples.';
const summary = 'Practised greetings; next discuss fruit.';
const text = '¿Cómo se dice "apple"?\n[FI] omena — 你好';
const artifact = '<svg viewBox="0 0 20 20"><text>Hola</text></svg>';
const history: ChatMessage[] = [
  { id: 'u1', timestamp: 1, role: 'user', text: 'Hola', uploadedFileVariants: [
    { id: 'image', uri: 'files/user-image', mimeType: 'image/png', targets: ['chat'], source: 'original', order: 1 },
  ] },
  { id: 'u2', timestamp: 2, role: 'user', text: 'Una manzana, por favor.' },
  { id: 'a1', timestamp: 3, role: 'assistant', text: 'Hola.\n[EN] Hello.', chatSummary: summary,
    rawAssistantResponse: `Hola.\n[EN] Hello.\n\n\`\`\`svg\n${artifact}\n\`\`\`\n\`\`\`maestro-tool\n{"tool":"music","prompt":"Calm piano","durationSeconds":8}\n\`\`\``,
    imageUrl: `data:image/svg+xml;base64,${Buffer.from(artifact).toString('base64')}`, imageMimeType: 'image/svg+xml', attachmentName: 'hello.svg',
    replySuggestions: [{ target: 'Gracias', native: 'Thank you' }],
  },
  { id: 'u3', timestamp: 4, role: 'user', text: '' },
];
const suggestionResponse = JSON.stringify({ suggestions: [{ target: 'Hola', native: 'Hello' }], reengagementSeconds: 90,
  chatSummary: summary, globalProfile: profile, artifact: null, toolRequest: null });
const stream = (response: string) => ({ async *[Symbol.asyncIterator]() { yield { text: response }; } });
const makeTextClient = (response = 'Hola.\n[EN] Hello.') => ({
  models: {
    generateContent: vi.fn(async () => ({ text: response })),
    generateContentStream: vi.fn(async () => stream(response)),
  },
  live: { connect: vi.fn(), music: { connect: vi.fn() } },
});
const captureError = new Error('contract capture completed');
const makeLiveClient = () => ({ ...makeTextClient(), live: { connect: vi.fn(async (_request: any) => { throw captureError; }), music: { connect: vi.fn() } } });
const runtime = () => createCoreRuntime({ clock: { now: () => 1_000, sleep: async () => {}, setInterval: () => 0, clearInterval() {} } });
const makeHeadlessClient = (ai: any, pairId = 'es-ES-en-US') => {
  const state = createEmptyHeadlessProfileState();
  state.chats[pairId] = structuredClone(history);
  state.globalProfile = profile;
  const events = createCoreEventJournal();
  return { ai, state, events, accessMode: 'byok', runtime: createCoreRuntime({ ...runtime(), events }), save: vi.fn(), files: { statuses: async (uris: string[]) =>
    Object.fromEntries(uris.map(uri => [uri, { active: true, deleted: false }])) } } as unknown as HeadlessClient;
};

afterEach(() => vi.restoreAllMocks());

describe('Maestro model input contracts', () => {
  it('preserves every shipped central prompt, including whitespace', () => {
    // Explicit names: adding another export must not silently rewrite this baseline.
    expect({
      tutor: prompts.DEFAULT_SYSTEM_PROMPT_CONTENT,
      voice: prompts.VOICE_TAG_PERSONA_GUIDELINES,
      suggestions: prompts.DEFAULT_REPLY_SUGGESTIONS_PROMPT_CONTENT,
      imageContext: prompts.DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
      imageSystem: prompts.IMAGE_GEN_SYSTEM_INSTRUCTION,
      imageUser: prompts.IMAGE_GEN_USER_PROMPT_TEMPLATE,
      imageRetry: prompts.IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION,
      artReference: ART_STYLE_REFERENCE_TEXT,
    }).toMatchSnapshot();
  });

  for (const [target, native] of [['es-ES', 'en-US'], ['ja-JP', 'fi-FI']]) {
    it(`preserves browser text request parts and configuration (${target}/${native})`, async () => {
      const pair = pairFor(target, native);
      const ai = makeTextClient();
      await generateGeminiResponse('gemini-3.6-flash', text, deriveHistoryForApi(history, {
        globalProfileText: profile, contextSummary: summary, artStyleReferenceText: ART_STYLE_REFERENCE_TEXT,
        avatarOverlayFileUri: 'files/avatar', avatarOverlayMimeType: 'image/jpeg',
      }), { aiClient: ai, systemInstruction: prompts.composeMaestroSystemInstruction(pair.baseSystemPrompt),
        currentFileParts: [{ fileUri: 'files/current-document', mimeType: 'application/pdf' }], useGoogleSearch: true });
      expect(ai.models.generateContentStream.mock.calls).toMatchSnapshot();
    });
  }

  for (const responseSource of ['chat', 'live'] as const) {
    for (const withHistory of [false, true]) {
      it(`preserves ${responseSource} suggestions request and schema (history=${withHistory})`, async () => {
        const ai = makeTextClient(suggestionResponse);
        await runReplySuggestions({ assistantMessageId: 'a1', lastTutorMessage: 'Hola.\n[EN] Hello.',
          history: withHistory ? history : [], languagePair: pairFor('es-ES', 'en-US'),
          existingGlobalProfile: withHistory ? profile : undefined, responseSource }, { aiClient: ai });
        expect(ai.models.generateContentStream.mock.calls).toMatchSnapshot();
      });
    }
  }

  it('preserves translation at the provider boundary', async () => {
    const ai = makeTextClient();
    await translateText(text, 'Finnish', 'Japanese', { aiClient: ai });
    expect(ai.models.generateContent.mock.calls).toMatchSnapshot();
  });

  it('preserves browser live context without silently adding text-chat persona rules', async () => {
    expect(await buildLiveSystemInstruction({ basePrompt: pairFor('es-ES', 'en-US').baseSystemPrompt,
      messages: history, computeHistorySubsetForMedia: messages => messages, resolveBookmarkContextSummary: () => summary,
    })).toMatchSnapshot();
  });

  for (const mode of ['stt', 'conversation', 'observer'] as const) {
    it(`preserves the actual headless ${mode} connection instruction and configuration`, async () => {
      const ai = makeLiveClient();
      await expect(runHeadlessLiveTurn(makeHeadlessClient(ai), { mode, languagePairId: 'es-ES-en-US',
        pcm: new Int16Array(32_000).fill(6_000), pace: false, runSuggestionAftersteps: false,
      })).rejects.toThrow('contract capture completed');
      expect(ai.live.connect).toHaveBeenCalledOnce();
      const { model, config } = ai.live.connect.mock.calls[0][0];
      expect({ model, config }).toMatchSnapshot();
    });
  }

  it('preserves the actual headless text and re-engagement provider requests', async () => {
    const ai = makeTextClient();
    const client = makeHeadlessClient(ai);
    await runHeadlessChatTurn(client, { text, languagePairId: 'es-ES-en-US', useGoogleSearch: false });
    await runHeadlessReengagement(client, { languagePairId: 'es-ES-en-US', runSuggestionAftersteps: false });
    expect(ai.models.generateContentStream.mock.calls).toMatchSnapshot();
  });

  it('preserves STT context trimming and the no-context instruction', () => {
    expect([
      buildLiveSttSystemInstruction(),
      buildLiveSttSystemInstruction({ lastAssistantMessage: '  Hola.\n[EN] Hello.  ', replySuggestions: [' Gracias ', '', 'Sí'] }),
    ]).toMatchSnapshot();
  });

  it('preserves exact TTS language tags, blank lines, and Play rules', () => {
    expect(buildTriggeredTtsSystemInstruction([{ text, langCode: 'fi-FI' }, { text: 'こんにちは', langCode: 'ja-JP' }, { text: 'No tag' }])).toMatchSnapshot();
  });

  for (const langCode of [undefined, 'fi-FI']) {
    it(`preserves the audio-note connection (language=${langCode})`, async () => {
      const ai = makeLiveClient();
      await expect(runCoreAudioNoteGeneration({ aiClient: ai, runtime: runtime(), liveOpenTrigger: LIVE_OPEN_TRIGGER.TOOL_AUDIO_NOTE,
        model: 'gemini-live-test', text: `  ${text}  `, langCode, triggerPcmBase64: 'AAAAAA==', triggerSampleRate: 24_000,
      })).rejects.toThrow('contract capture completed');
      const { model, config } = ai.live.connect.mock.calls[0][0];
      expect({ model, config }).toMatchSnapshot();
    });
  }

  it('preserves the diagnostic fallback instruction independently of normal STT', async () => {
    const ai = makeLiveClient();
    await expect(runSyntheticLiveJourney(ai, { liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm: new Int16Array([1, 2]), sampleRate: 16_000, pace: false }), gateInputOnSpeech: false,
    }, { runtime: runtime() })).rejects.toThrow('contract capture completed');
    const { model, config } = ai.live.connect.mock.calls[0][0];
    expect({ model, config }).toMatchSnapshot();
  });

  it('preserves image request history, role order, avatar, noise and retry instruction', async () => {
    vi.spyOn(Date, 'now').mockReturnValue(1_000);
    vi.spyOn(Math, 'random').mockReturnValue(0.5);
    const ai = makeTextClient();
    ai.models.generateContent.mockResolvedValueOnce({ candidates: [] } as any).mockResolvedValueOnce({
      candidates: [{ content: { parts: [{ inlineData: { mimeType: 'image/png', data: 'AAAA' } }] } }],
    } as any);
    await runMaestroImageGeneration({ contextText: text, maxAttempts: 2, maestroAvatarUri: 'files/avatar', maestroAvatarMimeType: 'image/png',
      history: [{ role: 'assistant', text: 'Earlier', chatSummary: summary, fileParts: [{ fileUri: 'files/old', mimeType: 'image/png' }] },
        ...[1, 2, 3].map(index => ({ role: 'user', text: `Scene ${index}`, fileParts: [{ fileUri: `files/image-${index}`, mimeType: 'image/png' }] })),
        { role: 'user', text: 'Listen', fileParts: [{ fileUri: 'files/audio', mimeType: 'audio/wav' }] },
        { role: 'user', text: prompts.DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE }],
    }, { aiClient: ai, runtime: runtime() });
    expect(ai.models.generateContent.mock.calls).toMatchSnapshot();
  });

  it('preserves compact artifact/tool history wording and truncation', () => {
    expect([
      buildCompactAssistantHistoryText(history[2]),
      buildCompactAssistantHistoryText({ ...history[2], imageUrl: `data:image/svg+xml;base64,${Buffer.from(artifact.repeat(50)).toString('base64')}` }),
      buildCompactAssistantHistoryText({ ...history[2], text: 'x'.repeat(3_000), translations: [{ target: 'x'.repeat(3_000), native: 'y' }] }),
    ]).toMatchSnapshot();
  });
});
