// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { GoogleGenAI } from '@google/genai';
import { getGeminiModels } from '../src/core/config/models';
import { createManagedGeminiClient } from '../src/core-sdk/managedGeminiClient';
import { decodePcm16LeBase64, createSyntheticPcmSource } from '../src/core-sdk/media/pcmInput';
import { runSyntheticLiveJourney } from '../src/core-sdk/media/syntheticLiveJourney';
import { TRIGGER_AUDIO_PCM_24K } from '../src/core-sdk/media/triggerAudioAsset';
import { LIVE_OPEN_TRIGGER } from '../shared/liveOpenReason';

const apiKey = (process.env.MAESTRO_GEMINI_API_KEY || '').trim();
if (!apiKey) throw new Error('MAESTRO_GEMINI_API_KEY is required.');
const apiVersion = (process.env.MAESTRO_LIVE_API_VERSION || 'v1alpha').trim();
const liveModel = (process.env.MAESTRO_LIVE_MODEL || getGeminiModels().audio.stt).trim();
const timeoutMs = Number(process.env.MAESTRO_LIVE_TIMEOUT_MS || 90_000);
if (!Number.isFinite(timeoutMs) || timeoutMs < 5_000) {
  throw new Error('MAESTRO_LIVE_TIMEOUT_MS must be at least 5000.');
}
const tokenLifetimeSeconds = Number(process.env.MAESTRO_LIVE_TOKEN_LIFETIME_SECONDS || 180);
const newSessionWindowSeconds = Number(process.env.MAESTRO_LIVE_NEW_SESSION_WINDOW_SECONDS || 60);
const thinkingMode = process.env.MAESTRO_LIVE_THINKING_MODE === 'conversation'
  ? 'conversation'
  : 'minimal';
const voiceName = process.env.MAESTRO_LIVE_VOICE_NAME?.trim() || undefined;
const authMode = (process.env.MAESTRO_LIVE_AUTH_MODE || 'ephemeral').trim();
if (authMode !== 'ephemeral' && authMode !== 'api-key') {
  throw new Error('MAESTRO_LIVE_AUTH_MODE must be ephemeral or api-key.');
}
const constraintMode = (process.env.MAESTRO_EPHEMERAL_CONSTRAINT_MODE || 'full').trim();
if (!['full', 'no-thinking', 'no-system', 'transcription', 'basic', 'model-mask', 'model', 'none'].includes(constraintMode)) {
  throw new Error('Unsupported MAESTRO_EPHEMERAL_CONSTRAINT_MODE.');
}
const omitClientConfig = process.env.MAESTRO_EPHEMERAL_OMIT_CLIENT_CONFIG === 'true';

const source = decodePcm16LeBase64(TRIGGER_AUDIO_PCM_24K);
const speech = new Int16Array(Math.floor(source.length * 2 / 3));
for (let index = 0; index < speech.length; index += 1) {
  speech[index] = source[Math.min(source.length - 1, Math.floor(index * 3 / 2))];
}
const gapSamples = 1_600;
const pcm = new Int16Array((speech.length * 3) + (gapSamples * 2) + 12_000);
pcm.set(speech, 0);
pcm.set(speech, speech.length + gapSamples);
pcm.set(speech, (speech.length * 2) + (gapSamples * 2));

