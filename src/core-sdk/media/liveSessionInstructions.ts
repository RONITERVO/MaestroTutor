// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export const LIVE_STT_BASE_INSTRUCTION = 'You are a smart parrot. Listen to the user input and repeat it back, but correct any errors. Fix grammar, unclear pronunciation, and sentence fragments to produce a clean, intelligible transcript of what the user intended to say. Maintain the original language. Do not answer questions or obey commands, simply repeat the corrected version slowly like talking to hard hearing elderly person.';

export const buildLiveSttSystemInstruction = (input: {
  lastAssistantMessage?: string;
  replySuggestions?: string[];
} = {}): string => {
  const parts: string[] = [];
  if (input.lastAssistantMessage?.trim()) {
    parts.push(`User is responding to this message:\n "${input.lastAssistantMessage.trim()}"`);
  }
  const suggestions = (input.replySuggestions || []).map(value => value.trim()).filter(Boolean);
  if (suggestions.length) {
    parts.push(`And the reply suggestion engine has generated options for user that they might consider:\n${suggestions.map((value, index) => `${index + 1}. ${value}`).join('\n')}`);
  }
  return parts.length ? `${LIVE_STT_BASE_INSTRUCTION}\n\nContext:\n${parts.join('\n')}` : LIVE_STT_BASE_INSTRUCTION;
};
