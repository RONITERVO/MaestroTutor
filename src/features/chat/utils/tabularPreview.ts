// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface TabularChartSeries {
  labels: string[];
  values: number[];
  label?: string;
}

const parseDelimitedLine = (line: string, delimiter: string): string[] => {
  const out: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const ch = line[i];

    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
      continue;
    }

    if (ch === delimiter && !inQuotes) {
      out.push(current.trim());
      current = '';
      continue;
    }

    current += ch;
  }

  out.push(current.trim());
  return out;
};

export const detectDelimiter = (text: string): string => {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 10);

  if (lines.length === 0) return ',';

  const candidates = [',', '\t', ';', '|'];
  let best = ',';
  let bestScore = -1;

  for (const candidate of candidates) {
    let score = 0;
    for (const line of lines) {
      const cells = parseDelimitedLine(line, candidate);
      if (cells.length > 1) score += cells.length;
    }
    if (score > bestScore) {
      bestScore = score;
      best = candidate;
    }
  }

  return best;
};

export const parseDelimitedText = (text: string, delimiter?: string): string[][] => {
  if (!text) return [];
  const resolvedDelimiter = delimiter || detectDelimiter(text);
  const lines = text
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n')
    .slice(0, 600);

  const rows: string[][] = [];
  for (const line of lines) {
    if (!line.trim()) continue;
    const cells = parseDelimitedLine(line, resolvedDelimiter);
    if (cells.some((cell) => cell.length > 0)) rows.push(cells);
  }
  return rows;
};

const normalizeNumericCell = (value: string): number | null => {
  if (!value) return null;
  let raw = value.trim();
  if (!raw) return null;

  let negative = false;
  if (/^\(.*\)$/.test(raw)) {
    negative = true;
    raw = raw.slice(1, -1);
  }

  const isPercent = raw.endsWith('%');
  raw = raw.replace(/%$/, '');

  // Keep only number-related symbols.
  raw = raw.replace(/[^\d,.\-]/g, '');
  if (!raw) return null;

  const commaCount = (raw.match(/,/g) || []).length;
  const dotCount = (raw.match(/\./g) || []).length;

  if (commaCount > 0 && dotCount > 0) {
    raw = raw.replace(/,/g, '');
  } else if (commaCount > 0 && dotCount === 0) {
    if (commaCount === 1) {
      const commaIndex = raw.indexOf(',');
      const decimals = raw.length - commaIndex - 1;
      if (decimals > 0 && decimals <= 3) {
        raw = raw.replace(',', '.');
      } else {
        raw = raw.replace(/,/g, '');
      }
    } else {
      raw = raw.replace(/,/g, '');
    }
  }

  const n = Number(raw);
  if (!Number.isFinite(n)) return null;

  let out = n;
  if (negative) out = -Math.abs(out);
  if (isPercent) out = out / 100;
  return out;
};

export const deriveChartSeriesFromRows = (rows: string[][]): TabularChartSeries | null => {
  if (!rows || rows.length < 2) return null;

  const maxCols = rows.reduce((max, row) => Math.max(max, row.length), 0);
  if (maxCols === 0) return null;

  let bestNumericCol = -1;
  let bestNumericCount = 0;

  for (let c = 0; c < maxCols; c++) {
    let count = 0;
    for (let r = 0; r < rows.length; r++) {
      const value = rows[r][c] || '';
      if (normalizeNumericCell(value) !== null) count++;
    }
    if (count > bestNumericCount) {
      bestNumericCount = count;
      bestNumericCol = c;
    }
  }

  if (bestNumericCol < 0 || bestNumericCount < 2) return null;

  const firstNumeric = normalizeNumericCell(rows[0][bestNumericCol] || '');
  const headerLikely = firstNumeric === null;
  const dataStart = headerLikely ? 1 : 0;

  let labelCol = -1;
  let labelScore = 0;

  for (let c = 0; c < maxCols; c++) {
    if (c === bestNumericCol) continue;
    let score = 0;
    for (let r = dataStart; r < rows.length; r++) {
      const value = (rows[r][c] || '').trim();
      if (!value) continue;
      if (normalizeNumericCell(value) === null) score++;
    }
    if (score > labelScore) {
      labelScore = score;
      labelCol = c;
    }
  }

  const labels: string[] = [];
  const values: number[] = [];

  for (let r = dataStart; r < rows.length && values.length < 30; r++) {
    const numeric = normalizeNumericCell(rows[r][bestNumericCol] || '');
    if (numeric === null) continue;
    const labelRaw = labelCol >= 0 ? (rows[r][labelCol] || '').trim() : '';
    labels.push(labelRaw || `${values.length + 1}`);
    values.push(numeric);
  }

  if (values.length < 2) return null;

  const label = headerLikely
    ? ((rows[0][bestNumericCol] || '').trim() || undefined)
    : undefined;

  return { labels, values, label };
};
