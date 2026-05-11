// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo, useState } from 'react';
import { IconHandRaised, IconReturnToChatScroll } from '../../../shared/ui/Icons';
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

interface TabularInteractionDeckToggleProps {
  isPanEnabled: boolean;
  textColorClass: string;
  subtleTextClass: string;
  surfaceClassName: string;
  panelSurfaceClassName: string;
  onToggle: () => void;
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

const computeLinePoints = (series: TabularChartSeries, width: number, height: number): Point[] => {
  if (!series.values.length) return [];
  const min = Math.min(...series.values);
  const max = Math.max(...series.values);
  const range = max - min || 1;
  const leftPad = 14;
  const rightPad = 10;
  const topPad = 10;
  const bottomPad = 14;
  const drawableW = Math.max(1, width - leftPad - rightPad);
  const drawableH = Math.max(1, height - topPad - bottomPad);

  return series.values.map((value, index) => {
    const x = leftPad + (series.values.length === 1 ? drawableW / 2 : (index / (series.values.length - 1)) * drawableW);
    const y = topPad + (1 - (value - min) / range) * drawableH;
    return { x, y };
  });
};

const TabularInteractionDeckToggle: React.FC<TabularInteractionDeckToggleProps> = ({
  isPanEnabled,
  textColorClass,
  subtleTextClass,
  surfaceClassName,
  panelSurfaceClassName,
  onToggle,
}) => {
  const modes = [
    {
      enabled: false,
      label: 'Chat scroll',
      Icon: IconReturnToChatScroll,
      shapeClass: 'sketch-shape-2',
    },
    {
      enabled: true,
      label: 'Table pan',
      Icon: IconHandRaised,
      shapeClass: 'sketch-shape-3',
    },
  ];
  const actionLabel = isPanEnabled ? 'Use chat scroll' : 'Pan chart and table';

  return (
    <div className="relative h-7 w-[90px] shrink-0 select-none" role="group" aria-label="Artifact interaction mode">
      {modes.map(({ enabled, label, Icon, shapeClass }) => {
        const isActive = isPanEnabled === enabled;
        const sizeClass = isActive ? 'h-6 w-[74px]' : 'h-[22px] w-[46px]';
        const positionClass = isActive
          ? 'left-0 top-0 z-20 -rotate-2 scale-100'
          : 'right-0 bottom-0 z-10 rotate-6 scale-95';
        const toneClass = isActive
          ? `${surfaceClassName} ${textColorClass}`
          : `${panelSurfaceClassName} ${subtleTextClass}`;
        const contentClass = isActive ? 'justify-start pl-2 pr-1' : 'justify-end px-1.5';

        return (
          <button
            key={enabled ? 'pan' : 'scroll'}
            type="button"
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              onToggle();
            }}
            className={`absolute ${sizeClass} ${positionClass} ${shapeClass} ${toneClass} border border-sketch-line/45 paper-texture isolate overflow-hidden transition-all duration-300 ease-out focus:outline-none focus:ring-2 focus:ring-mode-toggle-text/30 active:scale-95 btn-depth`}
            title={actionLabel}
            aria-label={actionLabel}
            aria-pressed={isActive}
          >
            <span className={`relative z-10 flex h-full w-full items-center gap-1 ${contentClass}`} aria-hidden="true">
              <Icon className="h-3.5 w-3.5 shrink-0" />
              {isActive && (
                <span className="max-w-[42px] truncate text-[9px] font-semibold uppercase tracking-wide">
                  {label}
                </span>
              )}
            </span>
          </button>
        );
      })}
    </div>
  );
};

