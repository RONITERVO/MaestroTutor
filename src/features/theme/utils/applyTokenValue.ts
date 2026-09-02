// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { alphaVarName, parseTokenValue } from './tokenValue';

/**
 * The single place that writes a theme token to the DOM.
 *
 * Every write path goes through here - the settings sync hook, the customizer's
 * live preview, and preset application - so a stored value can never reach the
 * page with its channels and its opacity out of step.
 *
 * `--x-alpha` is always written, never left to fall back. A user who sets a
 * token to fully opaque has to win over a default theme that ships it
 * translucent, and only an explicit inline `1` does that.
 *
 * `--x-color` is not written: it is declared once in :root as
 * `hsl(var(--x) / var(--x-alpha, 1))` and re-resolves on its own when either
 * input changes on the same element.
 */
export function applyTokenValue(el: HTMLElement, cssVar: string, value: string): void {
  const { channels, alpha } = parseTokenValue(value);
  el.style.setProperty(`--${cssVar}`, channels);
  el.style.setProperty(alphaVarName(cssVar), String(alpha));
}

/** Drops a token's overrides so the value generated into :root applies again. */
export function clearTokenValue(el: HTMLElement, cssVar: string): void {
  el.style.removeProperty(`--${cssVar}`);
  el.style.removeProperty(alphaVarName(cssVar));
}
