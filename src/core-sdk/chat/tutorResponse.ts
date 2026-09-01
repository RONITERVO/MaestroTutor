// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getShortLangCodeForPrompt } from '../../shared/utils/languageUtils';
import { parseAssistantResponseForAttachment } from './assistantResponseAttachments';

export interface StrictParsedTutorResponse {
  translations: Array<{ target: string; native: string }>;
  visibleText: string;
  hasSkippedNonLanguageContent: boolean;
}

const TUTOR_FENCE_OPEN_REGEX = /^(\s{0,3})(`{3,}|~{3,})([^\n]*)$/;
const MARKUP_LINE_TAG_REGEX = /<\/?[a-z][\w:-]*(?:\s+[^<>]*)?\/?>/i;
const MARKUP_DECLARATION_OR_COMMENT_REGEX = /<!--|-->|^<!doctype\b|^<!\[CDATA\[|^\]\]>$|^<\?xml\b|^\?>$/i;
const MARKUP_ATTRIBUTE_ONLY_LINE_REGEX = /^(?:[a-z_:][\w:.-]*\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'>/]+)\s*)+\/?>?$/i;
const MARKUP_STYLE_DECLARATION_LINE_REGEX = /^[a-z-]+\s*:\s*[^;]+;?$/i;
const MARKUP_STYLE_HINT_REGEX = /(?:#(?:[0-9a-f]{3}){1,2}\b|rgb[a]?\(|hsl[a]?\(|url\(|\b(?:px|em|rem|vh|vw|deg|ms|s)\b|font|fill|stroke|color|width|height|margin|padding|display|position|background|transform|animation)/i;
const MARKUP_VOID_TAGS = new Set([
  'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta',
  'param', 'source', 'track', 'wbr',
]);

const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const isMatchingTutorFenceClose = (
  rawLine: string,
  activeFence: { char: '`' | '~'; length: number },
): boolean => {
  const trimmed = rawLine.trim();
  if (!trimmed || trimmed[0] !== activeFence.char) return false;
  let count = 0;
  while (count < trimmed.length && trimmed[count] === activeFence.char) count++;
  return count >= activeFence.length && trimmed.slice(count).trim().length === 0;
};

const getMarkupBlockFromLine = (trimmedLine: string): { tag: string; inOpeningTag: boolean } | null => {
  const openMatch = /^<([a-z][\w:-]*)\b/i.exec(trimmedLine);
  if (!openMatch) return null;
  const tagName = openMatch[1].toLowerCase();
  if (MARKUP_VOID_TAGS.has(tagName)) return null;
  if (/\/>\s*$/.test(trimmedLine)) return null;
  if (new RegExp(`<\\/${escapeRegExp(tagName)}\\s*>`, 'i').test(trimmedLine)) return null;
  return { tag: tagName, inOpeningTag: !trimmedLine.includes('>') };
};

const stripTutorVisibleLines = (responseText: string): {
  lines: string[];
  hasSkippedNonLanguageContent: boolean;
} => {
  const normalizedLines = responseText.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n');
  const lines: string[] = [];
  let hasSkippedNonLanguageContent = false;
  let activeFence: { char: '`' | '~'; length: number } | null = null;
  let activeMarkupBlock: { tag: string; inOpeningTag: boolean } | null = null;
  let activeMarkupComment = false;

  for (const rawLine of normalizedLines) {
    if (activeFence) {
      hasSkippedNonLanguageContent = true;
      if (isMatchingTutorFenceClose(rawLine, activeFence)) activeFence = null;
      continue;
    }
    const trimmed = rawLine.trim();
    if (activeMarkupComment) {
      hasSkippedNonLanguageContent = true;
      if (trimmed.includes('-->')) activeMarkupComment = false;
      continue;
    }
    if (activeMarkupBlock) {
      hasSkippedNonLanguageContent = true;
      if (activeMarkupBlock.inOpeningTag && /\/>\s*$/.test(trimmed)) {
        activeMarkupBlock = null;
        continue;
      }
      if (activeMarkupBlock?.inOpeningTag && trimmed.includes('>')) activeMarkupBlock.inOpeningTag = false;
      if (activeMarkupBlock && new RegExp(`<\\/${escapeRegExp(activeMarkupBlock.tag)}\\s*>`, 'i').test(trimmed)) {
        activeMarkupBlock = null;
      }
      continue;
    }
    const openMatch = TUTOR_FENCE_OPEN_REGEX.exec(rawLine);
    if (openMatch) {
      activeFence = { char: openMatch[2][0] as '`' | '~', length: openMatch[2].length };
      hasSkippedNonLanguageContent = true;
      continue;
    }
    if (trimmed.includes('<!--')) {
      activeMarkupComment = !trimmed.includes('-->');
      hasSkippedNonLanguageContent = true;
      continue;
    }
    const markupBlock = getMarkupBlockFromLine(trimmed);
    if (markupBlock) {
      activeMarkupBlock = markupBlock;
      hasSkippedNonLanguageContent = true;
      continue;
    }
    if (trimmed) lines.push(trimmed);
  }
  if (activeFence || activeMarkupBlock || activeMarkupComment) hasSkippedNonLanguageContent = true;
  return { lines, hasSkippedNonLanguageContent };
};

