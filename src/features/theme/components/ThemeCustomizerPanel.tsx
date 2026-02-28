// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import React, { useState, useCallback, useRef } from 'react';
import { HexColorPicker } from 'react-colorful';
import { IconXMark, IconUndo } from '../../../shared/ui/Icons';
import { useMaestroStore } from '../../../store';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { COLOR_GROUPS, type ColorGroup } from '../config/colorRegistry';
import { DEFAULT_COLORS } from '../config/defaultColors';
import { PRESET_THEMES, type PresetTheme } from '../config/presetThemes';
import { hslStringToHex, hexToHslString } from '../utils/colorConversion';

interface ThemeCustomizerPanelProps {
  onClose: () => void;
}

/* ------------------------------------------------------------------ */
/*  Collapsible Group                                                 */
/* ------------------------------------------------------------------ */

interface ColorGroupSectionProps {
  group: ColorGroup;
  customColors: Record<string, string>;
  activeColorVar: string | null;
  onSelectColor: (cssVar: string) => void;
  getEffectiveHex: (cssVar: string) => string;
  isModified: (cssVar: string) => boolean;
  onColorChange: (cssVar: string, hex: string) => void;
  onResetColor: (cssVar: string) => void;
}

const ColorGroupSection: React.FC<ColorGroupSectionProps> = ({
  group,
  activeColorVar,
  onSelectColor,
  getEffectiveHex,
  isModified,
  onColorChange,
  onResetColor,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(group.collapsedByDefault ?? false);

  const groupHasActive = activeColorVar !== null && group.colors.some(c => c.cssVar === activeColorVar);
  const activeColor = groupHasActive ? group.colors.find(c => c.cssVar === activeColorVar) : null;
  const groupModifiedCount = group.colors.filter(c => isModified(c.cssVar)).length;

  // Auto-expand if user selects a color inside a collapsed group (shouldn't happen in normal flow, but safety)
  if (groupHasActive && isCollapsed) {
    setIsCollapsed(false);
  }

  return (
    <div>
      {/* Group header – tappable to expand/collapse */}
      <button
        type="button"
        onClick={() => setIsCollapsed(!isCollapsed)}
        className="flex items-center w-full gap-2 py-1 group"
      >
        <span className={`text-[10px] text-muted-foreground transition-transform duration-200 ${isCollapsed ? '' : 'rotate-90'}`}>
          &#9654;
        </span>
        <span className="text-xs font-hand text-muted-foreground uppercase tracking-wider">
          {group.groupName}
        </span>
        {groupModifiedCount > 0 && (
          <span className="ml-auto text-[10px] bg-accent/20 text-accent px-1.5 py-0.5 rounded-full font-semibold">
            {groupModifiedCount}
          </span>
        )}
      </button>

      {group.groupDescription && !isCollapsed && (
        <p className="text-[10px] text-muted-foreground/70 ml-4 -mt-0.5 mb-1.5">
          {group.groupDescription}
        </p>
      )}

      {/* Color swatches */}
      {!isCollapsed && (
        <>
          <div className="grid grid-cols-4 gap-1.5 ml-1">
            {group.colors.map(cv => {
              const hex = getEffectiveHex(cv.cssVar);
              const active = activeColorVar === cv.cssVar;
              const modified = isModified(cv.cssVar);

              return (
                <button
                  key={cv.cssVar}
                  type="button"
                  onClick={() => onSelectColor(active ? '' : cv.cssVar)}
                  className={`flex flex-col items-center gap-1 p-1.5 rounded-lg transition-all duration-150
                    ${active ? 'bg-muted ring-2 ring-accent scale-105' : 'active:scale-95'}`}
                >
                  <div
                    className={`w-8 h-8 rounded-full border-2 shadow-sm transition-colors
                      ${modified ? 'border-accent' : 'border-border'}`}
                    style={{ backgroundColor: hex }}
                  />
                  <span className="text-[10px] text-muted-foreground leading-tight text-center truncate w-full">
                    {cv.friendlyName}
                  </span>
                  {modified && (
                    <div className="w-1.5 h-1.5 rounded-full bg-accent -mt-0.5" />
                  )}
                </button>
              );
            })}
          </div>

          {/* Inline picker for the selected color */}
          {activeColor && activeColorVar && (
            <div className="mt-3 p-3 bg-muted/50 rounded-lg border border-border/50 animate-fade-up">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-hand text-foreground">
                  {activeColor.friendlyName}
                </span>
                {isModified(activeColorVar) && (
                  <button
                    type="button"
                    onClick={() => onResetColor(activeColorVar)}
                    className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                  >
                    <IconUndo className="w-3 h-3" />
                    Reset
                  </button>
                )}
              </div>
              {activeColor.description && (
                <p className="text-[11px] text-muted-foreground/80 mb-2 leading-snug">
                  {activeColor.description}
                </p>
              )}
              <HexColorPicker
                color={getEffectiveHex(activeColorVar)}
                onChange={(hex) => onColorChange(activeColorVar, hex)}
                style={{ width: '100%', height: '160px' }}
              />
              <div className="mt-2 text-xs text-muted-foreground text-center font-mono">
                {getEffectiveHex(activeColorVar).toUpperCase()}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};

/* ------------------------------------------------------------------ */
/*  Main Panel                                                        */
/* ------------------------------------------------------------------ */

const ThemeCustomizerPanel: React.FC<ThemeCustomizerPanelProps> = ({ onClose }) => {
  const settings = useMaestroStore(selectSettings);
  const setSettings = useMaestroStore(state => state.setSettings);
  const customColors = settings.customColors || {};

  const [activeColorVar, setActiveColorVar] = useState<string | null>(null);

  // Debounce timer for IndexedDB persistence
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const getEffectiveHsl = useCallback((cssVar: string): string => {
    return customColors[cssVar] || DEFAULT_COLORS[cssVar] || '0 0% 50%';
  }, [customColors]);

  const getEffectiveHex = useCallback((cssVar: string): string => {
    return hslStringToHex(getEffectiveHsl(cssVar));
  }, [getEffectiveHsl]);

  // Apply color immediately to DOM for real-time preview, debounce persistence
  const handleColorChange = useCallback((cssVar: string, hex: string) => {
    const hslValue = hexToHslString(hex);

    // Immediate DOM update for smooth real-time preview
    document.documentElement.style.setProperty(`--${cssVar}`, hslValue);

    // Debounced store + IndexedDB persist
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setSettings(prev => ({
        ...prev,
        customColors: {
          ...(prev.customColors || {}),
          [cssVar]: hslValue,
        },
      }));
    }, 80);
  }, [setSettings]);

  const handleResetColor = useCallback((cssVar: string) => {
    document.documentElement.style.removeProperty(`--${cssVar}`);
    setSettings(prev => {
      const next = { ...(prev.customColors || {}) };
      delete next[cssVar];
      return { ...prev, customColors: Object.keys(next).length > 0 ? next : undefined };
    });
    setActiveColorVar(null);
  }, [setSettings]);

  const handleResetAll = useCallback(() => {
    const root = document.documentElement;
    for (const key of Object.keys(customColors)) {
      root.style.removeProperty(`--${key}`);
    }
    setSettings(prev => ({ ...prev, customColors: undefined }));
    setActiveColorVar(null);
  }, [setSettings, customColors]);

  const applyPreset = useCallback((preset: PresetTheme) => {
    const root = document.documentElement;

    // Clear all current overrides first
    for (const key of Object.keys(customColors)) {
      root.style.removeProperty(`--${key}`);
    }

    // If the preset has no colors (default), just reset
    if (Object.keys(preset.colors).length === 0) {
      setSettings(prev => ({ ...prev, customColors: undefined }));
      setActiveColorVar(null);
      return;
    }

    // Apply preset colors to DOM
    for (const [cssVar, hslValue] of Object.entries(preset.colors)) {
      root.style.setProperty(`--${cssVar}`, hslValue);
    }

    // Persist
    setSettings(prev => ({
      ...prev,
      customColors: { ...preset.colors },
    }));
    setActiveColorVar(null);
  }, [setSettings, customColors]);

  const isModified = (cssVar: string) => cssVar in customColors;
  const hasAnyCustomization = Object.keys(customColors).length > 0;

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-[89] bg-black/20"
        onClick={onClose}
      />

      {/* Panel */}
      <div
        className="fixed inset-x-0 bottom-0 z-[90] flex flex-col bg-card/95 backdrop-blur-md border-t border-border shadow-2xl rounded-t-2xl overflow-hidden"
        style={{ height: '75vh', maxHeight: '75vh' }}
      >
        {/* Header */}
        <div className="flex flex-col items-center pt-2 pb-1 px-4 shrink-0">
          <div className="w-10 h-1 bg-muted-foreground/30 rounded-full mb-2" />
          <div className="flex w-full items-center justify-between">
            <h2 className="text-lg font-sketch text-foreground">Colors</h2>
            <div className="flex items-center gap-2">
              {hasAnyCustomization && (
                <button
                  type="button"
                  onClick={handleResetAll}
                  className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground px-2 py-1 rounded transition-colors"
                >
                  <IconUndo className="w-3 h-3" />
                  Reset All
                </button>
              )}
              <button
                type="button"
                onClick={onClose}
                className="p-1.5 text-muted-foreground hover:text-foreground rounded-full transition-colors"
              >
                <IconXMark className="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>

        {/* Scrollable content */}
        <div className="flex-1 overflow-y-auto px-4 pb-8 space-y-4 scrollbar-hide">

          {/* ── Quick Themes ────────────────────────── */}
          <div>
            <h3 className="text-xs font-hand text-muted-foreground uppercase tracking-wider mb-2">
              Quick Themes
            </h3>
            <div className="flex flex-wrap gap-2">
              {PRESET_THEMES.map(preset => {
                // Show a small preview swatch of the preset's accent or background
                const previewBg = preset.colors['background'] || DEFAULT_COLORS['background'];
                const previewAccent = preset.colors['accent'] || DEFAULT_COLORS['accent'];
                const previewFg = preset.colors['foreground'] || DEFAULT_COLORS['foreground'];

                return (
                  <button
                    key={preset.name}
                    type="button"
                    onClick={() => applyPreset(preset)}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border/60 hover:border-accent/50 bg-card/60 hover:bg-muted/50 transition-all active:scale-95"
                  >
                    {/* 3-dot preview swatch */}
                    <div className="flex gap-0.5">
                      <div className="w-3 h-3 rounded-full border border-border/40" style={{ backgroundColor: hslStringToHex(previewBg) }} />
                      <div className="w-3 h-3 rounded-full border border-border/40" style={{ backgroundColor: hslStringToHex(previewAccent) }} />
                      <div className="w-3 h-3 rounded-full border border-border/40" style={{ backgroundColor: hslStringToHex(previewFg) }} />
                    </div>
                    <div className="text-left">
                      <div className="text-xs font-semibold text-foreground leading-tight">
                        {preset.name}
                      </div>
                      <div className="text-[9px] text-muted-foreground leading-tight">
                        {preset.description}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* ── Divider ────────────────────────── */}
          <div className="border-t border-border/30" />

          {/* ── Individual Color Groups ────────────────────────── */}
          {COLOR_GROUPS.map(group => (
            <ColorGroupSection
              key={group.groupName}
              group={group}
              customColors={customColors}
              activeColorVar={activeColorVar}
              onSelectColor={(cssVar) => setActiveColorVar(cssVar || null)}
              getEffectiveHex={getEffectiveHex}
              isModified={isModified}
              onColorChange={handleColorChange}
              onResetColor={handleResetColor}
            />
          ))}
        </div>
      </div>
    </>
  );
};

export default ThemeCustomizerPanel;
