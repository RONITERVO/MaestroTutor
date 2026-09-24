// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export const truncateForToolPrompt = (value: string, maxChars: number = 420): string => {
  const normalized = (value || '').replace(/\s+/g, ' ').trim();
  if (!normalized) return '';
  if (normalized.length <= maxChars) return normalized;
  let end = Math.max(0, maxChars - 1);
  const lastCodeUnit = normalized.charCodeAt(end - 1);
  if (lastCodeUnit >= 0xD800 && lastCodeUnit <= 0xDBFF) end--;
  return `${normalized.slice(0, end)}…`;
};
