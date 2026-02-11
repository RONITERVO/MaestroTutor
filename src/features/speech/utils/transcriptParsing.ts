// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

/**
 * Transcript Parsing Utilities (Simplified)
 * 
 * The Gemini Live API uses language codes [sv-SE], [fi-FI] etc. in transcripts.
 * These act as line separators. Audio segments are created by newlines in transcript.
 * 
 * KEY INSIGHT:
 * - Empty lines at the START don't create audio segments (no audio arrived yet)
 * - Empty lines in the MIDDLE do create audio segments (short silence/gap)
 * - So: skip leading empty lines, then map 1:1 with audio segments
 */

/** Matches language codes like [sv-SE], [fi-FI], [en-US], [cmn-CN] */
const LANGUAGE_CODE_PATTERN = /\[([a-z]{2,3}-[A-Z]{2})\]/g;

/** Matches emotion tags like [laughing], [excited] */
const EMOTION_TAG_PATTERN = /\[(laughing|chuckles|excited|happy|thoughtful|curious|sad|angry|surprised|nervous|confident|whispers|sighs)\]/gi;

/**
 * Normalizes transcript for splitting: language codes become newlines.
 * Trims the leading newline that results from the first language code
 * being replaced — that replacement is not a boundary between lines,
 * it's the start of the first line.  Without this trim, N lines produce
 * N newlines (and therefore N split points → N+1 segments) instead of
 * the correct N-1.
 */
export function normalizeTranscriptForSplitting(transcript: string): string {
  return transcript
    .replace(/\r\n/g, '\n')
    .replace(LANGUAGE_CODE_PATTERN, '\n')
    .replace(/\n+/g, '\n')
    .replace(/^\n/, '');
}

/**
 * Counts newlines in normalized transcript.
 */
export function countTranscriptNewlines(transcript: string): number {
  const normalized = normalizeTranscriptForSplitting(transcript);
  return (normalized.match(/\n/g) || []).length;
}

/**
 * Counts segment boundaries using language codes [xx-XX] or [xxx-XX].
 * Each language code marks the start of a new segment.
 * Returns number of boundaries (= language code count - 1).
 *
 * More reliable than newline counting for TTS transcripts where empty
 * entries (language codes without content) collapse into consecutive
 * newlines that get merged by normalizeTranscriptForSplitting.
 */
export function countLanguageCodeSeparators(transcript: string): number {
  const matches = transcript.match(LANGUAGE_CODE_PATTERN);
  return matches ? Math.max(0, matches.length - 1) : 0;
}

/**
 * Splits transcript into segments by language codes.
 * Returns the content after each language code, with newlines replaced by spaces.
 * Falls back to returning the full transcript as a single entry if no codes found.
 */
export function splitTranscriptByLanguageCodes(transcript: string): string[] {
  const parts = transcript.split(/\[[a-z]{2,3}-[A-Z]{2}\]/);
  if (parts.length <= 1) return [transcript.trim()];
  return parts.slice(1).map(p => p.replace(/[\n\r]+/g, ' ').trim());
}

/**
 * Normalizes text for comparison (removes markers, punctuation, lowercases).
 */
