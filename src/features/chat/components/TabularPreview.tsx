// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo, useState } from 'react';
import AttachmentInteractionToggle from './AttachmentInteractionToggle';
import useChatResettingAttachmentMode from './useChatResettingAttachmentMode';
import type { TabularChartSeries, TabularSheetPreview } from '../utils/tabularPreview';

interface TabularPreviewProps {
  sheets: TabularSheetPreview[];
  textColorClass: string;
  subtleTextClass: string;
  compact?: boolean;
  title?: string | null;
  standalone?: boolean;
  bottomInset?: number;
  surfaceClassName?: string;
  panelSurfaceClassName?: string;
}

const SAMPLE_ROW_LIMIT_COMPACT = 4;
const SAMPLE_COL_LIMIT_COMPACT = 4;
const SAMPLE_ROW_LIMIT_FULL = 60;
const SAMPLE_COL_LIMIT_FULL = 24;
const MAX_CHARTS_FULL = 12;

interface Point {
  x: number;
  y: number;
}

interface ChartLayout {
  leftPad: number;
  rightPad: number;
  topPad: number;
  bottomPad: number;
}

const getChartLayout = (compact: boolean): ChartLayout => (
  compact
    ? { leftPad: 34, rightPad: 16, topPad: 28, bottomPad: 28 }
    : { leftPad: 42, rightPad: 22, topPad: 34, bottomPad: 34 }
);

const computeLinePoints = (series: TabularChartSeries, width: number, height: number, layout: ChartLayout): Point[] => {
  if (!series.values.length) return [];
  const min = Math.min(...series.values);
  const max = Math.max(...series.values);
  const range = max - min || 1;
  const drawableW = Math.max(1, width - layout.leftPad - layout.rightPad);
  const drawableH = Math.max(1, height - layout.topPad - layout.bottomPad);

  return series.values.map((value, index) => {
    const x = layout.leftPad + (series.values.length === 1 ? drawableW / 2 : (index / (series.values.length - 1)) * drawableW);
    const y = layout.topPad + (1 - (value - min) / range) * drawableH;
    return { x, y };
  });
};

const hashString = (value: string): number => {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = ((hash << 5) - hash) + value.charCodeAt(i);
    hash |= 0;
  }
  return Math.abs(hash);
};

const jitter = (seed: number, index: number, axis: number, amount: number): number => {
  const wave = Math.sin(seed * 0.017 + index * 1.713 + axis * 2.491);
  const counterWave = Math.cos(seed * 0.011 + index * 0.937 + axis * 4.113);
  return (wave * 0.68 + counterWave * 0.32) * amount;
};

const jitterPoints = (points: Point[], seed: number, amount: number): Point[] => (
  points.map((point, index) => ({
    x: point.x + jitter(seed, index, 0, amount),
    y: point.y + jitter(seed, index, 1, amount),
  }))
);

const buildLinePath = (points: Point[]): string => {
  if (points.length === 0) return '';
  return `M ${points.map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(' L ')}`;
};

const buildClosedAreaPath = (points: Point[], baselineY: number): string => {
  if (points.length < 2) return '';
  const line = buildLinePath(points);
  const first = points[0];
  const last = points[points.length - 1];
  return `${line} L ${last.x.toFixed(2)},${baselineY.toFixed(2)} L ${first.x.toFixed(2)},${baselineY.toFixed(2)} Z`;
};

const buildSketchLinePath = (
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  seed: number,
  bow = 2.5
): string => {
  const midX = (x1 + x2) / 2 + jitter(seed, 0, 0, bow);
  const midY = (y1 + y2) / 2 + jitter(seed, 0, 1, bow);
  return `M ${x1.toFixed(2)},${y1.toFixed(2)} Q ${midX.toFixed(2)},${midY.toFixed(2)} ${x2.toFixed(2)},${y2.toFixed(2)}`;
};

const formatChartNumber = (value: number): string => {
  const abs = Math.abs(value);
  const options: Intl.NumberFormatOptions = abs >= 10_000
    ? { notation: 'compact', maximumFractionDigits: 1 }
    : { maximumFractionDigits: abs < 10 && !Number.isInteger(value) ? 2 : 1 };
  return value.toLocaleString(undefined, options);
};

