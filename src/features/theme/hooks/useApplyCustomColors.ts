// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { useEffect } from 'react';
import { useMaestroStore } from '../../../store';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { ALL_COLOR_VARS } from '../config/colorRegistry';

/**
 * Syncs customColors from settings to CSS custom properties on <html>.
 * Inline style properties override the :root defaults from index.css.
 * Removing the property resets to the CSS default.
 */
export function useApplyCustomColors() {
  const customColors = useMaestroStore(state => selectSettings(state).customColors);

  useEffect(() => {
    const root = document.documentElement;
    for (const { cssVar } of ALL_COLOR_VARS) {
      const customValue = customColors?.[cssVar];
      if (customValue) {
        root.style.setProperty(`--${cssVar}`, customValue);
      } else {
        root.style.removeProperty(`--${cssVar}`);
      }
    }
  }, [customColors]);
}
