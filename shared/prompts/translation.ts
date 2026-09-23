// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export const buildTranslationPrompt = (text: string, from: string, to: string): string => `Translate the following text from ${from} to ${to}. Return ONLY the translation. Text: "${text}"`;
