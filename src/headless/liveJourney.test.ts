// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import type { ChatMessage } from '../core/types';
import { runHeadlessLiveTurn, summarizeLiveMessageForHeadlessOutput } from './liveJourney';
import { createEmptyHeadlessProfileState } from './profile';
import { createCoreRuntime } from '../core-sdk/runtime';
import type { HeadlessClient } from './client';

describe('headless Live result output', () => {
  it('opens once per turn with rebuilt persisted chat context and no camera frames', async () => {
    const sessions: Array<{ close: ReturnType<typeof vi.fn> }> = [];
    const connect = vi.fn(async (params: any) => {
      const turn = sessions.length + 1;
      const session = {
        close: vi.fn(),
        sendRealtimeInput: vi.fn((message: any) => {
          if (message.audioStreamEnd) params.callbacks.onmessage({ serverContent: {
            inputTranscription: { text: `Question ${turn}` },
            outputTranscription: { text: `Answer ${turn}` },
            turnComplete: true,
          } });
        }),
      };
      sessions.push(session);
      return session;
    });
    const client = {
      accessMode: 'byok', state: createEmptyHeadlessProfileState(), save: vi.fn(),
      runtime: createCoreRuntime({ clock: { now: Date.now, sleep: async () => {}, setInterval: () => 0, clearInterval: () => {} } }),
      ai: { live: { connect } },
    } as unknown as HeadlessClient;
    const input = { mode: 'conversation' as const, pcm: new Int16Array(16_000).fill(6_000),
      languagePairId: 'en-US-fi-FI', pace: false, runSuggestionAftersteps: false, includeVisual: false };
    const first = await runHeadlessLiveTurn(client, input);
    const second = await runHeadlessLiveTurn(client, input);
    expect(connect).toHaveBeenCalledTimes(2);
    expect(connect.mock.calls[1][0].config.systemInstruction).toContain('Answer 1');
    for (const call of connect.mock.calls) expect(call[0].config.sessionResumption).toBeUndefined();
    for (const session of sessions) expect(session.close).toHaveBeenCalledOnce();
    expect(first.contextEvidence.historyMessageCount).toBe(0);
    expect(second.contextEvidence.historyMessageCount).toBe(2);
    expect(second.contextEvidence.systemInstructionSha256).not.toBe(first.contextEvidence.systemInstructionSha256);
    expect(second.sentVideoFrameCount).toBe(0);
    expect(client.state.chats['en-US-fi-FI']).toHaveLength(4);
  });
  it('omits persisted inline media while retaining useful message metadata', () => {
    const result = summarizeLiveMessageForHeadlessOutput({
      id: 'assistant-1',
      role: 'assistant',
      timestamp: 1,
      text: 'Hello',
      imageUrl: 'data:image/jpeg;base64,AAAA',
      recordedUtterance: {
        dataUrl: 'data:audio/wav;base64,BBBB',
        provider: 'gemini',
        langCode: 'en-US',
        transcript: 'Hello',
        sampleRate: 16_000,
      },
      ttsAudioCache: [{
        key: 'live:test',
        langCode: 'en-US',
        provider: 'gemini-live',
        audioDataUrl: 'data:audio/wav;base64,CCCC',
        updatedAt: 1,
      }],
    } as ChatMessage);

    expect(result.message).not.toHaveProperty('imageUrl');
    expect(result.message.recordedUtterance).not.toHaveProperty('dataUrl');
    expect((result.message.ttsAudioCache as any[])[0]).not.toHaveProperty('audioDataUrl');
    expect(result.omittedInlineData).toEqual({
      imageDataUrlCharacters: 27,
      recordedUtteranceDataUrlCharacters: 26,
      'ttsAudioCache.0.audioDataUrlCharacters': 26,
    });
  });
});