const TabularPreview: React.FC<TabularPreviewProps> = ({
  sheets,
  textColorClass,
  subtleTextClass,
  compact = false,
  title,
  standalone = false,
  bottomInset = 0,
  surfaceClassName = 'bg-paper-surface/90',
  panelSurfaceClassName = 'bg-paper-stripe/55',
}) => {
  const [activeSheetIndex, setActiveSheetIndex] = useState(0);
  const [isPanEnabled, setIsPanEnabled] = useState(false);

  const maxRows = compact ? SAMPLE_ROW_LIMIT_COMPACT : SAMPLE_ROW_LIMIT_FULL;
  const maxCols = compact ? SAMPLE_COL_LIMIT_COMPACT : SAMPLE_COL_LIMIT_FULL;
  const chartHeight = compact ? 76 : 132;
  const chartWidth = compact ? 220 : 320;
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
  const xScrollClass = isPanEnabled ? 'overflow-x-auto overflow-y-hidden' : 'overflow-hidden';
  const tableScrollClass = isPanEnabled ? 'overflow-auto' : 'overflow-hidden';
  const shellClass = standalone
    ? `mt-2 w-full max-w-[560px] rounded-2xl border border-sketch-line/50 ${surfaceClassName} paper-texture notebook-lines msg-depth isolate overflow-hidden ${textColorClass}`
    : `mt-2 rounded-xl border border-sketch-line/35 ${surfaceClassName} paper-texture notebook-lines overflow-hidden ${textColorClass}`;
  const contentPaddingStyle = effectiveBottomInset > 0
    ? { paddingBottom: `calc(0.75rem + ${effectiveBottomInset}px)` }
    : undefined;

  return (
    <div className={compact ? 'mt-2 space-y-2' : shellClass}>
      {!compact && (
        <div className={`relative z-10 flex items-center justify-between gap-3 px-3 py-2 border-b border-sketch-line/25 ${panelSurfaceClassName}`}>
          <div className="min-w-0">
            <p className={`text-[11px] font-semibold uppercase tracking-wide truncate ${textColorClass}`}>
              {title || activeSheet.name || 'Chart data'}
            </p>
            <p className={`text-[10px] truncate ${subtleTextClass}`}>
              {activeSheet.rows.length.toLocaleString()} rows
              {chartList.length > 0 ? ` / ${chartList.length} chart ${chartList.length === 1 ? 'series' : 'series'}` : ''}
            </p>
          </div>
          <TabularInteractionDeckToggle
            isPanEnabled={isPanEnabled}
            textColorClass={textColorClass}
            subtleTextClass={subtleTextClass}
            surfaceClassName={surfaceClassName}
            panelSurfaceClassName={panelSurfaceClassName}
            onToggle={() => setIsPanEnabled((prev) => !prev)}
          />
        </div>
      )}

      <div className={compact ? 'space-y-2' : 'relative z-10 space-y-3 p-3'} style={contentPaddingStyle}>
        {!compact && normalizedSheets.length > 1 && (
          <div className="overflow-x-auto pb-1" style={scrollStyle}>
            <div className="inline-flex min-w-max gap-1.5">
              {normalizedSheets.map((sheet, index) => (
                <button
                  key={`${sheet.name}-${index}`}
                  type="button"
                  onClick={() => setActiveSheetIndex(index)}
                  className={`sketch-shape-8 border px-2 py-1 text-[10px] transition-colors ${
                    index === safeIndex
                      ? `border-pencil-stroke/35 ${surfaceClassName} ${textColorClass}`
                      : `border-sketch-line/30 ${panelSurfaceClassName} ${subtleTextClass}`
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
          <section className={compact ? `rounded border border-sketch-line/25 ${panelSurfaceClassName} p-2` : `sketchy-border-thin sketch-shape-4 ${panelSurfaceClassName} p-2 shadow-sm`}>
            <div className={`mb-2 flex items-center justify-between text-[10px] ${subtleTextClass}`}>
              <span>{compact ? 'Chart preview' : 'Charts'}</span>
              {!compact && <span>{chartList.length} series</span>}
            </div>
            <div className={xScrollClass} style={scrollStyle}>
              <div className={`flex gap-3 ${compact ? '' : 'min-w-max pr-1'}`}>
                {chartList.map((chart, chartIndex) => {
                  const points = computeLinePoints(chart, chartWidth, chartHeight);
                  if (points.length < 2) return null;
                  const path = `M ${points.map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(' L ')}`;
                  const min = Math.min(...chart.values);
                  const max = Math.max(...chart.values);

                  return (
                    <figure
                      key={`${chart.label || 'series'}-${chart.sourceColumnIndex ?? chartIndex}`}
                      className={`${compact ? 'w-full' : 'w-[18rem] shrink-0'} sketch-shape-5 border border-sketch-line/30 ${surfaceClassName} p-2 shadow-sm`}
                    >
                      <figcaption className={`mb-1 flex items-center justify-between gap-2 text-[10px] ${subtleTextClass}`}>
                        <span className={`truncate ${compact ? 'max-w-[58%]' : 'max-w-[64%]'} ${textColorClass}`}>
                          {chart.label || `Series ${chartIndex + 1}`}
                        </span>
                        <span className="shrink-0">min {min.toLocaleString()} / max {max.toLocaleString()}</span>
                      </figcaption>
                      <svg
                        viewBox={`0 0 ${chartWidth} ${chartHeight}`}
                        className="block h-auto w-full"
                        role="img"
                        aria-label={chart.label || `Chart series ${chartIndex + 1}`}
                      >
                        <path
                          d={`M 14 ${chartHeight - 14} H ${chartWidth - 10} M 14 10 V ${chartHeight - 14}`}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.26"
                          strokeWidth="1.3"
                          strokeLinecap="round"
                          className={subtleTextClass}
                        />
                        <path
                          d={`M 14 ${Math.round(chartHeight * 0.36)} C ${Math.round(chartWidth * 0.3)} ${Math.round(chartHeight * 0.33)}, ${Math.round(chartWidth * 0.62)} ${Math.round(chartHeight * 0.38)}, ${chartWidth - 10} ${Math.round(chartHeight * 0.34)}`}
                          fill="none"
                          stroke="currentColor"
                          strokeOpacity="0.1"
                          strokeWidth="1.1"
                          strokeDasharray="4 6"
                          strokeLinecap="round"
                          className={subtleTextClass}
                        />
                        <path
                          d={path}
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2.3"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          className={textColorClass}
                        />
                        {points.map((point, i) => (
                          <circle key={i} cx={point.x} cy={point.y} r="2.35" className={textColorClass} fill="currentColor" />
                        ))}
                      </svg>
                      <div className={`mt-1 flex justify-between gap-2 text-[10px] ${subtleTextClass}`}>
                        <span className="max-w-[48%] truncate">{chart.labels[0] || 'start'}</span>
                        <span className="max-w-[48%] truncate text-right">
                          {chart.labels[chart.labels.length - 1] || 'end'}
                        </span>
                      </div>
                    </figure>
                  );
                })}
              </div>
            </div>
          </section>
        )}

        {sampleRows.length > 0 && (
          <section className={compact ? `rounded border border-sketch-line/25 ${panelSurfaceClassName}` : `sketchy-border-thin sketch-shape-1 ${panelSurfaceClassName} shadow-sm`}>
            <div className={`${tableScrollClass} max-h-80`} style={scrollStyle}>
              <table className="min-w-full border-collapse text-[10px]">
                <tbody>
                  {sampleRows.map((row, rowIndex) => (
                    <tr key={rowIndex} className={rowIndex === 0 ? `${textColorClass}` : `${subtleTextClass}`}>
                      {row.map((cell, cellIndex) => (
                        <td
                          key={cellIndex}
                          className={`max-w-[12rem] whitespace-nowrap border-b border-sketch-line/15 px-2 py-1.5 truncate ${
                            rowIndex === 0 ? `${surfaceClassName} font-semibold` : ''
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
          <p className={`text-[10px] ${subtleTextClass}`}>
            Showing {sampleRows.length} of {activeSheet.rows.length} rows in table preview.
          </p>
        )}
      </div>
    </div>
  );
};

export default TabularPreview;
