// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

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
