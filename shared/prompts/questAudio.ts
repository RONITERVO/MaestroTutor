// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Shared welcome/help wording. This requirement never verifies an audio route. */
export const QUEST_AUDIO_SETUP = {
  title: 'Headphones for sound',
  requirement: 'Headphones are required for the full Quest v1 audio experience.',
  setup: 'Connect headphones to your Quest before starting audio. You can still read and type without them.',
  detection: 'Maestro cannot currently check whether headphones are connected.',
} as const;

export const QUEST_AUDIO_GUIDE = [
  QUEST_AUDIO_SETUP.title,
  QUEST_AUDIO_SETUP.requirement,
  QUEST_AUDIO_SETUP.setup,
  QUEST_AUDIO_SETUP.detection,
  'After the app loses focus, use the book’s Resume audio bell when needed, then start speech again. Returning to the book does not automatically restart audio.',
  'During a Live conversation, voice input is paused while Maestro’s native voice plays and briefly afterward. Headphones do not change that behavior.',
  'Reading this guide does not start audio, open the microphone, grant permissions or change settings.',
].join('\n\n');

export const QUEST_AUDIO_CONTEXT = [
  'Quest v1 audio setup:',
  QUEST_AUDIO_SETUP.requirement,
  QUEST_AUDIO_SETUP.setup,
  QUEST_AUDIO_SETUP.detection,
  'Treat headphone use as guidance, never a detected connection or a prerequisite for reading, typing or editing the room. Do not claim headphones remove native speech input suppression, prove echo cancellation or qualify built-in speakers. Explain this when relevant to audio setup; do not repeat it in unrelated language practice.',
].join(' ');

/** Runtime identity must come from the host, never room capabilities or user text. */
export const withQuestAudioContext = (basePrompt: string, nativeQuest: boolean): string =>
  nativeQuest && basePrompt.length > 0 && !basePrompt.includes(QUEST_AUDIO_CONTEXT) ? basePrompt + '\n\n' + QUEST_AUDIO_CONTEXT : basePrompt;
