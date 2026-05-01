// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { useEffect } from 'react';
import { useMaestroStore } from '../../../store';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { ALL_COLOR_VARS } from '../config/colorRegistry';
import { DEFAULT_THEME_COLORS } from '../config/defaultTheme';
/**
 * Syncs customColors from settings to CSS custom properties on <html>.
 * Falls back to the current app default theme when no custom value is set, so
 * new tokens take effect immediately via HMR without a restart.
 */
export function useApplyCustomColors() {
  const customColors = useMaestroStore(state => selectSettings(state).customColors);

  useEffect(() => {
    const root = document.documentElement;
    const managedVars = new Set([
      ...ALL_COLOR_VARS.map(({ cssVar }) => cssVar),
      ...Object.keys(customColors || {}),
    ]);

    for (const cssVar of managedVars) {
      const value = customColors?.[cssVar] || DEFAULT_THEME_COLORS[cssVar];
      if (value) {
        root.style.setProperty(`--${cssVar}`, value);
      } else {
        root.style.removeProperty(`--${cssVar}`);
      }
    }
  }, [customColors]);
}
