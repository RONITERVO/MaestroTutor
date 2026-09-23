// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

// Only authored framing lives here; history selection, truncation limits, and data stay with their owners.
export const REENGAGEMENT_PROMPT = '...';
export const PROMPT_CONTEXT_TEXT = {
  user: 'User',
  tutor: 'Tutor',
  maestro: 'Maestro',
  assistantAttachment: '(assistant attachment)',
  image: '(image)',
  sentImage: '(sent an image)',
  noHistory: 'No history yet.',
  noProfile: '(none)',
  omittedImage: ' [Previous image context omitted]',
  compactTruncation: '\n... [truncated for compact history]',
  compactArtifactUnavailable: '[compact history artifact preview unavailable]',
  compactArtifact: '[Earlier assistant turn artifact preview; compact history only]',
  compactTool: '[Earlier assistant turn used this tool; compact history only]',
} as const;

export const formatLearnerProfileContext = (text: string): string =>
  `Learner Profile (global):\n${text}\nEND OF GLOBAL PROFILE MEMORY.`;
export const formatConversationSummary = (text: string): string => `Conversation Summary:\n${text}`;
export const formatImageConversationSummary = (text: string): string => `[Conversation Summary from earlier context]\n${text}`;
export const formatOmittedMediaContext = (type: string): string => ` [${type} context omitted]`;
export const appendLiveHistoryContext = (base: string, history: string): string => history
  ? `${base}\n\n--- CURRENT CONVERSATION CONTEXT (History) ---\n${history}\n--- END CONTEXT ---`
  : base;
