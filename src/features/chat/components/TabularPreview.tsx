// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo, useState } from 'react';
import type { TabularChartSeries, TabularSheetPreview } from '../utils/tabularPreview';

interface TabularPreviewProps {
  sheets: TabularSheetPreview[];
  textColorClass: string;
  subtleTextClass: string;
  compact?: boolean;
}

const SAMPLE_ROW_LIMIT_COMPACT = 4;
const SAMPLE_COL_LIMIT_COMPACT = 4;
const SAMPLE_ROW_LIMIT_FULL = 24;
const SAMPLE_COL_LIMIT_FULL = 16;
const MAX_CHARTS_FULL = 12;

interface Point {
  x: number;
  y: number;
}

const computeLinePoints = (series: TabularChartSeries, width: number, height: number): Point[] => {
  if (!series.values.length) return [];
  const min = Math.min(...series.values);
  const max = Math.max(...series.values);
  const range = max - min || 1;
  const leftPad = 8;
  const rightPad = 8;
  const topPad = 8;
  const bottomPad = 8;
  const drawableW = Math.max(1, width - leftPad - rightPad);
  const drawableH = Math.max(1, height - topPad - bottomPad);

  return series.values.map((value, index) => {
    const x = leftPad + (series.values.length === 1 ? drawableW / 2 : (index / (series.values.length - 1)) * drawableW);
    const y = topPad + (1 - (value - min) / range) * drawableH;
    return { x, y };
  });
};

const TabularPreview: React.FC<TabularPreviewProps> = ({
  sheets,
  textColorClass,
  subtleTextClass,
  compact = false,
}) => {
  const [activeSheetIndex, setActiveSheetIndex] = useState(0);

  const maxRows = compact ? SAMPLE_ROW_LIMIT_COMPACT : SAMPLE_ROW_LIMIT_FULL;
  const maxCols = compact ? SAMPLE_COL_LIMIT_COMPACT : SAMPLE_COL_LIMIT_FULL;
  const chartHeight = compact ? 76 : 120;
  const chartWidth = compact ? 220 : 300;

  const normalizedSheets = useMemo(() => (
    (sheets || []).filter((sheet) => Array.isArray(sheet.rows) && sheet.rows.length > 0)
  ), [sheets]);

  const safeIndex = Math.min(Math.max(activeSheetIndex, 0), Math.max(0, normalizedSheets.length - 1));
  const activeSheet = normalizedSheets[safeIndex];

  const chartList = useMemo(() => {
    if (!activeSheet) return [];
    const list = Array.isArray(activeSheet.chartSeriesList) ? activeSheet.chartSeriesList : [];
    if (compact) return list.slice(0, 1);
    return list.slice(0, MAX_CHARTS_FULL);
  }, [activeSheet, compact]);

  const sampleRows = useMemo(
    () => (activeSheet?.rows || []).slice(0, maxRows).map((row) => row.slice(0, maxCols)),
    [activeSheet, maxRows, maxCols]
  );

  if (!activeSheet) return null;

  return (
    <div className="mt-2 space-y-2">
      {!compact && normalizedSheets.length > 1 && (
        <div className="overflow-x-auto pb-1">
          <div className="inline-flex min-w-max gap-1">
            {normalizedSheets.map((sheet, index) => (
              <button
                key={`${sheet.name}-${index}`}
                type="button"
                onClick={() => setActiveSheetIndex(index)}
                className={`px-2 py-1 text-[10px] rounded border ${
                  index === safeIndex
                    ? `border-black/30 ${textColorClass} bg-black/10`
                    : `border-black/10 ${subtleTextClass} bg-black/5`
                }`}
                title={sheet.name}
              >
                {sheet.name || `Sheet ${index + 1}`}
              </button>
            ))}
          </div>
        </div>
      )}

      {chartList.length > 0 && (
        <div className="rounded border border-black/10 bg-black/5 p-2">
          <div className={`flex items-center justify-between text-[10px] mb-1 ${subtleTextClass}`}>
            <span>{compact ? 'Chart preview' : 'Charts'}</span>
            {!compact && <span>{chartList.length} series</span>}
          </div>
          <div className="overflow-x-auto">
            <div className={`flex gap-3 ${compact ? '' : 'min-w-max pr-1'}`}>
              {chartList.map((chart, chartIndex) => {
                const points = computeLinePoints(chart, chartWidth, chartHeight);
                if (points.length < 2) return null;
                const path = `M ${points.map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(' L ')}`;
                const min = Math.min(...chart.values);
                const max = Math.max(...chart.values);

                return (
                  <div
                    key={`${chart.label || 'series'}-${chart.sourceColumnIndex ?? chartIndex}`}
                    className={`rounded border border-black/10 bg-black/5 p-2 ${compact ? 'w-full' : 'w-[18rem] shrink-0'}`}
                  >
                    <div className={`flex items-center justify-between text-[10px] mb-1 ${subtleTextClass}`}>
                      <span className="truncate max-w-[62%]">
                        {chart.label || `Series ${chartIndex + 1}`}
                      </span>
                      <span>min {min.toLocaleString()} / max {max.toLocaleString()}</span>
                    </div>
                    <svg
                      viewBox={`0 0 ${chartWidth} ${chartHeight}`}
                      className="w-full h-auto block"
                      role="img"
                      aria-label={chart.label || `Chart series ${chartIndex + 1}`}
                    >
                      <line x1="8" y1={chartHeight - 8} x2={chartWidth - 8} y2={chartHeight - 8} stroke="currentColor" strokeOpacity="0.2" />
                      <line x1="8" y1="8" x2="8" y2={chartHeight - 8} stroke="currentColor" strokeOpacity="0.2" />
                      <path d={path} fill="none" stroke="currentColor" strokeWidth="2.2" className={textColorClass} />
                      {points.map((point, i) => (
                        <circle key={i} cx={point.x} cy={point.y} r="2.2" className={textColorClass} fill="currentColor" />
                      ))}
                    </svg>
                    <div className={`mt-1 flex justify-between text-[10px] ${subtleTextClass}`}>
                      <span className="truncate max-w-[48%]">{chart.labels[0] || 'start'}</span>
                      <span className="truncate max-w-[48%] text-right">
                        {chart.labels[chart.labels.length - 1] || 'end'}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {sampleRows.length > 0 && (
        <div className="rounded border border-black/10 bg-black/5 overflow-auto max-h-72">
          <table className="min-w-full text-[10px] border-collapse">
            <tbody>
              {sampleRows.map((row, rowIndex) => (
                <tr key={rowIndex} className={rowIndex === 0 ? `${textColorClass}` : `${subtleTextClass}`}>
                  {row.map((cell, cellIndex) => (
                    <td key={cellIndex} className="px-2 py-1 border-b border-black/5 whitespace-nowrap max-w-[12rem] truncate">
                      {cell || '-'}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!compact && activeSheet.rows.length > sampleRows.length && (
        <p className={`text-[10px] ${subtleTextClass}`}>
          Showing {sampleRows.length} of {activeSheet.rows.length} rows in table preview.
        </p>
      )}
    </div>
  );
};

export default TabularPreview;
