// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import React, { useState, useCallback, useRef } from 'react';
import { HexColorPicker } from 'react-colorful';
import { IconXMark, IconUndo } from '../../../shared/ui/Icons';
import { useMaestroStore } from '../../../store';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { COLOR_GROUPS } from '../config/colorRegistry';
import { DEFAULT_COLORS } from '../config/defaultColors';
import { hslStringToHex, hexToHslString } from '../utils/colorConversion';

interface ThemeCustomizerPanelProps {
  onClose: () => void;
}

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

        {/* Scrollable color groups */}
        <div className="flex-1 overflow-y-auto px-4 pb-8 space-y-5 scrollbar-hide">
          {COLOR_GROUPS.map(group => {
            const groupHasActive = activeColorVar !== null && group.colors.some(c => c.cssVar === activeColorVar);
            const activeColor = groupHasActive ? group.colors.find(c => c.cssVar === activeColorVar) : null;

            return (
              <div key={group.groupName}>
                <h3 className="text-xs font-hand text-muted-foreground mb-2 uppercase tracking-wider">
                  {group.groupName}
                </h3>
                <div className="grid grid-cols-4 gap-1.5">
                  {group.colors.map(cv => {
                    const hex = getEffectiveHex(cv.cssVar);
                    const active = activeColorVar === cv.cssVar;
                    const modified = isModified(cv.cssVar);

                    return (
                      <button
                        key={cv.cssVar}
                        type="button"
                        onClick={() => setActiveColorVar(active ? null : cv.cssVar)}
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

                {/* Inline picker */}
                {activeColor && activeColorVar && (
                  <div className="mt-3 p-3 bg-muted/50 rounded-lg border border-border/50 animate-fade-up">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-sm font-hand text-foreground">
                        {activeColor.friendlyName}
                      </span>
                      {isModified(activeColorVar) && (
                        <button
                          type="button"
                          onClick={() => handleResetColor(activeColorVar)}
                          className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                        >
                          <IconUndo className="w-3 h-3" />
                          Reset
                        </button>
                      )}
                    </div>
                    <HexColorPicker
                      color={getEffectiveHex(activeColorVar)}
                      onChange={(hex) => handleColorChange(activeColorVar, hex)}
                      style={{ width: '100%', height: '160px' }}
                    />
                    <div className="mt-2 text-xs text-muted-foreground text-center font-mono">
                      {getEffectiveHex(activeColorVar).toUpperCase()}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </>
  );
};

export default ThemeCustomizerPanel;
