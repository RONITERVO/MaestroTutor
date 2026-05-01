// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { DEFAULT_THEME_PRESET } from './defaultTheme';

export interface PresetTheme {
  name: string;
  /** Short description for the user. */
  description: string;
  /** Partial set of color overrides. Only listed vars are changed; unlisted revert to defaults. */
  colors: Record<string, string>;
}

export const PRESET_THEMES: PresetTheme[] = [
  {
    name: DEFAULT_THEME_PRESET.name,
    description: 'Current app default',
    colors: {},
  },
];
