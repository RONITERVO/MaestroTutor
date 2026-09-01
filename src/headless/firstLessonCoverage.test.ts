// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  evaluateFirstLessonCoverage,
  FIRST_LESSON_ATTACHMENT_KINDS,
} from './firstLessonCoverage';

const hash = 'a'.repeat(64);
const live = (videoFrames: number, observer = false) => ({
  inputTranscript: 'Play',
  outputTranscript: 'Hola',
  inputTranscriptDeltaCount: 1,
  outputTranscriptDeltaCount: 2,
  sentVideoFrameCount: videoFrames,
  capturedInputSamples: 16_000,
  capturedInputSha256: hash,
  capturedModelSamples: 24_000,
  capturedModelSha256: hash,
  gate: { enabled: observer },
});

const validEvidence = () => ({
  userTurnCount: 14,
  turns: [
    { kind: 'text', streaming: { visiblyStreamed: true } },
    { kind: 'google-search', searchQueryCount: 1, streaming: { visiblyStreamed: true } },
    ...FIRST_LESSON_ATTACHMENT_KINDS.map(kind => ({
      kind: `attachment-${kind}`,
      streaming: { visiblyStreamed: true },
      cleanedUp: true,
      cleanupFailureCount: 0,
    })),
  ],
  aftersteps: ['image', 'audio-note', 'music'].map(tool => ({
    tool,
    suggestionCount: 3,
    visiblyStreamed: true,
  })),
  stt: { inputTranscript: 'Play', inputTranscriptDeltaCount: 1, capturedInputSamples: 16_000 },
  liveAudio: live(0),
  liveVisual: live(1),
  observerAudio: live(0, true),
  observerVisual: live(1, true),
  translation: { translatedText: 'Gracias' },
  tts: {
    triggerAudioSamplesSent: 100,
    triggerPacketCount: 1,
    sampleCount: 200,
    purpose: 'tts',
    dataSha256: hash,
  },
  reengagement: {
    emptyUserRequest: true,
    userMessagePersisted: false,
    turn: { streaming: { visiblyStreamed: true } },
  },
});

describe('first lesson coverage gate', () => {
  it('requires every requested path and accepts complete runtime evidence', () => {
    expect(Object.values(evaluateFirstLessonCoverage(validEvidence())).every(Boolean)).toBe(true);
  });

  it('rejects indirect evidence for streaming, transcripts, advanced attachments and captured audio', () => {
    const evidence = validEvidence();
    evidence.turns = evidence.turns.filter(turn => turn.kind !== 'attachment-office');
    evidence.aftersteps[0].visiblyStreamed = false;
    evidence.liveVisual.outputTranscriptDeltaCount = 0;
    evidence.observerAudio.capturedModelSha256 = '';
    expect(evaluateFirstLessonCoverage(evidence)).toMatchObject({
      attachments: false,
      suggestionAftersteps: false,
      liveVisual: false,
      observerAudio: false,
      audioCapture: false,
    });
  });

  it('requires visible streaming evidence from the empty-input re-engagement turn', () => {
    const evidence = validEvidence();
    evidence.reengagement.turn.streaming.visiblyStreamed = false;
    expect(evaluateFirstLessonCoverage(evidence).reengagement).toBe(false);
  });
});