export function normalizeTextForComparison(text: string): string {
  return text
    .replace(LANGUAGE_CODE_PATTERN, '')
    .replace(EMOTION_TAG_PATTERN, '')
    .replace(/[^\p{L}\p{N}\s]/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

/**
 * Checks if text has meaningful spoken content (not just markers).
 */
function hasContent(text: string): boolean {
  return normalizeTextForComparison(text).length >= 3;
}

/**
 * Simple text similarity using word overlap.
 */
export function calculateSimilarity(a: string, b: string): number {
  const aNorm = normalizeTextForComparison(a);
  const bNorm = normalizeTextForComparison(b);
  
  if (aNorm === bNorm) return 1;
  if (!aNorm || !bNorm) return 0;
  
  // Substring match (handles truncation)
  if (aNorm.includes(bNorm) || bNorm.includes(aNorm)) {
    const shorter = aNorm.length < bNorm.length ? aNorm : bNorm;
    const longer = aNorm.length >= bNorm.length ? aNorm : bNorm;
    return shorter.length / longer.length;
  }
  
  // Word overlap
  const aWords = new Set(aNorm.split(' ').filter(w => w.length > 2));
  const bWords = new Set(bNorm.split(' ').filter(w => w.length > 2));
  if (aWords.size === 0 || bWords.size === 0) return 0;
  
  let matches = 0;
  for (const word of aWords) if (bWords.has(word)) matches++;
  return (2 * matches) / (aWords.size + bWords.size);
}

/**
 * Maps audio segments to original text lines for caching.
 *
 * KEY INSIGHT: Transcript line i corresponds to audio segment i.
 * Each boundary in the transcript created that segment's split point.
 * So we must use ALL transcript lines (including empty) for indexing,
 * then only cache segments that have matching content.
 *
 * @param audioSegmentCount - Number of audio segments we actually have
 * @param splitByLanguageCodes - When true, split transcript by language codes [xx-XX]
 *   instead of normalized newlines. More reliable for TTS where empty entries
 *   (language codes without content) collapse when using newline normalization.
 * @returns Array where result[audioSegmentIndex] = originalLineIndex (or -1 if no reliable match)
 */
export function mapAudioSegmentsToTextLines(
  originalTexts: string[],
  transcript: string,
  audioSegmentCount: number,
  splitByLanguageCodes: boolean = false
): number[] {
  // Split transcript into lines matching audio segments
  let allTranscriptLines: string[];
  if (splitByLanguageCodes) {
    allTranscriptLines = splitTranscriptByLanguageCodes(transcript);
  } else {
    const normalized = normalizeTranscriptForSplitting(transcript);
    allTranscriptLines = normalized.split('\n').map(l => l.trim());
  }

  console.debug(`[Mapping] ${audioSegmentCount} segments, ${allTranscriptLines.length} transcript lines, ${originalTexts.length} originals`);

  const result: number[] = [];
  const usedOriginals = new Set<number>();

  // Match segments with content to original lines by text similarity
  for (let i = 0; i < audioSegmentCount; i++) {
    // Segment i corresponds to transcript line i
    const transcriptText = allTranscriptLines[i] || '';

    // Skip if transcript line is empty or has no content
    if (!hasContent(transcriptText)) {
      result.push(-1);
      console.debug(`[Mapping] seg${i} → SKIP (empty transcript: "${transcriptText}")`);
      continue;
    }

    let bestMatch = -1;
    let bestScore = 0;

    // Find best matching original line
    for (let j = 0; j < originalTexts.length; j++) {
      if (usedOriginals.has(j)) continue;
      const score = calculateSimilarity(transcriptText, originalTexts[j]);
      if (score > bestScore) {
        bestScore = score;
        bestMatch = j;
      }
    }

    // Accept match only if above threshold (30% word overlap)
    if (bestScore >= 0.3 && bestMatch !== -1) {
      usedOriginals.add(bestMatch);
      result.push(bestMatch);
      console.debug(`[Mapping] seg${i} → line${bestMatch} (${bestScore.toFixed(2)}) "${transcriptText.substring(0, 30)}..."`);
    } else {
      result.push(-1);
      console.debug(`[Mapping] seg${i} → NO MATCH (best=${bestScore.toFixed(2)}) "${transcriptText.substring(0, 30)}..."`);
    }
  }

  // Sequential gap fill: when verified text-similarity matches form a strictly
  // ascending sequence, unmatched segments in gaps can be safely assigned to
  // unmapped lines if the gap has exactly the right count (unambiguous 1:1
  // positional mapping). This handles cases where the transcript contains only
  // markers (language codes, emotion tags) without actual spoken text, but the
  // audio IS correct. Safety: min-length and max-length checks in the caller
  // provide additional protection against caching wrong audio.
  const verifiedMappings = result
    .map((lineIdx, segIdx) => ({ segIdx, lineIdx }))
    .filter(m => m.lineIdx !== -1);

  const isSequential = verifiedMappings.every((m, i) =>
    i === 0 || m.lineIdx > verifiedMappings[i - 1].lineIdx
  );

  if (isSequential && verifiedMappings.length >= 2) {
    const boundaries = [
      { segIdx: -1, lineIdx: -1 },
      ...verifiedMappings,
      { segIdx: audioSegmentCount, lineIdx: originalTexts.length }
    ];

    for (let b = 0; b < boundaries.length - 1; b++) {
      const lower = boundaries[b];
      const upper = boundaries[b + 1];

      // Unmatched segments in this gap
      const gapSegs: number[] = [];
      for (let s = lower.segIdx + 1; s < upper.segIdx; s++) {
        if (result[s] === -1) gapSegs.push(s);
      }

      // Unmapped lines in this gap
      const gapLines: number[] = [];
      for (let l = lower.lineIdx + 1; l < upper.lineIdx; l++) {
        if (!usedOriginals.has(l)) gapLines.push(l);
      }

      // Only fill when counts match exactly (unambiguous positional mapping)
      if (gapSegs.length > 0 && gapSegs.length === gapLines.length) {
        for (let g = 0; g < gapSegs.length; g++) {
          result[gapSegs[g]] = gapLines[g];
          usedOriginals.add(gapLines[g]);
          console.debug(`[Mapping] seg${gapSegs[g]} → line${gapLines[g]} (sequential gap fill)`);
        }
      }
    }
  }

  return result;
}

/**
 * Extracts content lines from transcript (lines with actual spoken text).
 */
export function extractContentLines(transcript: string): string[] {
  const normalized = normalizeTranscriptForSplitting(transcript);
  return normalized.split('\n').map(l => l.trim()).filter(hasContent);
}
