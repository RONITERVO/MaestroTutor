// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface TabularChartSeries {
  labels: string[];
  values: number[];
  label?: string;
  sourceColumnIndex?: number;
}

export interface TabularSheetPreview {
  name: string;
  rows: string[][];
  chartSeriesList: TabularChartSeries[];
}

const JSON_CHART_EXTENSIONS = new Set(['json', 'json5', 'jsonc']);

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

const tryParseJson = (text: string): any | null => {
  const normalized = (text || '').trim();
  if (!normalized) return null;

  try {
    return JSON.parse(normalized);
  } catch {
    return null;
  }
};

const getChartJsonRoot = (input: any): any => {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return input;

  const nestedData = input.data;
  if (!nestedData || typeof nestedData !== 'object' || Array.isArray(nestedData)) {
    return input;
  }

  const hasChartShape =
    Array.isArray(nestedData.labels) ||
    Array.isArray(nestedData.datasets) ||
    Array.isArray(nestedData.series) ||
    Array.isArray(nestedData.values) ||
    Array.isArray(nestedData.data);

  return hasChartShape ? nestedData : input;
};

const normalizeChartJsonNumber = (value: unknown): number | null => {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }
  if (typeof value === 'string') {
    return normalizeNumericCell(value);
  }
  return null;
};

const buildRowsFromSeriesList = (labels: string[], seriesList: TabularChartSeries[]): string[][] => {
  if (!seriesList.length) return [];

  const maxLen = Math.max(
    labels.length,
    ...seriesList.map((series) => series.values.length)
  );
  if (maxLen < 1) return [];

  const header = ['label', ...seriesList.map((series, index) => series.label || `Series ${index + 1}`)];
  const rows: string[][] = [header];

  for (let i = 0; i < maxLen; i++) {
    const row = [labels[i] || String(i + 1)];
    for (const series of seriesList) {
      const value = series.values[i];
      row.push(typeof value === 'number' && Number.isFinite(value) ? String(value) : '');
    }
    rows.push(row);
  }

  return rows;
};

export const deriveChartSheetFromJsonText = (
  text: string,
  name = 'Chart data'
): TabularSheetPreview | null => {
  const parsed = tryParseJson(text);
  const root = getChartJsonRoot(parsed);
  if (!root || typeof root !== 'object' || Array.isArray(root)) return null;

  const labels = Array.isArray(root.labels)
    ? root.labels.map((value: unknown, index: number) => {
        const normalized = String(value ?? '').trim();
        return normalized || String(index + 1);
      })
    : [];

  const datasetList = Array.isArray(root.datasets)
    ? root.datasets
    : (Array.isArray(root.series) ? root.series : null);

  if (Array.isArray(datasetList)) {
    const seriesList = datasetList
      .map((dataset: any, index: number): TabularChartSeries | null => {
        const rawData = Array.isArray(dataset?.data) ? dataset.data : [];
        if (!rawData.length) return null;

        const normalizedValues: number[] = [];
        const normalizedLabels: string[] = [];
        for (let valueIndex = 0; valueIndex < rawData.length; valueIndex++) {
          const numericValue = normalizeChartJsonNumber(rawData[valueIndex]);
          if (numericValue === null) continue;
          normalizedValues.push(numericValue);
          normalizedLabels.push(labels[valueIndex] || String(valueIndex + 1));
        }

        if (!normalizedValues.length) return null;

        return {
          labels: normalizedLabels,
          values: normalizedValues,
          label: String(dataset?.label || dataset?.name || `Series ${index + 1}`).trim() || `Series ${index + 1}`,
          sourceColumnIndex: index + 1,
        };
      })
      .filter(Boolean) as TabularChartSeries[];

    if (!seriesList.length) return null;

    return {
      name,
      rows: buildRowsFromSeriesList(labels, seriesList),
      chartSeriesList: seriesList,
    };
  }

  const flatValues = Array.isArray(root.values)
    ? root.values
    : (Array.isArray(root.data) ? root.data : null);

  if (Array.isArray(flatValues)) {
    const normalizedValues: number[] = [];
    const normalizedLabels: string[] = [];

    for (let i = 0; i < flatValues.length; i++) {
      const numericValue = normalizeChartJsonNumber(flatValues[i]);
      if (numericValue === null) continue;
      normalizedValues.push(numericValue);
      normalizedLabels.push(labels[i] || String(i + 1));
    }

    if (!normalizedValues.length) return null;

    const seriesList: TabularChartSeries[] = [{
      labels: normalizedLabels,
      values: normalizedValues,
      label: String(root.label || 'Value').trim() || 'Value',
      sourceColumnIndex: 1,
    }];

    return {
      name,
      rows: buildRowsFromSeriesList(labels, seriesList),
      chartSeriesList: seriesList,
    };
  }

  return null;
};

