// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { LanguageDefinition, DEFAULT_TARGET_LANG_CODE, DEFAULT_NATIVE_LANG_CODE, ALL_LANGUAGES } from '../../core/config/languages';
import { DEFAULT_SYSTEM_PROMPT_CONTENT, DEFAULT_REPLY_SUGGESTIONS_PROMPT_CONTENT } from '../../core/config/prompts';
import { LanguagePair } from '../../core/types';

export const getPrimaryCode = (codes: string): string => (codes || "").split(',')[0].trim();

export const getPrimarySubtag = (langCode: string): string =>
  getPrimaryCode(langCode).split('-')[0].toLowerCase();

export const getShortLangCodeForPrompt = (commaSeparatedCodes: string): string => {
    return getPrimarySubtag(commaSeparatedCodes).toUpperCase();
}

export const findLanguageByExactCode = (langCode: string): LanguageDefinition | undefined => {
  if (!langCode) return undefined;
  const normalized = langCode.toLowerCase();
  return ALL_LANGUAGES.find(lang => lang.langCode.toLowerCase() === normalized);
};

export const findLanguageByPrimarySubtag = (langCode: string): LanguageDefinition | undefined => {
  if (!langCode) return undefined;
  const primary = getPrimarySubtag(langCode);
  return ALL_LANGUAGES.find(lang => getPrimarySubtag(lang.langCode) === primary);
};

interface PromptTemplateFillData {
  targetLanguageName: string;
  nativeLanguageName: string;
  nativeLanguageCode: string;
  needsRomanization: boolean;
}

const ROMANIZATION_INSTRUCTION_TEXT = `    *   After each {TARGET_LANGUAGE_NAME} sentence, add a \`[ROM] <romanization>\` line (Latin-script pronunciation) before the translation.
`;

const SUGGESTIONS_ROMANIZATION_INSTRUCTION_TEXT = `    *   Also include \`"romanization"\` (Latin-script pronunciation) for each \`target\` string.
`;

const SUGGESTIONS_ROMANIZATION_EXAMPLE_LINE = `    { "target": "こんにちは", "romanization": "Konnichiwa", "native": "Hello" }
`;

const DEFAULT_SUGGESTIONS_EXAMPLE_LINE = '    { "target": "mas n", "native": "more n" }\n';

const LANGUAGE_CODE_SET = new Set(ALL_LANGUAGES.map(lang => lang.langCode));

export const parseLanguagePairId = (pairId: string): { targetCode: string; nativeCode: string } | null => {
  if (!pairId) return null;
  const trimmed = pairId.trim();
  if (!trimmed) return null;
  const tokens = trimmed.split('-');
  if (tokens.length < 2) return null;
  for (let i = 1; i < tokens.length; i += 1) {
    const targetCode = tokens.slice(0, i).join('-');
    const nativeCode = tokens.slice(i).join('-');
    if (LANGUAGE_CODE_SET.has(targetCode) && LANGUAGE_CODE_SET.has(nativeCode)) {
      return { targetCode, nativeCode };
    }
  }
  return null;
};

export const fillPromptTemplateForPair = (template: string, pairData: PromptTemplateFillData): string => {
    if (!pairData) return template;
    const romanizationInstruction = pairData.needsRomanization ? ROMANIZATION_INSTRUCTION_TEXT : '';
    const suggestionsRomanizationInstruction = pairData.needsRomanization ? SUGGESTIONS_ROMANIZATION_INSTRUCTION_TEXT : '';
    const suggestionsRomanizationExampleLine = pairData.needsRomanization
      ? SUGGESTIONS_ROMANIZATION_EXAMPLE_LINE
      : DEFAULT_SUGGESTIONS_EXAMPLE_LINE;
    return template
        .replace(/{ROMANIZATION_INSTRUCTION}/g, romanizationInstruction)
        .replace(/{SUGGESTIONS_ROMANIZATION_INSTRUCTION}/g, suggestionsRomanizationInstruction)
        .replace(/{SUGGESTIONS_ROMANIZATION_EXAMPLE_LINE}/g, suggestionsRomanizationExampleLine)
        .replace(/{TARGET_LANGUAGE_NAME}/g, pairData.targetLanguageName)
        .replace(/{NATIVE_LANGUAGE_NAME}/g, pairData.nativeLanguageName)
        .replace(/{NATIVE_LANGUAGE_CODE_SHORT}/g, getShortLangCodeForPrompt(pairData.nativeLanguageCode));
};

export const createLanguagePairObject = (
  targetDef: LanguageDefinition,
  nativeDef: LanguageDefinition,
  isDefault = false
): LanguagePair => {
  const pairId = `${targetDef.langCode}-${nativeDef.langCode}`;
  const pairName = `${targetDef.displayName} (for ${nativeDef.displayName} speakers)`;

  const promptFillData: PromptTemplateFillData = {
    targetLanguageName: targetDef.displayName,
    nativeLanguageName: nativeDef.displayName,
    nativeLanguageCode: nativeDef.code,
    needsRomanization: !!targetDef.needsRomanization,
  };
  return {
    id: pairId, name: pairName,
    targetLanguageName: targetDef.displayName,
    targetLanguageCode: targetDef.code,
    nativeLanguageName: nativeDef.displayName,
    nativeLanguageCode: nativeDef.code,
    isDefault,
    baseSystemPrompt: fillPromptTemplateForPair(DEFAULT_SYSTEM_PROMPT_CONTENT, promptFillData),
    baseReplySuggestionsPrompt: fillPromptTemplateForPair(DEFAULT_REPLY_SUGGESTIONS_PROMPT_CONTENT, promptFillData),
  };
};

export const generateAllLanguagePairs = (): LanguagePair[] => {
  const pairs: LanguagePair[] = [];
  ALL_LANGUAGES.forEach(targetDef => {
    ALL_LANGUAGES.forEach(nativeDef => {
      if (targetDef.langCode !== nativeDef.langCode) {
        const isDefaultPair = targetDef.langCode === DEFAULT_TARGET_LANG_CODE && nativeDef.langCode === DEFAULT_NATIVE_LANG_CODE;
        pairs.push(createLanguagePairObject(targetDef, nativeDef, isDefaultPair));
      }
    });
  });
  return pairs;
};
