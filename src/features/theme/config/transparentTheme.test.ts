// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { makeTransparentPalette, unreadableTokens } from './transparentTheme';
import { THEME_PRESETS_BY_ID } from './themePresets';
import { ALL_THEMES, THEME_IDS, isClearThemeId } from './themeCatalogue';
import { parseTokenValue } from '../utils/tokenValue';
import { contrastRatio, ensureContrast, parseHsl } from '../utils/contrast';

const clearPalettes = () =>
  Object.entries(THEME_PRESETS_BY_ID).filter(([themeId]) => isClearThemeId(themeId));

const alphaOf = (palette: Record<string, string>, cssVar: string) =>
  parseTokenValue(palette[cssVar]).alpha;

describe('clear theme derivation', () => {
  it('drops the fills that make the outline look', () => {
    const clear = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE_CLEAR].colors;
    // The composer and the chat are the elements people liked as outlines.
    for (const cssVar of [
      'chat-input-bg', 'chat-outer-bg', 'sugg-input-bg', 'user-msg-bg', 'ai-msg-bg',
      'send-btn-bg', 'suggestion-bg', 'paper-surface', 'audio-player-bg', 'debug-panel-bg',
    ]) {
      expect(alphaOf(clear, cssVar), cssVar).toBe(0);
    }
  });

  it('keeps the fills that would otherwise become unreadable or unnoticeable', () => {
    for (const [themeId, preset] of clearPalettes()) {
      for (const cssVar of [
        'page-bg',                              // the canvas everything is read against
        'flag-idle-bg', 'flag-speaking-bg',     // Maestro's state has to register
        'marker-target-bg', 'marker-native-bg', // the translation highlight is a fill
        'audio-play-btn', 'audio-bar',          // the audio player's controls
        'debug-header-bg', 'debug-card-bg',     // the traffic log stays legible
        'gate-bg', 'gate-input-bg',             // the API key gate
        'mic-record-bg',                        // recording state
        'danger-btn-bg',                        // a destructive action is never an outline
      ]) {
        expect(alphaOf(preset.colors, cssVar), `${themeId} ${cssVar}`).toBe(1);
      }
    }
  });

  it('leaves colours drawn over media alone', () => {
    for (const [themeId, preset] of clearPalettes()) {
      const solid = THEME_PRESETS_BY_ID[
        themeId.replace(/_clear$/, '') as keyof typeof THEME_PRESETS_BY_ID
      ].colors;
      for (const cssVar of ['scrim-modal', 'media-chip-bg', 'media-letterbox', 'game-deck-bg']) {
        expect(preset.colors[cssVar], `${themeId} ${cssVar}`).toBe(solid[cssVar]);
      }
    }
  });

  it('keeps hover feedback as a wash rather than removing it', () => {
    const clear = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE_CLEAR].colors;
    for (const cssVar of ['suggestion-hover', 'history-btn-hover', 'apikey-ok-hover']) {
      const alpha = alphaOf(clear, cssVar);
      expect(alpha, cssVar).toBeGreaterThan(0);
      expect(alpha, cssVar).toBeLessThan(0.5);
    }
    // A fill that stays solid keeps a solid hover to match.
    expect(alphaOf(clear, 'danger-btn-hover')).toBe(1);
  });

  it('leaves every foreground readable against the page', () => {
    for (const [themeId, preset] of clearPalettes()) {
      expect(unreadableTokens(preset.colors), themeId).toEqual([]);
    }
  });

  it('actually checks a meaningful number of foregrounds', () => {
    // Guards the guard: if the skip rules ever swallowed everything, the
    // readability test above would pass while proving nothing.
    const clear = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE_CLEAR].colors;
    const allPageColoured = Object.fromEntries(
      Object.entries(clear).map(([k, v]) => [k, k.endsWith('-bg') ? v : clear['page-bg']]),
    );
    expect(unreadableTokens(allPageColoured).length).toBeGreaterThan(50);
  });

  it('flags the regression it exists to prevent', () => {
    const clear = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE_CLEAR].colors;
    // Graphite's user bubble text is near-white because the bubble is dark.
    // Without repair it would land on near-white paper.
    const regressed = { ...clear, 'user-msg-text': clear['page-bg'] };
    expect(unreadableTokens(regressed).map(f => f.cssVar)).toContain('user-msg-text');
  });

  it('is idempotent', () => {
    const solid = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE].colors;
    const once = makeTransparentPalette(solid);
    expect(makeTransparentPalette(once)).toEqual(once);
  });

  it('preserves every token of the palette it derives from', () => {
    const solid = THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE].colors;
    expect(Object.keys(makeTransparentPalette(solid)).sort()).toEqual(Object.keys(solid).sort());
  });

  it('gives every theme a fingerprint no other theme shares', () => {
    // forwardFillThemeTokens picks the first match, so a collision would
    // silently fill a user's theme from the wrong palette.
    const fingerprints = ALL_THEMES.map(theme => {
      const { colors } = THEME_PRESETS_BY_ID[theme.themeId];
      return `${colors['page-bg']}|${colors['user-msg-bg']}`;
    });
    expect(new Set(fingerprints).size).toBe(ALL_THEMES.length);
  });
});

describe('contrast maths', () => {
  it('matches known WCAG ratios', () => {
    const white = parseHsl('0 0% 100%')!;
    const black = parseHsl('0 0% 0%')!;
    expect(contrastRatio(white, black)).toBeCloseTo(21, 1);
    expect(contrastRatio(white, white)).toBeCloseTo(1, 5);
  });

  it('leaves a colour alone when it already passes', () => {
    const page = parseHsl('0 0% 100%')!;
    const ink = parseHsl('220 30% 20%')!;
    expect(ensureContrast(ink, page, 4.5)).toEqual(ink);
  });

  it('keeps hue and saturation while fixing lightness', () => {
    const page = parseHsl('40 8% 97%')!;
    const tooLight = parseHsl('40 8% 97%')!;
    const fixed = ensureContrast(tooLight, page, 4.5);
    expect(fixed.h).toBe(tooLight.h);
    expect(fixed.s).toBe(tooLight.s);
    expect(contrastRatio(fixed, page)).toBeGreaterThanOrEqual(4.5);
  });

  it('finds the reachable direction on a mid-tone page', () => {
    const page = parseHsl('220 10% 50%')!;
    const fixed = ensureContrast(parseHsl('220 10% 52%')!, page, 4.5);
    expect(contrastRatio(fixed, page)).toBeGreaterThanOrEqual(4.5);
  });
});