export const isJsonChartFile = (mimeType?: string | null, fileName?: string | null): boolean => {
  const normalizedMime = (mimeType || '').trim().toLowerCase();
  if (normalizedMime.includes('json')) return true;

  const normalizedName = (fileName || '').trim().toLowerCase();
  const dotIndex = normalizedName.lastIndexOf('.');
  if (dotIndex < 0 || dotIndex >= normalizedName.length - 1) return false;

  return JSON_CHART_EXTENSIONS.has(normalizedName.slice(dotIndex + 1));
};

const deriveBestLabelColumn = (rows: string[][], dataStart: number, numericColumns: number[]): number => {
  const numericSet = new Set(numericColumns);
  const maxCols = rows.reduce((max, row) => Math.max(max, row.length), 0);
  let labelCol = -1;
  let labelScore = 0;

  for (let c = 0; c < maxCols; c++) {
    if (numericSet.has(c)) continue;
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

  return labelCol;
};

export const deriveChartSeriesListFromRows = (rows: string[][]): TabularChartSeries[] => {
  if (!rows || rows.length < 2) return [];

  const maxCols = rows.reduce((max, row) => Math.max(max, row.length), 0);
  if (maxCols === 0) return [];

  const numericColumns: Array<{ columnIndex: number; numericCount: number }> = [];

  for (let c = 0; c < maxCols; c++) {
    let count = 0;
    for (let r = 0; r < rows.length; r++) {
      const value = rows[r][c] || '';
      if (normalizeNumericCell(value) !== null) count++;
    }
    if (count >= 2) numericColumns.push({ columnIndex: c, numericCount: count });
  }

  if (numericColumns.length === 0) return [];
  numericColumns.sort((a, b) => b.numericCount - a.numericCount);

  const firstNumeric = normalizeNumericCell(rows[0][numericColumns[0].columnIndex] || '');
  const headerLikely = firstNumeric === null;
  const dataStart = headerLikely ? 1 : 0;

  const labelCol = deriveBestLabelColumn(
    rows,
    dataStart,
    numericColumns.map((c) => c.columnIndex)
  );

  const seriesList: TabularChartSeries[] = [];
  const maxSeries = 8;

  for (let seriesIndex = 0; seriesIndex < numericColumns.length && seriesList.length < maxSeries; seriesIndex++) {
    const targetColumn = numericColumns[seriesIndex].columnIndex;
    const labels: string[] = [];
    const values: number[] = [];

    for (let r = dataStart; r < rows.length && values.length < 50; r++) {
      const numeric = normalizeNumericCell(rows[r][targetColumn] || '');
      if (numeric === null) continue;
      const labelRaw = labelCol >= 0 ? (rows[r][labelCol] || '').trim() : '';
      labels.push(labelRaw || `${values.length + 1}`);
      values.push(numeric);
    }

    if (values.length < 2) continue;

    const label = headerLikely
      ? ((rows[0][targetColumn] || '').trim() || undefined)
      : undefined;

    seriesList.push({
      labels,
      values,
      label,
      sourceColumnIndex: targetColumn,
    });
  }

  return seriesList;
};

export const deriveChartSeriesFromRows = (rows: string[][]): TabularChartSeries | null => {
  const list = deriveChartSeriesListFromRows(rows);
  return list.length > 0 ? list[0] : null;
};
