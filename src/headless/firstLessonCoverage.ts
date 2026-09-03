// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export const FIRST_LESSON_ATTACHMENT_KINDS = [
  'text', 'image', 'audio', 'pdf', 'svg', 'video', 'office',
] as const;

export const FIRST_LESSON_TOOL_KINDS = ['image', 'audio-note', 'music'] as const;

type Evidence = Record<string, any>;

export interface FirstLessonCoverageEvidence {
  accessMode: 'managed' | 'byok';
  userTurnCount: number;
  requireToolUploads: boolean;
  turns: Evidence[];
  aftersteps: Evidence[];
  stt: Evidence;
  liveAudio: Evidence;
  liveVisual: Evidence;
  observerAudio: Evidence;
  observerVisual: Evidence;
  translation: Evidence;
  tts: Evidence;
  reengagement: Evidence;
  managedBillingEvidence: Evidence;
}

const hasTranscriptStream = (result: Evidence): boolean => (
  Number(result.inputTranscriptDeltaCount) > 0
  && Number(result.outputTranscriptDeltaCount) > 0
  && Boolean(result.inputTranscript)
  && Boolean(result.outputTranscript)
  && result.transcriptEvidence?.passed === true
  && Number(result.gate?.streamEnds) === 1
);

const hasCapturedAudio = (result: Evidence): boolean => (
  Number(result.capturedInputSamples) > 0
  && typeof result.capturedInputSha256 === 'string'
  && result.capturedInputSha256.length === 64
  && Number(result.capturedModelSamples) > 0
  && typeof result.capturedModelSha256 === 'string'
  && result.capturedModelSha256.length === 64
);

/** Converts provider/runtime evidence into the release-gate requirements. */
export const evaluateFirstLessonCoverage = (evidence: FirstLessonCoverageEvidence) => {
  const chatTurns = evidence.turns.filter(turn => turn.streaming != null);
  const requiredToolKinds = new Set(evidence.aftersteps.map(item => item.tool).filter(Boolean));
  return {
    tenUserTurns: evidence.userTurnCount >= 10,
    text: evidence.turns.some(turn => turn.kind === 'text'),
    googleSearch: evidence.turns.some(
      turn => turn.kind === 'google-search' && Number(turn.searchQueryCount) > 0,
    ),
    attachments: FIRST_LESSON_ATTACHMENT_KINDS.every(
      kind => evidence.turns.some(turn => (
        turn.kind === `attachment-${kind}`
        && turn.cleanedUp === true
        && Number(turn.cleanupFailureCount) === 0
      )),
    ),
    chatStreaming: chatTurns.length > 0
      && chatTurns.every(turn => turn.streaming?.visiblyStreamed === true),
    stt: Number(evidence.stt.inputTranscriptDeltaCount) > 0
      && Boolean(evidence.stt.inputTranscript)
      && Number(evidence.stt.capturedInputSamples) > 0
      && evidence.stt.transcriptEvidence?.passed === true
      && Number(evidence.stt.gate?.streamEnds) === 1,
    liveAudio: hasTranscriptStream(evidence.liveAudio) && hasCapturedAudio(evidence.liveAudio),
    liveVisual: Number(evidence.liveVisual.sentVideoFrameCount) > 0
      && hasTranscriptStream(evidence.liveVisual)
      && hasCapturedAudio(evidence.liveVisual),
    observerAudio: evidence.observerAudio.gate?.enabled === true
      && Number(evidence.observerAudio.sentVideoFrameCount) === 0
      && hasTranscriptStream(evidence.observerAudio)
      && hasCapturedAudio(evidence.observerAudio),
    observerVisual: evidence.observerVisual.gate?.enabled === true
      && Number(evidence.observerVisual.sentVideoFrameCount) > 0
      && hasTranscriptStream(evidence.observerVisual)
      && hasCapturedAudio(evidence.observerVisual),
    suggestionAftersteps: evidence.aftersteps.length > 0
      && evidence.aftersteps.every(
        item => Number(item.suggestionCount) > 0 && item.visiblyStreamed === true,
      ),
    tools: FIRST_LESSON_TOOL_KINDS.every(tool => requiredToolKinds.has(tool)),
    toolUploads: !evidence.requireToolUploads || FIRST_LESSON_TOOL_KINDS.every(tool => (
      evidence.aftersteps.some(item => item.tool === tool && item.uploaded === true)
    )),
    translation: Boolean(evidence.translation.translatedText),
    ttsTrigger: Number(evidence.tts.triggerAudioSamplesSent) > 0
      && Number(evidence.tts.triggerPacketCount) > 0
      && Number(evidence.tts.sampleCount) > 0
      && evidence.tts.purpose === 'tts'
      && typeof evidence.tts.dataSha256 === 'string'
      && evidence.tts.dataSha256.length === 64,
    audioCapture: [
      evidence.liveAudio,
      evidence.liveVisual,
      evidence.observerAudio,
      evidence.observerVisual,
    ].every(hasCapturedAudio),
    reengagement: evidence.reengagement.emptyUserRequest === true
      && evidence.reengagement.userMessagePersisted === false
      && evidence.reengagement.turn?.streaming?.visiblyStreamed === true,
    costAccounting: evidence.accessMode === 'byok'
      ? evidence.managedBillingEvidence.applicable === false
        && evidence.managedBillingEvidence.payer === 'byok-api-key-owner'
      : evidence.managedBillingEvidence.applicable === true
        && evidence.managedBillingEvidence.passed === true
        && Number(evidence.managedBillingEvidence.creditsSpent) > 0
        && Number(evidence.managedBillingEvidence.reservedCreditsAfter) === 0,
  };
};
