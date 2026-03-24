// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import React, { useState, useCallback, useRef } from 'react';
import { HexColorPicker } from 'react-colorful';
import { IconXMark, IconUndo, IconBookmark, IconDownload, IconUpload, IconCheck, IconSparkles } from '../../../shared/ui/Icons';
import { useMaestroStore } from '../../../store';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { COLOR_GROUPS, type ColorGroup } from '../config/colorRegistry';
import { ORIGINAL_COLORS } from '../config/themeColors';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { PRESET_THEMES, type PresetTheme } from '../config/presetThemes';
import { getPurchasableThemePreset } from '../config/purchasableThemePresets';
import { THEME_PRODUCTS } from '../config/themeProducts';
import { useThemeBilling } from '../hooks/useThemeBilling';
import { hslStringToHex, hexToHslString } from '../utils/colorConversion';
import { exportThemeToFile, importThemeFromFile } from '../utils/themeFileIO';
import ThemeStorePanel from './ThemeStorePanel';

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
        <span className={`text-[10px] text-theme-muted-text transition-transform duration-200 ${isCollapsed ? '' : 'rotate-90'}`}>
          &#9654;
        </span>
        <span className="text-xs font-hand text-theme-muted-text uppercase tracking-wider">
          {group.groupName}
        </span>
        {groupModifiedCount > 0 && (
          <span className="ml-auto text-[10px] bg-theme-input-border/20 text-theme-input-border px-1.5 py-0.5 rounded-full font-semibold">
            {groupModifiedCount}
          </span>
        )}
      </button>

      {group.groupDescription && !isCollapsed && (
        <p className="text-[10px] text-theme-muted-text/70 ml-4 -mt-0.5 mb-1.5">
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
                    ${active ? 'bg-theme-panel-bg/80 ring-2 ring-theme-input-border scale-105' : 'active:scale-95'}`}
                >
                  <div
                    className={`w-8 h-8 rounded-full border-2 shadow-sm transition-colors
                      ${modified ? 'border-theme-input-border' : 'border-line-border'}`}
                    style={{ backgroundColor: hex }}
                  />
                  <span className="text-[10px] text-theme-muted-text leading-tight text-center truncate w-full">
                    {cv.friendlyName}
                  </span>
                  {modified && (
                    <div className="w-1.5 h-1.5 rounded-full bg-theme-input-border -mt-0.5" />
                  )}
                </button>
              );
            })}
          </div>

          {/* Inline picker for the selected color */}
          {activeColor && activeColorVar && (
            <div className="mt-3 p-3 bg-theme-panel-bg/50 rounded-lg border border-line-border/50 animate-fade-up">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-hand text-theme-panel-text">
                  {activeColor.friendlyName}
                </span>
                {isModified(activeColorVar) && (
                  <button
                    type="button"
                    onClick={() => onResetColor(activeColorVar)}
                    className="flex items-center gap-1 text-xs text-theme-muted-text hover:text-theme-panel-text transition-colors"
                  >
                    <IconUndo className="w-3 h-3" />
                    Reset
                  </button>
                )}
              </div>
              {activeColor.description && (
                <p className="text-[11px] text-theme-muted-text/80 mb-2 leading-snug">
                  {activeColor.description}
                </p>
              )}
              <HexColorPicker
                color={getEffectiveHex(activeColorVar)}
                onChange={(hex) => onColorChange(activeColorVar, hex)}
                style={{ width: '100%', height: '160px' }}
              />
              <div className="mt-2 text-xs text-theme-muted-text text-center font-mono">
                {getEffectiveHex(activeColorVar).toUpperCase()}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};

interface QuickThemeButtonProps {
  name: string;
  description: string;
  previewColors: string[];
  onClick: () => void;
  accentIcon?: React.ReactNode;
  className?: string;
}

const getPresetPreviewColors = (preset: PresetTheme): string[] => [
  preset.colors['page-bg'] || ORIGINAL_COLORS['page-bg'],
  preset.colors['chat-outer-bg'] || ORIGINAL_COLORS['chat-outer-bg'],
  preset.colors['page-text'] || ORIGINAL_COLORS['page-text'],
];

const QuickThemeButton: React.FC<QuickThemeButtonProps> = ({
  name,
  description,
  previewColors,
  onClick,
  accentIcon,
  className = '',
}) => (
  <button
    type="button"
    onClick={onClick}
    className={`flex items-center gap-2 px-3 py-2 rounded-lg border border-line-border/60 hover:border-theme-input-border/50 bg-theme-preset-btn/60 hover:bg-theme-panel-bg/50 transition-all active:scale-95 ${className}`.trim()}
  >
    <div className="flex gap-0.5">
      {previewColors.slice(0, 3).map((color, index) => (
        <div
          key={`${name}-${index}`}
          className="w-3 h-3 rounded-full border border-line-border/40"
          style={{ backgroundColor: hslStringToHex(color) }}
        />
      ))}
    </div>
    <div className="text-left min-w-0">
      <div className="text-xs font-semibold text-theme-panel-text leading-tight flex items-center gap-1">
        <span className="truncate">{name}</span>
        {accentIcon}
      </div>
      <div className="text-[9px] text-theme-muted-text leading-tight">
        {description}
      </div>
    </div>
  </button>
);

/* ------------------------------------------------------------------ */
/*  Main Panel                                                        */
/* ------------------------------------------------------------------ */

const ThemeCustomizerPanel: React.FC<ThemeCustomizerPanelProps> = ({ onClose }) => {
  const { t } = useAppTranslations();
  const settings = useMaestroStore(selectSettings);
  const setSettings = useMaestroStore(state => state.setSettings);
  const customColors = settings.customColors || {};

  const [activeColorVar, setActiveColorVar] = useState<string | null>(null);
  const [isThemeStoreOpen, setIsThemeStoreOpen] = useState(false);
  const { ownedProductIds } = useThemeBilling();

  // Debounce timer for IndexedDB persistence
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const getEffectiveHsl = useCallback((cssVar: string): string => {
    return customColors[cssVar] || ORIGINAL_COLORS[cssVar] || '0 0% 50%';
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
  const savedPresets = settings.savedThemePresets || [];
  const ownedPurchasedThemes = THEME_PRODUCTS.flatMap(product => {
    if (!ownedProductIds.has(product.productId)) return [];

    const preset = getPurchasableThemePreset(product.productId);
    if (!preset) return [];

    return [{ productId: product.productId, preset }];
  });

  // Inline naming state: 'save' or 'export' mode, or null when idle
  const [namingMode, setNamingMode] = useState<'save' | 'export' | null>(null);
  const [nameInput, setNameInput] = useState('');
  const nameInputRef = useRef<HTMLInputElement>(null);

  const startNaming = useCallback((mode: 'save' | 'export') => {
    if (!hasAnyCustomization) return;
    setNameInput('');
    setNamingMode(mode);
    // Focus after render
    setTimeout(() => nameInputRef.current?.focus(), 0);
  }, [hasAnyCustomization]);

  const cancelNaming = useCallback(() => {
    setNamingMode(null);
    setNameInput('');
  }, []);

  const confirmNaming = useCallback(async () => {
    const trimmed = nameInput.trim();
    if (!trimmed || !namingMode) return;

    if (namingMode === 'save') {
      const preset: PresetTheme = {
        name: trimmed,
        description: 'Custom theme',
        colors: { ...customColors },
      };
      setSettings(prev => ({
        ...prev,
        savedThemePresets: [...(prev.savedThemePresets || []), preset],
      }));
    } else {
      try {
        await exportThemeToFile({
          name: trimmed,
          description: 'Custom theme',
          colors: { ...customColors },
        });
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        if (!msg.toLowerCase().includes('cancel')) {
          console.error('Failed to export theme:', err);
        }
      }
    }

    setNamingMode(null);
    setNameInput('');
  }, [nameInput, namingMode, customColors, setSettings]);

  const handleDeleteSavedPreset = useCallback((index: number) => {
    setSettings(prev => {
      const next = [...(prev.savedThemePresets || [])];
      next.splice(index, 1);
      return { ...prev, savedThemePresets: next.length > 0 ? next : undefined };
    });
  }, [setSettings]);

  const handleImport = useCallback(async () => {
    try {
      const preset = await importThemeFromFile();
      applyPreset(preset);

      // Auto-save the imported theme so it appears in Quick Themes
      // and can be switched back to without re-importing.
      // Skip if a saved preset with the same name already exists.
      setSettings(prev => {
        const existing = prev.savedThemePresets || [];
        if (existing.some(p => p.name === preset.name)) return prev;
        return {
          ...prev,
          savedThemePresets: [...existing, preset],
        };
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg === 'CANCELLED') return;
      if (msg === 'INVALID_THEME_FORMAT') {
        alert('Invalid theme file. Please select a valid .json theme file.');
        return;
      }
      console.error('Failed to import theme:', err);
    }
  }, [applyPreset, setSettings]);

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-[89] bg-black/20"
        onClick={onClose}
      />

      {/* Panel */}
      <div
        className="fixed inset-x-0 bottom-0 z-[90] flex flex-col bg-theme-panel-bg/10 backdrop-blur-md border-t border-line-border shadow-2xl rounded-t-2xl overflow-hidden"
        style={{ height: '45vh', maxHeight: '45vh' }}
      >
        {/* Header */}
        <div className="flex flex-col items-center pt-2 pb-1 px-4 shrink-0">
          <div className="w-10 h-1 bg-theme-muted-text/30 rounded-full mb-2" />
          <div className="flex w-full items-center justify-between">
            <h2 className="text-lg font-sketch text-theme-panel-text">{t('themeCustomizer.title') || 'Paint Colors'}</h2>
            <div className="flex items-center gap-1">
              {hasAnyCustomization && (
                <button
                  type="button"
                  onClick={() => startNaming('save')}
                  className="flex items-center gap-1 text-xs text-theme-muted-text hover:text-theme-panel-text px-2 py-1 rounded transition-colors"
                  title={t('themeCustomizer.saveAsPreset') || 'Save as preset'}
                >
                  <IconBookmark className="w-3.5 h-3.5" />
                </button>
              )}
              {hasAnyCustomization && (
                <button
                  type="button"
                  onClick={() => startNaming('export')}
                  className="flex items-center gap-1 text-xs text-theme-muted-text hover:text-theme-panel-text px-2 py-1 rounded transition-colors"
                  title={t('themeCustomizer.exportToFile') || 'Export to file'}
                >
                  <IconDownload className="w-3.5 h-3.5" />
                </button>
              )}
              <button
                type="button"
                onClick={handleImport}
                className="flex items-center gap-1 text-xs text-theme-muted-text hover:text-theme-panel-text px-2 py-1 rounded transition-colors"
                title={t('themeCustomizer.importFromFile') || 'Import from file'}
              >
                <IconUpload className="w-3.5 h-3.5" />
              </button>
              {hasAnyCustomization && (
                <button
                  type="button"
                  onClick={handleResetAll}
                  className="flex items-center gap-1 text-xs text-theme-muted-text hover:text-theme-panel-text px-2 py-1 rounded transition-colors"
                >
                  <IconUndo className="w-3 h-3" />
                  {t('themeCustomizer.resetAll') || 'Reset All'}
                </button>
              )}
              <button
                type="button"
                onClick={onClose}
                className="p-1.5 text-theme-muted-text hover:text-theme-panel-text rounded-full transition-colors"
              >
                <IconXMark className="w-5 h-5" />
              </button>
            </div>
          </div>
          <p className="w-full text-[11px] text-theme-muted-text/85 mt-0.5">
            {t('themeCustomizer.instruction') || 'Tap a color circle, then drag in the picker. Changes apply instantly.'}
          </p>

          {/* Inline naming row */}
          {namingMode && (
            <div className="flex w-full items-center gap-1.5 mt-1.5 animate-fade-up">
              <span className="text-[10px] font-hand text-theme-muted-text uppercase tracking-wider shrink-0">
                {namingMode === 'save' ? t('themeCustomizer.saveAs') || 'Save as' : t('themeCustomizer.exportAs') || 'Export as'}
              </span>
              <input
                ref={nameInputRef}
                type="text"
                value={nameInput}
                onChange={(e) => setNameInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') confirmNaming();
                  if (e.key === 'Escape') cancelNaming();
                }}
                className="flex-1 bg-theme-input-bg/80 border border-theme-input-border/30 sketchy-border-thin px-2 py-0.5 text-xs text-theme-panel-text placeholder-theme-muted-text/60 focus:outline-none focus:border-theme-input-border transition-colors font-hand"
                placeholder={t('themeCustomizer.namePlaceholder') || 'Theme name...'}
                autoFocus
              />
              <button
                type="button"
                onClick={confirmNaming}
                disabled={!nameInput.trim()}
                className="p-1 text-theme-input-border hover:text-theme-panel-text disabled:opacity-30 transition-colors"
                title={t('themeCustomizer.confirm') || 'Confirm'}
              >
                <IconCheck className="w-4 h-4" />
              </button>
              <button
                type="button"
                onClick={cancelNaming}
                className="p-1 text-theme-muted-text hover:text-theme-panel-text transition-colors"
                title={t('themeCustomizer.cancel') || 'Cancel'}
              >
                <IconXMark className="w-4 h-4" />
              </button>
            </div>
          )}
        </div>

        {/* Scrollable content */}
        <div className="flex-1 overflow-y-auto px-4 pb-8 space-y-4 scrollbar-hide">

          {/* ── Quick Themes ────────────────────────── */}
          <div>
            <h3 className="text-xs font-hand text-theme-muted-text uppercase tracking-wider mb-2">
              {t('themeCustomizer.quickThemes') || 'Quick Themes'}
            </h3>
            <div className="flex flex-wrap gap-2">
              {PRESET_THEMES.map(preset => {
                return (
                  <QuickThemeButton
                    key={preset.name}
                    name={preset.name}
                    description={preset.description}
                    previewColors={getPresetPreviewColors(preset)}
                    onClick={() => applyPreset(preset)}
                  />
                );
              })}
              {ownedPurchasedThemes.map(({ productId, preset }) => (
                <QuickThemeButton
                  key={productId}
                  name={preset.name}
                  description={preset.description}
                  previewColors={getPresetPreviewColors(preset)}
                  onClick={() => applyPreset(preset)}
                  accentIcon={<IconCheck className="w-3 h-3 text-flag-busy-text shrink-0" />}
                  className="border-theme-input-border/30"
                />
              ))}
              {savedPresets.map((preset, idx) => {
                return (
                  <div key={`saved-${idx}`} className="relative group/saved">
                    <QuickThemeButton
                      name={preset.name}
                      description={preset.description}
                      previewColors={getPresetPreviewColors(preset)}
                      onClick={() => applyPreset(preset)}
                      className="border-theme-input-border/30"
                    />
                    <button
                      type="button"
                      onClick={(e) => { e.stopPropagation(); handleDeleteSavedPreset(idx); }}
                      className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-gate-error-text text-gate-btn-text flex items-center justify-center text-[10px] leading-none opacity-0 group-hover/saved:opacity-100 transition-opacity"
                      title={t('themeCustomizer.deletePreset') || 'Delete saved preset'}
                    >
                      &times;
                    </button>
                  </div>
                );
              })}
              <QuickThemeButton
                name={t('themeCustomizer.storeTitle') || 'Theme Store'}
                description={t('themeCustomizer.storeDescription') || 'Browse and buy more themes'}
                previewColors={[
                  '210 70% 45%',
                  '38 90% 55%',
                  '280 100% 65%',
                ]}
                onClick={() => setIsThemeStoreOpen(true)}
                accentIcon={<IconSparkles className="w-3 h-3 text-theme-input-border shrink-0" />}
              />
            </div>
          </div>

          {/* ── Divider ────────────────────────── */}
          <div className="border-t border-line-border/30" />

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
      {isThemeStoreOpen && (
        <ThemeStorePanel onClose={() => setIsThemeStoreOpen(false)} />
      )}
    </>
  );
};

export default ThemeCustomizerPanel;
