// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { ORIGINAL_COLORS } from './themeColors';

export interface PresetTheme {
  name: string;
  /** Short description for the user. */
  description: string;
  /** Partial set of color overrides. Only listed vars are changed; unlisted revert to defaults. */
  colors: Record<string, string>;
}

export const PRESET_THEMES: PresetTheme[] = [
  {
    name: 'Notebook',
    description: 'The original hand-drawn look',
    colors: {},
  },
  {
    name: 'Original',
    description: 'The default',
    colors: ORIGINAL_COLORS,
  },
];
