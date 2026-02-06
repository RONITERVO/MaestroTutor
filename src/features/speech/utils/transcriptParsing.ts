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

/** Matches language codes like [sv-SE], [fi-FI], [en-US] */
const LANGUAGE_CODE_PATTERN = /\[([a-z]{2}-[A-Z]{2})\]/g;

/** Matches emotion tags like [laughing], [excited] */
const EMOTION_TAG_PATTERN = /\[(laughing|chuckles|excited|happy|thoughtful|curious|sad|angry|surprised|nervous|confident|whispers|sighs)\]/gi;

/**
 * Normalizes transcript for splitting: language codes become newlines.
 */
export function normalizeTranscriptForSplitting(transcript: string): string {
  return transcript
    .replace(/\r\n/g, '\n')
    .replace(LANGUAGE_CODE_PATTERN, '\n')
    .replace(/\n+/g, '\n');
}

/**
 * Counts newlines in normalized transcript.
 */
export function countTranscriptNewlines(transcript: string): number {
  const normalized = normalizeTranscriptForSplitting(transcript);
  return (normalized.match(/\n/g) || []).length;
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
 * Each newline in the transcript created that segment's split point.
 * So we must use ALL transcript lines (including empty) for indexing,
 * then only cache segments that have matching content.
 * 
 * @param audioSegmentCount - Number of audio segments we actually have
 * @returns Array where result[audioSegmentIndex] = originalLineIndex (or -1 if no reliable match)
 */
export function mapAudioSegmentsToTextLines(
  originalTexts: string[],
  transcript: string,
  audioSegmentCount: number
): number[] {
  const normalized = normalizeTranscriptForSplitting(transcript);
  // Use ALL lines, not filtered - segment i corresponds to transcript line i
  const allTranscriptLines = normalized.split('\n').map(l => l.trim());
  
  console.debug(`[Mapping] ${audioSegmentCount} segments, ${allTranscriptLines.length} transcript lines, ${originalTexts.length} originals`);
  
  const result: number[] = [];
  const usedOriginals = new Set<number>();
  
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
  
  return result;
}

/**
 * Extracts content lines from transcript (lines with actual spoken text).
 */
export function extractContentLines(transcript: string): string[] {
  const normalized = normalizeTranscriptForSplitting(transcript);
  return normalized.split('\n').map(l => l.trim()).filter(hasContent);
}
