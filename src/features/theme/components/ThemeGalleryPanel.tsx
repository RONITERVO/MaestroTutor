// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import React from 'react';
import { IconCheck, IconSparkles, IconXMark } from '../../../shared/ui/Icons';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { THEME_GALLERY_ITEMS } from '../config/themeCatalogue';
import { getThemePreset } from '../config/themePresets';
import type { PresetTheme } from '../config/presetThemes';

interface ThemeGalleryPanelProps {
  onApplyTheme: (preset: PresetTheme) => void;
  onClose: () => void;
}

/**
 * Bottom-sheet gallery for the color themes included with Maestro.
 *
 * The layout intentionally retains the former store's card presentation, but
 * the catalogue is local, immediate and permanently free: there is no account,
 * storefront, ownership cache, price lookup, purchase, or restore state.
 */
const ThemeGalleryPanel: React.FC<ThemeGalleryPanelProps> = ({ onApplyTheme, onClose }) => {
  const { t } = useAppTranslations();

  return (
    <>
      <div className="fixed inset-0 z-[89] bg-black/20" onClick={onClose} />

      <div
        className="fixed inset-x-0 bottom-0 z-[90] flex flex-col bg-theme-panel-bg/10 backdrop-blur-md border-t border-line-border shadow-2xl rounded-t-2xl overflow-hidden"
        style={{ maxHeight: '70vh' }}
      >
        <div className="flex flex-col items-center pt-2 pb-1 px-4 shrink-0">
          <div className="w-10 h-1 bg-theme-muted-text/30 rounded-full mb-2" />
          <div className="flex w-full items-center justify-between">
            <div className="flex items-center gap-2">
              <IconSparkles className="w-5 h-5 text-theme-panel-text" />
              <h2 className="text-lg font-sketch text-theme-panel-text">
                {t('themeGallery.title') || 'Theme Gallery'}
              </h2>
            </div>
            <button
              type="button"
              title={t('themeGallery.close') || 'Close'}
              onClick={onClose}
              className="p-1.5 rounded-lg text-theme-muted-text hover:text-theme-panel-text hover:bg-theme-input-bg transition-colors"
            >
              <IconXMark className="w-5 h-5" />
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-y-auto px-4 pb-4 space-y-3">
          {THEME_GALLERY_ITEMS.map(theme => {
            const preset = getThemePreset(theme.themeId);
            if (!preset) return null;

            return (
              <div
                key={theme.themeId}
                className="flex items-stretch gap-3 p-3 rounded-xl bg-theme-input-bg border border-theme-input-border"
              >
                <div className="flex flex-col gap-1 shrink-0 justify-center">
                  {theme.previewColors.map((hsl, index) => (
                    <div
                      key={`${theme.themeId}-${index}`}
                      className="w-5 h-5 rounded-sm"
                      style={{ backgroundColor: `hsl(${hsl})` }}
                    />
                  ))}
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1.5">
                    <span className="text-base">{theme.icon}</span>
                    <span className="font-sketch text-theme-panel-text text-sm leading-tight">
                      {theme.displayName}
                    </span>
                    <span className="ml-auto flex items-center gap-0.5 text-xs text-flag-busy-text bg-flag-busy-bg/20 px-1.5 py-0.5 rounded-full shrink-0">
                      <IconCheck className="w-3 h-3" />
                      {t('themeGallery.included') || 'Free'}
                    </span>
                  </div>
                  <p className="text-xs text-theme-muted-text mt-0.5 leading-snug">
                    {theme.description}
                  </p>
                </div>

                <div className="shrink-0 flex items-center">
                  <button
                    type="button"
                    onClick={() => onApplyTheme(preset)}
                    className="px-3 py-1.5 rounded-lg bg-gate-btn-bg text-gate-btn-text text-xs font-medium active:opacity-80 transition-opacity"
                  >
                    {t('themeGallery.apply') || 'Apply'}
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        <div className="px-4 pb-4 pt-1 shrink-0">
          <p className="text-center text-theme-muted-text/60 text-[10px] leading-tight">
            {t('themeGallery.footerNote') || 'Every color theme is included and will remain free.'}
          </p>
        </div>
      </div>
    </>
  );
};

export default ThemeGalleryPanel;
