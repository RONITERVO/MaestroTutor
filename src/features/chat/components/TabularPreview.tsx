// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import type { TabularChartSeries } from '../utils/tabularPreview';

interface TabularPreviewProps {
  rows: string[][];
  chartSeries?: TabularChartSeries | null;
  textColorClass: string;
  subtleTextClass: string;
  compact?: boolean;
}

const SAMPLE_ROW_LIMIT_COMPACT = 4;
const SAMPLE_COL_LIMIT_COMPACT = 4;
const SAMPLE_ROW_LIMIT_FULL = 8;
const SAMPLE_COL_LIMIT_FULL = 8;

const buildLinePath = (series: TabularChartSeries, width: number, height: number): string => {
  if (!series.values.length) return '';

  const min = Math.min(...series.values);
  const max = Math.max(...series.values);
  const range = max - min || 1;
  const leftPad = 8;
  const rightPad = 8;
  const topPad = 8;
  const bottomPad = 8;
  const drawableW = Math.max(1, width - leftPad - rightPad);
  const drawableH = Math.max(1, height - topPad - bottomPad);
  const points = series.values.map((value, index) => {
    const x = leftPad + (series.values.length === 1 ? drawableW / 2 : (index / (series.values.length - 1)) * drawableW);
    const y = topPad + (1 - (value - min) / range) * drawableH;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });

  return `M ${points.join(' L ')}`;
};

const TabularPreview: React.FC<TabularPreviewProps> = ({
  rows,
  chartSeries,
  textColorClass,
  subtleTextClass,
  compact = false,
}) => {
  const maxRows = compact ? SAMPLE_ROW_LIMIT_COMPACT : SAMPLE_ROW_LIMIT_FULL;
  const maxCols = compact ? SAMPLE_COL_LIMIT_COMPACT : SAMPLE_COL_LIMIT_FULL;
  const chartHeight = compact ? 76 : 120;
  const chartWidth = compact ? 220 : 360;

  const sampleRows = useMemo(
    () => rows.slice(0, maxRows).map((row) => row.slice(0, maxCols)),
    [rows, maxRows, maxCols]
  );

  const path = useMemo(
    () => (chartSeries && chartSeries.values.length >= 2 ? buildLinePath(chartSeries, chartWidth, chartHeight) : ''),
    [chartSeries, chartWidth, chartHeight]
  );
  const pointCoords = useMemo(() => {
    if (!path) return [];
    return path
      .replace(/^M\s*/, '')
      .split(' L ')
      .map((point) => {
        const [x, y] = point.split(',');
        return { x, y };
      });
  }, [path]);

  const minMax = useMemo(() => {
    if (!chartSeries || chartSeries.values.length === 0) return null;
    return {
      min: Math.min(...chartSeries.values),
      max: Math.max(...chartSeries.values),
    };
  }, [chartSeries]);

  return (
    <div className="mt-2 space-y-2">
      {chartSeries && chartSeries.values.length >= 2 && path && (
        <div className="rounded border border-black/10 bg-black/5 p-2">
          <div className={`flex items-center justify-between text-[10px] mb-1 ${subtleTextClass}`}>
            <span>{chartSeries.label || 'Chart preview'}</span>
            {minMax && (
              <span>
                min {minMax.min.toLocaleString()} / max {minMax.max.toLocaleString()}
              </span>
            )}
          </div>
          <svg
            viewBox={`0 0 ${chartWidth} ${chartHeight}`}
            className="w-full h-auto block"
            role="img"
            aria-label="Chart preview"
          >
            <line x1="8" y1={chartHeight - 8} x2={chartWidth - 8} y2={chartHeight - 8} stroke="currentColor" strokeOpacity="0.2" />
            <line x1="8" y1="8" x2="8" y2={chartHeight - 8} stroke="currentColor" strokeOpacity="0.2" />
            <path d={path} fill="none" stroke="currentColor" strokeWidth="2.2" className={textColorClass} />
            {chartSeries.values.map((_, i) => {
              const point = pointCoords[i];
              if (!point) return null;
              return <circle key={i} cx={point.x} cy={point.y} r="2.2" className={textColorClass} fill="currentColor" />;
            })}
          </svg>
          <div className={`mt-1 flex justify-between text-[10px] ${subtleTextClass}`}>
            <span className="truncate max-w-[48%]">{chartSeries.labels[0] || 'start'}</span>
            <span className="truncate max-w-[48%] text-right">
              {chartSeries.labels[chartSeries.labels.length - 1] || 'end'}
            </span>
          </div>
        </div>
      )}

      {sampleRows.length > 0 && (
        <div className="rounded border border-black/10 bg-black/5 overflow-auto">
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
    </div>
  );
};

export default TabularPreview;