const extractNativeTutorText = (line: string, nativeLangPrefix: string): string | null => {
  const trimmed = line.trim();
  if (!trimmed || !trimmed.toLowerCase().startsWith(nativeLangPrefix.toLowerCase())) return null;
  return trimmed.slice(nativeLangPrefix.length).trim();
};

const isLikelyArtifactLeakTutorLine = (line: string): boolean => {
  const trimmed = line.trim();
  if (!trimmed) return false;
  if (/^(?:`{3,}|~{3,})/.test(trimmed)) return true;
  if (MARKUP_DECLARATION_OR_COMMENT_REGEX.test(trimmed)) return true;
  if (MARKUP_LINE_TAG_REGEX.test(trimmed)) return true;
  if (MARKUP_ATTRIBUTE_ONLY_LINE_REGEX.test(trimmed)) return true;
  if (MARKUP_STYLE_DECLARATION_LINE_REGEX.test(trimmed) && MARKUP_STYLE_HINT_REGEX.test(trimmed)) return true;
  if (/^<\/?(?:html|head|body|main|section|article|div|svg|canvas|script|style|button|table|form)\b/i.test(trimmed)) return true;
  if (/^[{}\[\](),.;:]+$/.test(trimmed)) return true;
  if (/^(?:const|let|var|function|class|import|export|return|if|for|while|switch|document\.|window\.)\b/.test(trimmed)) return true;
  return false;
};

const filterTutorLanguageCandidateLines = (lines: string[]): {
  lines: string[];
  hasSkippedNonLanguageContent: boolean;
} => {
  const languageLines: string[] = [];
  let hasSkippedNonLanguageContent = false;
  for (const line of lines) {
    if (isLikelyArtifactLeakTutorLine(line)) {
      hasSkippedNonLanguageContent = true;
      continue;
    }
    languageLines.push(line);
  }
  return { lines: languageLines, hasSkippedNonLanguageContent };
};

export const parseStrictTutorResponseText = (
  responseText: string | undefined,
  nativeLanguageCode: string | undefined,
): StrictParsedTutorResponse => {
  if (typeof responseText !== 'string' || !responseText.trim() || !nativeLanguageCode) {
    return { translations: [], visibleText: '', hasSkippedNonLanguageContent: false };
  }
  const nativeLangPrefix = `[${getShortLangCodeForPrompt(nativeLanguageCode)}]`;
  const artifactParsed = parseAssistantResponseForAttachment(responseText);
  const textWithoutArtifact = artifactParsed.attachment ? artifactParsed.cleanedText : responseText;
  const stripped = stripTutorVisibleLines(textWithoutArtifact);
  const candidates = filterTutorLanguageCandidateLines(stripped.lines);
  const translations: Array<{ target: string; native: string }> = [];
  const visibleLines: string[] = [];
  let hasSkippedNonLanguageContent = stripped.hasSkippedNonLanguageContent
    || candidates.hasSkippedNonLanguageContent
    || Boolean(artifactParsed.attachment);

  for (let i = 0; i < candidates.lines.length; i++) {
    const currentLine = candidates.lines[i];
    if (!currentLine) continue;
    const orphanNative = extractNativeTutorText(currentLine, nativeLangPrefix);
    if (orphanNative !== null) {
      hasSkippedNonLanguageContent = true;
      continue;
    }
    const nextLine = candidates.lines[i + 1] || '';
    const taggedNative = extractNativeTutorText(nextLine, nativeLangPrefix);
    const nativeText = taggedNative !== null ? taggedNative : nextLine.trim();
    if (!nativeText || isLikelyArtifactLeakTutorLine(nextLine) || isLikelyArtifactLeakTutorLine(nativeText)) {
      hasSkippedNonLanguageContent = true;
      continue;
    }
    translations.push({ target: currentLine, native: nativeText });
    visibleLines.push(currentLine, `${nativeLangPrefix} ${nativeText}`);
    i++;
  }
  return { translations, visibleText: visibleLines.join('\n').trim(), hasSkippedNonLanguageContent };
};

export const formatStreamingTutorDraftText = (
  responseText: string | undefined,
  nativeLanguageCode: string | undefined,
): string => {
  if (typeof responseText !== 'string' || !responseText.trim() || !nativeLanguageCode) return '';
  const nativeLangPrefix = `[${getShortLangCodeForPrompt(nativeLanguageCode)}]`;
  const artifactParsed = parseAssistantResponseForAttachment(responseText);
  const textWithoutArtifact = artifactParsed.attachment ? artifactParsed.cleanedText : responseText;
  const stripped = stripTutorVisibleLines(textWithoutArtifact);
  const candidates = filterTutorLanguageCandidateLines(stripped.lines);
  const visibleLines: string[] = [];
  for (let i = 0; i < candidates.lines.length; i++) {
    const currentLine = candidates.lines[i];
    if (!currentLine || extractNativeTutorText(currentLine, nativeLangPrefix) !== null) continue;
    const nextLine = candidates.lines[i + 1] || '';
    const taggedNative = extractNativeTutorText(nextLine, nativeLangPrefix);
    const nativeText = taggedNative !== null ? taggedNative : nextLine.trim();
    if (nativeText && !isLikelyArtifactLeakTutorLine(nextLine) && !isLikelyArtifactLeakTutorLine(nativeText)) {
      visibleLines.push(currentLine, `${nativeLangPrefix} ${nativeText}`);
      i++;
    } else if (!nextLine) {
      visibleLines.push(currentLine);
    }
  }
  return visibleLines.join('\n').trim();
};