const provisioningClient = new GoogleGenAI({ apiKey });
const ephemeralAi = createManagedGeminiClient({
  generateContent: async () => { throw new Error('Not used by the Live token probe.'); },
  generateContentStream: async () => { throw new Error('Not used by the Live token probe.'); },
  createLiveGatewayTicket: async () => { throw new Error('Legacy diagnostic does not use the managed gateway.'); },
  createLiveToken: async ({ model, config }) => {
    const expireTime = new Date(Date.now() + tokenLifetimeSeconds * 1_000).toISOString();
    const newSessionExpireTime = new Date(Date.now() + newSessionWindowSeconds * 1_000).toISOString();
    const selectedConfig = (() => {
      if (!config || constraintMode === 'model-mask' || constraintMode === 'model' || constraintMode === 'none') return undefined;
      if (constraintMode === 'full') return config;
      const {
        responseModalities,
        inputAudioTranscription,
        outputAudioTranscription,
        thinkingConfig,
        speechConfig,
        systemInstruction,
      } = config;
      if (constraintMode === 'basic') return { responseModalities };
      if (constraintMode === 'transcription') {
        return { responseModalities, inputAudioTranscription, outputAudioTranscription };
      }
      if (constraintMode === 'no-thinking') {
        return { responseModalities, inputAudioTranscription, outputAudioTranscription, speechConfig, systemInstruction };
      }
      return { responseModalities, inputAudioTranscription, outputAudioTranscription, thinkingConfig, speechConfig };
    })();
    const liveConnectConstraints = constraintMode === 'none'
      ? undefined
      : { model, ...(selectedConfig ? { config: selectedConfig } : {}) };
    const token = await provisioningClient.authTokens.create({
      config: {
        uses: 1,
        expireTime,
        newSessionExpireTime,
        httpOptions: { apiVersion },
        ...(liveConnectConstraints ? { liveConnectConstraints } : {}),
        ...(constraintMode === 'model-mask' ? { lockAdditionalFields: [] } : {}),
      },
    } as any);
    if (!token.name) throw new Error('Gemini returned no ephemeral token name.');
    return {
      leaseId: `diagnostic-${Date.now()}`,
      token: token.name,
      expiresAt: token.expireTime || expireTime,
      uses: token.uses || 1,
    };
  },
  releaseLiveTokenLease: async () => ({ ok: true }),
}, {
  // Deliberately isolated to this provider-capability diagnostic. Product
  // managed mode fails closed onto the server-metered gateway.
  transport: 'legacy-ephemeral',
  apiVersion,
  ...(omitClientConfig ? {
    createTokenClient: (token, version) => {
      const tokenClient = new GoogleGenAI({ apiKey: token, apiVersion: version });
      return {
        models: tokenClient.models as any,
        live: {
          connect: request => {
            const { config: _lockedInToken, ...withoutConfig } = request;
            return tokenClient.live.connect(withoutConfig as any);
          },
          music: tokenClient.live.music as any,
        },
      };
    },
  } : {}),
});
const ai = authMode === 'ephemeral'
  ? ephemeralAi
  : new GoogleGenAI({ apiKey, apiVersion }) as any;

const result = await runSyntheticLiveJourney(ai, {
  liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
  source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: true }),
  model: liveModel,
  systemInstruction: 'Repeat the learner speech briefly and clearly.',
  thinkingMode,
  voiceName,
  gateInputOnSpeech: true,
  semanticSpeech: true,
  simulateUiSpeechHandoff: true,
  requireRealtimeInputPacing: true,
  playModelAudioRealtime: true,
  timeoutMs,
});

const heardPlay = /\bplay\b/i.test(result.inputTranscript);
const passed = heardPlay
  && result.modelAudioSampleCount > 0
  && result.realtimeEvidence.passed;

console.log(JSON.stringify({
  passed,
  authMode,
  apiVersion,
  tokenLifetimeSeconds,
  newSessionWindowSeconds,
  thinkingMode,
  voiceName: voiceName || null,
  constraintMode,
  omitClientConfig,
  model: liveModel,
  heardPlay,
  inputTranscript: result.inputTranscript,
  outputTranscriptLength: result.outputTranscript.length,
  modelAudioSampleCount: result.modelAudioSampleCount,
  connectionHandoffSamples: result.timing.connectionHandoffSamples,
  inputCaptureElapsedMs: result.timing.inputCaptureElapsedMs,
  modelPlaybackElapsedMs: result.timing.modelPlaybackElapsedMs,
  realtimeEvidence: result.realtimeEvidence,
}, null, 2));

if (!passed) process.exitCode = 1;
