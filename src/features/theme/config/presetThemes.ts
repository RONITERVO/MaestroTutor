// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface PresetTheme {
  name: string;
  /** Short description for the user. */
  description: string;
  /**
   * A complete snapshot of every token. Applying a preset clears the current
   * overrides first, so a missing token would fall through to the app default
   * rather than to this theme; themePresets.ts fills any gaps before publishing.
   */
  colors: Record<string, string>;
}
