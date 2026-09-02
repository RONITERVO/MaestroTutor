// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import type { ChatMessage } from '../core/types';
import { summarizeLiveMessageForHeadlessOutput } from './liveJourney';

describe('headless Live result output', () => {
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
