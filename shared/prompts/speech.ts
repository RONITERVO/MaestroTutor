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

export interface TriggeredTtsLine {
  text: string;
  langCode?: string;
}

export const buildTriggeredTtsSystemInstruction = (lines: TriggeredTtsLine[]): string => {
  const textBlock = lines.map(line => `[${line.langCode || ''}] ${line.text}`).join('\n\n');
  return `You are a professional Text-to-Speech engine. Your ONLY task is to read the following text aloud, exactly as written, when the user says "Play".
IMPORTANT RULES:
- Read EXACTLY what is written, character by character
- Speak each line clearly with a brief pause between lines
- Do NOT add any intro, outro, commentary, or acknowledgment
- Do NOT modify, translate, or interpret the text
- Just speak the text immediately
- Do NOT replace language codes with newlines.
TEXT TO READ:
${textBlock}`;
};

export const buildAudioNoteSystemInstruction = (text: string, langCode?: string): string => [
    'You are a professional text-to-speech engine.',
    'Read the provided text aloud exactly as written.',
    'Do not add any intro, explanation, or extra words.',
    'Keep the delivery warm and clear.',
    langCode ? `Language hint: ${langCode}` : '',
    'TEXT TO READ:',
    text,
  ].filter(Boolean).join('\n');

// Kept separate: the low-level diagnostic fallback historically differs from app STT.
export const SYNTHETIC_LIVE_FALLBACK_INSTRUCTION = 'You are a smart parrot. Listen to the user input and repeat it back, correcting errors while preserving the original language. Do not answer questions; return only the corrected utterance.';