const truncateChartLabel = (value: string | undefined, maxLength: number): string => {
  const normalized = (value || '').trim();
  if (!normalized) return '';
  if (normalized.length <= maxLength) return normalized;
  return `${normalized.slice(0, Math.max(1, maxLength - 1)).trimEnd()}...`;
};

const TabularPreview: React.FC<TabularPreviewProps> = ({
  sheets,
  textColorClass,
  compact = false,
  title,
  standalone = false,
  bottomInset = 0,
}) => {
  const [activeSheetIndex, setActiveSheetIndex] = useState(0);
  const {
    rootRef,
    isAttachmentModeEnabled: isPanEnabled,
    setIsAttachmentModeEnabled: setIsPanEnabled,
  } = useChatResettingAttachmentMode<HTMLDivElement>();

  const maxRows = compact ? SAMPLE_ROW_LIMIT_COMPACT : SAMPLE_ROW_LIMIT_FULL;
  const maxCols = compact ? SAMPLE_COL_LIMIT_COMPACT : SAMPLE_COL_LIMIT_FULL;
  const chartHeight = compact ? 126 : 176;
  const chartWidth = compact ? 260 : 360;
  const chartLayout = getChartLayout(compact);
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;

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

  const scrollStyle: React.CSSProperties = {
    overscrollBehavior: isPanEnabled ? 'contain' : 'auto',
    touchAction: isPanEnabled ? 'pan-x pan-y' : 'pan-y',
    WebkitOverflowScrolling: 'touch' as any,
  };
  const panEventHandlers = isPanEnabled
    ? {
        onWheel: (event: React.WheelEvent) => event.stopPropagation(),
        onTouchMove: (event: React.TouchEvent) => event.stopPropagation(),
      }
    : undefined;
  const xScrollClass = isPanEnabled ? 'overflow-x-auto overflow-y-hidden' : 'overflow-hidden';
  const tableScrollClass = isPanEnabled ? 'overflow-auto' : 'overflow-hidden';
  const useSingleFullChart = !compact && chartList.length === 1;
  const paperTextClass = 'text-deep-ink';
  const paperSubtleTextClass = 'text-sketch-line';
  const paperSurfaceClassName = 'bg-paper-surface/85';
  const paperPanelSurfaceClassName = 'bg-paper-stripe/35';
  const shellClass = standalone
    ? `mt-2 mx-auto w-full max-w-[560px] overflow-hidden ${textColorClass}`
    : `mt-2 w-full overflow-hidden ${textColorClass}`;
  const contentPaddingStyle = effectiveBottomInset > 0
    ? { paddingBottom: `calc(0.75rem + ${effectiveBottomInset}px)` }
    : undefined;

  return (
    <div ref={rootRef} className={compact ? 'mt-2 space-y-2' : shellClass}>
      {!compact && (
        <div className="relative z-10 flex items-center justify-between gap-3 px-1 py-1.5">
          <div className="min-w-0">
            <p className={`truncate font-architect text-[14px] font-semibold ${paperTextClass}`}>
              {title || activeSheet.name || 'Chart data'}
            </p>
            <p className={`truncate text-[11px] ${paperSubtleTextClass}`}>
              {activeSheet.rows.length.toLocaleString()} rows
              {chartList.length > 0 ? ` / ${chartList.length} chart ${chartList.length === 1 ? 'series' : 'series'}` : ''}
            </p>
          </div>
          <AttachmentInteractionToggle
            isAttachmentModeEnabled={isPanEnabled}
            attachmentLabel="Table pan"
            attachmentTitle="Pan chart and table"
            groupLabel="Artifact interaction mode"
            activeTextClassName={paperTextClass}
            inactiveTextClassName={paperSubtleTextClass}
            activeSurfaceClassName={paperSurfaceClassName}
            inactiveSurfaceClassName={paperPanelSurfaceClassName}
            onToggle={() => setIsPanEnabled((prev) => !prev)}
          />
        </div>
      )}

      <div className={compact ? 'space-y-2' : 'relative z-10 space-y-3 px-1 pb-1 pt-0'} style={contentPaddingStyle}>
        {!compact && normalizedSheets.length > 1 && (
          <div className="overflow-x-auto pb-1" style={scrollStyle} {...panEventHandlers}>
            <div className="inline-flex min-w-max gap-1.5">
              {normalizedSheets.map((sheet, index) => (
                <button
                  key={`${sheet.name}-${index}`}
                  type="button"
                  onClick={() => setActiveSheetIndex(index)}
                  className={`sketch-shape-8 border px-2 py-1 text-[10px] transition-colors ${
                    index === safeIndex
                      ? `border-pencil-stroke/35 ${paperSurfaceClassName} ${paperTextClass}`
                      : `border-sketch-line/30 ${paperPanelSurfaceClassName} ${paperSubtleTextClass}`
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
          <section className={`notebook-chart-paper paper-texture notebook-lines isolate overflow-hidden ${compact ? 'sketch-shape-4 px-2 py-2' : 'sketch-shape-4 px-2 py-2.5'}`}>
            <div className={xScrollClass} style={scrollStyle} {...panEventHandlers}>
              <div className={`flex gap-4 ${compact || useSingleFullChart ? 'w-full' : 'min-w-max pr-1'}`}>
                {chartList.map((chart, chartIndex) => {
                  const points = computeLinePoints(chart, chartWidth, chartHeight, chartLayout);
                  if (points.length < 2) return null;
                  const seed = hashString(`${chart.label || ''}|${chart.sourceColumnIndex ?? chartIndex}|${chart.values.join('|')}`);
                  const primaryPath = buildLinePath(jitterPoints(points, seed, compact ? 0.55 : 0.8));
                  const secondaryPath = buildLinePath(jitterPoints(points, seed + 37, compact ? 0.35 : 0.6));
                  const areaPath = buildClosedAreaPath(points, chartHeight - chartLayout.bottomPad);
                  const min = Math.min(...chart.values);
                  const max = Math.max(...chart.values);
                  const minLabel = formatChartNumber(min);
                  const maxLabel = formatChartNumber(max);
                  const chartLabel = chart.label || title || `Series ${chartIndex + 1}`;
                  const titleLabel = truncateChartLabel(chartLabel, compact ? 22 : 30);
                  const startLabel = truncateChartLabel(chart.labels[0] || 'start', compact ? 14 : 20);
                  const endLabel = truncateChartLabel(chart.labels[chart.labels.length - 1] || 'end', compact ? 14 : 20);
                  const pointStep = points.length > 24 ? Math.ceil(points.length / 24) : 1;
                  const visiblePoints = points
                    .map((point, index) => ({ point, index }))
                    .filter(({ index }) => index === 0 || index === points.length - 1 || index % pointStep === 0);
                  const gridYs = [0.25, 0.5, 0.75].map((ratio) => (
                    chartLayout.topPad + (chartHeight - chartLayout.topPad - chartLayout.bottomPad) * ratio
                  ));

                  return (
                    <figure
                      key={`${chart.label || 'series'}-${chart.sourceColumnIndex ?? chartIndex}`}
                      className={`${compact || useSingleFullChart ? 'w-full' : 'w-[21.5rem] shrink-0'} notebook-chart-figure`}
                    >
                      <svg
                        viewBox={`0 0 ${chartWidth} ${chartHeight}`}
                        className="notebook-chart-svg block h-auto w-full text-pencil-stroke"
                        role="img"
                        aria-label={`${chartLabel}: minimum ${minLabel}, maximum ${maxLabel}`}
                      >
                        <path
                          d={areaPath}
                          className="text-watercolor-wash"
                          fill="currentColor"
                          fillOpacity="0.07"
                        />
                        {gridYs.map((gridY, gridIndex) => (
                          <path
                            key={gridIndex}
                            d={buildSketchLinePath(
                              chartLayout.leftPad,
                              gridY,
                              chartWidth - chartLayout.rightPad,
                              gridY + jitter(seed, gridIndex, 2, 0.7),
                              seed + gridIndex + 11,
                              1.1
                            )}
                            fill="none"
                            stroke="currentColor"
                            strokeOpacity="0.2"
                            strokeWidth="0.9"
                            strokeDasharray="4 7"
                            strokeLinecap="round"
                            className="text-sketch-line"
                          />
                        ))}
                        <path
                          d={buildSketchLinePath(
                            chartLayout.leftPad,
                            chartHeight - chartLayout.bottomPad,
                            chartWidth - chartLayout.rightPad,
                            chartHeight - chartLayout.bottomPad,
                            seed + 3
                          )}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.52"
                          strokeWidth="1.35"
                          strokeLinecap="round"
                          className="text-sketch-line"
                        />
                        <path
                          d={buildSketchLinePath(
                            chartLayout.leftPad,
                            chartLayout.topPad,
                            chartLayout.leftPad,
                            chartHeight - chartLayout.bottomPad,
                            seed + 7
                          )}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.48"
                          strokeWidth="1.25"
                          strokeLinecap="round"
                          className="text-sketch-line"
                        />
                        <path
                          d={secondaryPath}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.28"
                          strokeWidth={compact ? 2.4 : 3.2}
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="text-pencil-emphasis"
                        />
                        <path
                          d={primaryPath}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.92"
                          strokeWidth={compact ? 2 : 2.45}
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className="text-pencil-stroke"
                        />
                        {visiblePoints.map(({ point, index }) => (
                          <g key={index} transform={`translate(${point.x.toFixed(2)} ${point.y.toFixed(2)})`}>
                            <circle r={compact ? 2.8 : 3.2} className="text-pencil-emphasis" fill="currentColor" fillOpacity="0.16" />
                            <circle r={compact ? 1.65 : 1.9} className="text-pencil-stroke" fill="currentColor" fillOpacity="0.76" />
                          </g>
                        ))}
                        <text
                          x={chartLayout.leftPad}
                          y={compact ? 15 : 18}
                          className="text-deep-ink"
                          fill="currentColor"
                          fontSize={compact ? 11 : 13}
                          fontWeight={700}
                        >
                          {titleLabel}
                        </text>
                        <text
                          x={chartWidth - chartLayout.rightPad}
                          y={compact ? 15 : 18}
                          className="text-sketch-line"
                          fill="currentColor"
                          fontSize={compact ? 9 : 10.5}
                          textAnchor="end"
                        >
                          {minLabel} to {maxLabel}
                        </text>
                        <text
                          x={chartLayout.leftPad - 5}
                          y={chartLayout.topPad + 4}
                          className="text-sketch-line"
                          fill="currentColor"
                          fontSize={compact ? 8.5 : 9.5}
                          textAnchor="end"
                        >
                          {maxLabel}
                        </text>
                        <text
                          x={chartLayout.leftPad - 5}
                          y={chartHeight - chartLayout.bottomPad + 4}
                          className="text-sketch-line"
                          fill="currentColor"
                          fontSize={compact ? 8.5 : 9.5}
                          textAnchor="end"
                        >
                          {minLabel}
                        </text>
                        <text
                          x={chartLayout.leftPad}
                          y={chartHeight - 7}
                          className="text-sketch-line"
                          fill="currentColor"
                          fontSize={compact ? 8.5 : 10}
                        >
                          {startLabel}
                        </text>
                        <text
                          x={chartWidth - chartLayout.rightPad}
                          y={chartHeight - 7}
                          className="text-sketch-line"
                          fill="currentColor"
                          fontSize={compact ? 8.5 : 10}
                          textAnchor="end"
                        >
                          {endLabel}
                        </text>
                      </svg>
                    </figure>
                  );
                })}
              </div>
            </div>
          </section>
        )}

        {sampleRows.length > 0 && (
          <section className={`notebook-chart-table ${compact ? 'px-1' : 'px-1 pb-1'}`}>
            <div className={`${tableScrollClass} max-h-80`} style={scrollStyle} {...panEventHandlers}>
              <table className="min-w-full border-separate border-spacing-0 text-[11px]">
                <tbody>
                  {sampleRows.map((row, rowIndex) => (
                    <tr key={rowIndex} className={rowIndex === 0 ? paperTextClass : paperSubtleTextClass}>
                      {row.map((cell, cellIndex) => (
                        <td
                          key={cellIndex}
                          className={`max-w-[12rem] whitespace-nowrap border-b border-sketch-line/15 px-2 py-1.5 truncate ${
                            rowIndex === 0 ? 'font-semibold text-deep-ink' : cellIndex === 0 ? 'text-deep-ink/85' : ''
                          }`}
                        >
                          {cell || '-'}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {!compact && activeSheet.rows.length > sampleRows.length && (
          <p className={`px-1 text-[10px] ${paperSubtleTextClass}`}>
            Showing {sampleRows.length} of {activeSheet.rows.length} rows in table preview.
          </p>
        )}
      </div>
    </div>
  );
};

export default TabularPreview;
