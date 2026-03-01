// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { DEFAULT_COLORS } from './defaultColors';

export interface PresetTheme {
  name: string;
  /** Short description for the user. */
  description: string;
  /** Partial set of color overrides. Only listed vars are changed; unlisted revert to defaults. */
  colors: Record<string, string>;
}

const BASE_PRESET_THEMES: PresetTheme[] = [
  {
    name: 'Notebook',
    description: 'The original hand-drawn look',
    colors: {},
  },
  {
    name: 'Original',
    description: 'The warm parchment look from the original app',
    colors: {
      'background': '39 35% 93%',
      'foreground': '25 15% 20%',
      'card': '39 40% 96%',
      'card-foreground': '25 15% 20%',
      'popover': '39 40% 96%',
      'popover-foreground': '25 15% 20%',
      'primary': '25 20% 25%',
      'primary-foreground': '39 35% 93%',
      'secondary': '35 25% 85%',
      'secondary-foreground': '25 15% 25%',
      'muted': '35 20% 88%',
      'muted-foreground': '25 10% 45%',
      'accent': '18 60% 55%',
      'accent-foreground': '39 35% 96%',
      'destructive': '0 65% 50%',
      'destructive-foreground': '39 35% 96%',
      'border': '25 18% 70%',
      'input': '25 18% 75%',
      'ring': '25 20% 35%',
      'paper': '39 35% 93%',
      'paper-dark': '35 12% 85%',
      'pencil': '25 15% 25%',
      'pencil-light': '25 10% 55%',
      'pencil-mark': '25 15% 25%',
      'sketch-shadow': '25 15% 20%',
      'eraser': '0 60% 65%',
      'watercolor': '200 45% 55%',
      'ink': '220 30% 20%',
      'highlight': '45 80% 75%',
      'highlight-text': '25 15% 20%',
      'correction': '0 65% 50%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Dark Mode',
    description: 'Sleek, modern, and perfectly balanced',
    colors: {
      'background': '222 47% 8%',
      'foreground': '210 40% 96%',
      'card': '222 47% 12%',
      'card-foreground': '210 40% 96%',
      'popover': '222 47% 12%',
      'popover-foreground': '210 40% 96%',
      'primary': '215 25% 18%',
      'primary-foreground': '0 0% 100%',
      'secondary': '215 28% 14%',
      'secondary-foreground': '210 40% 96%',
      'muted': '215 28% 14%',
      'muted-foreground': '215 20% 65%',
      'accent': '222 47% 10%',
      'accent-foreground': '210 40% 96%',
      'destructive': '0 84% 60%',
      'destructive-foreground': '0 0% 100%',
      'border': '222 47% 18%',
      'input': '222 47% 18%',
      'ring': '217 91% 60%',
      'paper': '222 47% 8%',
      'paper-dark': '222 47% 10%',
      'pencil': '210 40% 96%',
      'pencil-light': '215 20% 65%',
      'sketch-shadow': '222 47% 4%',
      'eraser': '0 84% 60%',
      'watercolor': '217 91% 60%',
      'ink': '210 40% 98%',
      'highlight': '45 93% 25%',
      'highlight-text': '45 93% 90%',
      'pencil-mark': '215 32% 50%',
      'correction': '0 84% 60%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Ocean',
    description: 'Calming blues and teals',
    colors: {
      'background': '200 30% 95%',
      'foreground': '210 40% 18%',
      'card': '200 35% 98%',
      'card-foreground': '210 40% 18%',
      'popover': '200 35% 98%',
      'popover-foreground': '210 40% 18%',
      'primary': '210 40% 18%',
      'primary-foreground': '200 30% 95%',
      'secondary': '200 20% 88%',
      'secondary-foreground': '210 30% 25%',
      'muted': '200 20% 90%',
      'muted-foreground': '210 15% 45%',
      'accent': '195 80% 42%',
      'accent-foreground': '200 30% 98%',
      'destructive': '350 65% 50%',
      'destructive-foreground': '200 30% 98%',
      'border': '200 20% 80%',
      'input': '200 20% 80%',
      'ring': '195 60% 40%',
      'paper': '200 30% 96%',
      'paper-dark': '200 20% 90%',
      'pencil': '210 30% 28%',
      'pencil-light': '210 20% 58%',
      'sketch-shadow': '210 30% 22%',
      'watercolor': '180 55% 48%',
      'ink': '210 40% 20%',
      'highlight': '55 80% 78%',
      'highlight-text': '210 40% 18%',
      'correction': '350 65% 50%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Forest',
    description: 'Earthy greens and browns',
    colors: {
      'background': '80 15% 95%',
      'foreground': '90 25% 18%',
      'card': '80 18% 98%',
      'card-foreground': '90 25% 18%',
      'popover': '80 18% 98%',
      'popover-foreground': '90 25% 18%',
      'primary': '90 25% 20%',
      'primary-foreground': '80 15% 95%',
      'secondary': '80 12% 88%',
      'secondary-foreground': '90 20% 28%',
      'muted': '80 12% 90%',
      'muted-foreground': '80 10% 45%',
      'accent': '140 55% 38%',
      'accent-foreground': '80 15% 98%',
      'destructive': '15 70% 48%',
      'destructive-foreground': '80 15% 98%',
      'border': '80 12% 80%',
      'input': '80 12% 80%',
      'ring': '140 40% 35%',
      'paper': '80 15% 96%',
      'paper-dark': '80 12% 90%',
      'pencil': '90 20% 28%',
      'pencil-light': '90 12% 55%',
      'sketch-shadow': '90 20% 22%',
      'watercolor': '160 45% 45%',
      'ink': '90 30% 18%',
      'highlight': '50 75% 75%',
      'highlight-text': '90 25% 18%',
      'correction': '15 70% 48%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Sunset',
    description: 'Warm oranges and pinks',
    colors: {
      'background': '30 25% 96%',
      'foreground': '20 35% 18%',
      'card': '30 30% 98%',
      'card-foreground': '20 35% 18%',
      'popover': '30 30% 98%',
      'popover-foreground': '20 35% 18%',
      'primary': '20 35% 20%',
      'primary-foreground': '30 25% 96%',
      'secondary': '30 15% 88%',
      'secondary-foreground': '20 25% 28%',
      'muted': '30 15% 90%',
      'muted-foreground': '20 12% 48%',
      'accent': '15 80% 55%',
      'accent-foreground': '30 25% 98%',
      'destructive': '350 70% 50%',
      'destructive-foreground': '30 25% 98%',
      'border': '30 15% 80%',
      'input': '30 15% 80%',
      'ring': '15 60% 45%',
      'paper': '30 25% 96%',
      'paper-dark': '30 15% 90%',
      'pencil': '20 25% 28%',
      'pencil-light': '20 15% 58%',
      'sketch-shadow': '20 25% 22%',
      'watercolor': '340 50% 55%',
      'ink': '20 35% 18%',
      'highlight': '45 90% 78%',
      'highlight-text': '20 35% 18%',
      'correction': '350 70% 50%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'High Contrast',
    description: 'Maximum readability',
    colors: {
      'background': '0 0% 100%',
      'foreground': '0 0% 5%',
      'card': '0 0% 98%',
      'card-foreground': '0 0% 5%',
      'popover': '0 0% 98%',
      'popover-foreground': '0 0% 5%',
      'primary': '0 0% 5%',
      'primary-foreground': '0 0% 100%',
      'secondary': '0 0% 90%',
      'secondary-foreground': '0 0% 10%',
      'muted': '0 0% 92%',
      'muted-foreground': '0 0% 35%',
      'accent': '220 90% 50%',
      'accent-foreground': '0 0% 100%',
      'destructive': '0 90% 45%',
      'destructive-foreground': '0 0% 100%',
      'border': '0 0% 60%',
      'input': '0 0% 60%',
      'ring': '220 90% 50%',
      'paper': '0 0% 100%',
      'paper-dark': '0 0% 92%',
      'pencil': '0 0% 15%',
      'pencil-light': '0 0% 50%',
      'sketch-shadow': '0 0% 10%',
      'watercolor': '200 80% 45%',
      'ink': '0 0% 5%',
      'highlight': '55 100% 70%',
      'highlight-text': '0 0% 5%',
      'correction': '0 90% 45%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Lavender',
    description: 'Soft purple tones',
    colors: {
      'background': '270 20% 96%',
      'foreground': '270 30% 18%',
      'card': '270 25% 98%',
      'card-foreground': '270 30% 18%',
      'popover': '270 25% 98%',
      'popover-foreground': '270 30% 18%',
      'primary': '270 30% 20%',
      'primary-foreground': '270 20% 96%',
      'secondary': '270 15% 88%',
      'secondary-foreground': '270 20% 28%',
      'muted': '270 15% 90%',
      'muted-foreground': '270 10% 48%',
      'accent': '270 65% 55%',
      'accent-foreground': '270 20% 98%',
      'destructive': '350 65% 50%',
      'destructive-foreground': '270 20% 98%',
      'border': '270 15% 82%',
      'input': '270 15% 82%',
      'ring': '270 45% 45%',
      'paper': '270 20% 96%',
      'paper-dark': '270 15% 90%',
      'pencil': '270 20% 30%',
      'pencil-light': '270 12% 60%',
      'sketch-shadow': '270 20% 22%',
      'watercolor': '290 50% 55%',
      'ink': '270 30% 18%',
      'highlight': '50 80% 78%',
      'highlight-text': '270 30% 18%',
      'correction': '350 65% 50%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
  {
    name: 'Gemini Spark',
    description: 'Warm parchment illuminated by Gemini AI starlight',
    colors: {
      'background': '39 35% 93%', 
      'foreground': '250 30% 20%', 
      'card': '39 40% 96%', 
      'card-foreground': '250 30% 20%',
      'popover': '39 40% 96%',
      'popover-foreground': '250 30% 20%',
      'primary': '250 40% 25%', 
      'primary-foreground': '39 35% 93%',
      'secondary': '35 25% 85%', 
      'secondary-foreground': '250 30% 20%',
      'muted': '35 20% 88%', 
      'muted-foreground': '250 20% 45%',
      'accent': '260 85% 65%', 
      'accent-foreground': '0 0% 100%',
      'destructive': '350 70% 55%',
      'destructive-foreground': '0 0% 100%',
      'border': '260 15% 75%',
      'input': '260 15% 75%',
      'ring': '200 85% 55%', 
      'paper': '39 35% 93%', 
      'paper-dark': '35 12% 85%',
      'pencil': '250 40% 30%', 
      'pencil-light': '250 20% 60%',
      'sketch-shadow': '250 30% 15%',
      'eraser': '280 80% 65%', 
      'watercolor': '200 85% 55%', 
      'ink': '250 50% 20%', 
      'highlight': '280 80% 85%',
      'highlight-text': '250 30% 20%',
      'pencil-mark': '250 40% 30%',
      'correction': '350 60% 55%',
      'api-key-valid-bg': '161 94% 30%',
      'api-key-valid-text': '0 0% 100%',
      'api-key-missing-bg': '347 77% 50%',
      'api-key-missing-text': '0 0% 100%',
    },
  },
];

interface HslTriplet {
  h: number;
  s: number;
  l: number;
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

const parseHslTriplet = (value: string): HslTriplet | null => {
  const match = value.trim().match(/^(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)%\s+(-?\d+(?:\.\d+)?)%$/);
  if (!match) return null;
  return {
    h: Number(match[1]),
    s: Number(match[2]),
    l: Number(match[3]),
  };
};

const formatHslTriplet = ({ h, s, l }: HslTriplet): string => {
  const hue = ((h % 360) + 360) % 360;
  return `${Math.round(hue)} ${Math.round(clamp(s, 0, 100))}% ${Math.round(clamp(l, 0, 100))}%`;
};

const tune = (
  source: string,
  { hue = 0, saturation = 0, lightness = 0 }: { hue?: number; saturation?: number; lightness?: number }
): string => {
  const parsed = parseHslTriplet(source);
  if (!parsed) return source;
  return formatHslTriplet({
    h: parsed.h + hue,
    s: parsed.s + saturation,
    l: parsed.l + lightness,
  });
};

const pick = (colors: Record<string, string>, ...keys: string[]): string => {
  for (const key of keys) {
    if (colors[key]) return colors[key];
    if (DEFAULT_COLORS[key]) return DEFAULT_COLORS[key];
  }
  return '0 0% 50%';
};

const withDedicatedUiStateColors = (baseColors: Record<string, string>): Record<string, string> => {
  if (Object.keys(baseColors).length === 0) return baseColors;

  const vivid = pick(baseColors, 'ring', 'accent');
  const vividText = pick(baseColors, 'accent-foreground', 'primary-foreground');
  const paper = pick(baseColors, 'paper', 'background');
  const pencil = pick(baseColors, 'pencil');
  const secondary = pick(baseColors, 'secondary');
  const muted = pick(baseColors, 'muted');
  const mutedText = pick(baseColors, 'muted-foreground', 'secondary-foreground');
  const border = pick(baseColors, 'border');
  const watercolor = pick(baseColors, 'watercolor');
  const correction = pick(baseColors, 'correction', 'destructive');
  const correctionText = pick(baseColors, 'destructive-foreground', 'accent-foreground');
  const apiKeyValidBg = pick(baseColors, 'api-key-valid-bg');
  const apiKeyMissingBg = pick(baseColors, 'api-key-missing-bg');
  const liveErrorBase = tune(vivid, { lightness: 3, saturation: 6 });
  const trimBase = pick(baseColors, 'action-trim', 'accent', 'watercolor');
  const dangerBase = pick(baseColors, 'action-danger', 'destructive', 'correction');

  const derivedColors: Record<string, string> = {
    // Status flag states
    'status-hold-bg': tune(vivid, { hue: 68, saturation: 8, lightness: 4 }),
    'status-hold-border': tune(vivid, { hue: 68, saturation: 12, lightness: -2 }),
    'status-hold-text': vividText,
    'status-speaking-bg': vivid,
    'status-speaking-border': tune(vivid, { saturation: 6, lightness: -6 }),
    'status-speaking-text': vividText,
    'status-typing-bg': tune(vivid, { saturation: 8, lightness: -11 }),
    'status-typing-border': tune(vivid, { saturation: 10, lightness: -15 }),
    'status-typing-text': vividText,
    'status-listening-bg': pencil,
    'status-listening-border': tune(pencil, { saturation: 4, lightness: -8 }),
    'status-listening-text': paper,
    'status-observing-bg': secondary,
    'status-observing-border': tune(secondary, { saturation: 2, lightness: -8 }),
    'status-observing-text': mutedText,
    'status-observing-high-bg': tune(vivid, { saturation: 4, lightness: -4 }),
    'status-observing-high-border': tune(vivid, { saturation: 8, lightness: -10 }),
    'status-observing-high-text': vividText,
    'status-idle-bg': muted,
    'status-idle-border': border,
    'status-idle-text': mutedText,
    'status-busy-bg': watercolor,
    'status-busy-border': tune(watercolor, { saturation: 6, lightness: -8 }),
    'status-busy-text': tune(watercolor, { saturation: 10, lightness: -12 }),

    // API key button
    'api-key-valid-hover-bg': tune(apiKeyValidBg, { saturation: 2, lightness: -6 }),
    'api-key-missing-hover-bg': tune(apiKeyMissingBg, { saturation: 2, lightness: -6 }),

    // Microphone / recording controls
    'recording-mic-armed-bg': correction,
    'recording-mic-armed-text': correctionText,
    'recording-mic-armed-ring': tune(correction, { saturation: 4, lightness: -5 }),
    'recording-mic-listening-bg': tune(correction, { saturation: -6, lightness: 5 }),
    'recording-mic-listening-text': correctionText,
    'recording-mic-pulse-outer': correction,
    'recording-mic-pulse-inner': tune(correction, { saturation: -10, lightness: 10 }),
    'recording-live-chip-bg': tune(correction, { saturation: 4, lightness: -2 }),
    'recording-live-chip-text': correctionText,
    'recording-live-chip-dot': correctionText,
    'recording-live-stop-bg': tune(correction, { saturation: 6, lightness: -4 }),
    'recording-live-stop-hover-bg': tune(correction, { saturation: 8, lightness: -10 }),
    'recording-live-stop-text': correctionText,
    'recording-live-stop-icon': correctionText,
    'recording-local-stop-bg': tune(correction, { saturation: -4, lightness: 3 }),
    'recording-local-stop-hover-bg': tune(correction, { saturation: 2, lightness: -3 }),
    'recording-local-stop-text': correctionText,
    'recording-local-stop-icon': correctionText,
    'recording-remove-bg': tune(correction, { saturation: -2, lightness: 1 }),
    'recording-remove-hover-bg': tune(correction, { saturation: 4, lightness: -6 }),
    'recording-remove-text': correctionText,
    'recording-indicator-dot': tune(correction, { saturation: 6, lightness: -2 }),
    'recording-inline-error-bg': tune(correction, { saturation: -4, lightness: 2 }),
    'recording-inline-error-text': correctionText,

    // Live session action buttons
    'live-session-button-active-bg': tune(correction, { hue: -8, saturation: 2, lightness: -4 }),
    'live-session-button-active-hover-bg': tune(correction, { hue: -8, saturation: 4, lightness: -10 }),
    'live-session-button-active-text': correctionText,
    'live-session-button-error-bg': liveErrorBase,
    'live-session-button-error-hover-bg': tune(liveErrorBase, { saturation: 2, lightness: -6 }),
    'live-session-button-error-text': pick(baseColors, 'foreground'),
    'live-overlay-button-error-bg': tune(liveErrorBase, { saturation: -4, lightness: 4 }),
    'live-overlay-button-error-hover-bg': tune(liveErrorBase, { saturation: 2, lightness: -2 }),
    'live-overlay-button-error-text': vividText,

    // Session shortcut hover states
    'action-danger-shortcut-hover-bg': tune(dangerBase, { saturation: 4, lightness: -2 }),
    'action-danger-shortcut-hover-text': pick(baseColors, 'action-danger-text', 'destructive-foreground', 'accent-foreground'),
    'action-trim-shortcut-hover-bg': tune(trimBase, { saturation: 4, lightness: -2 }),
    'action-trim-shortcut-hover-text': pick(baseColors, 'action-trim-text', 'accent-foreground'),
  };

  return {
    ...derivedColors,
    ...baseColors,
  };
};

export const PRESET_THEMES: PresetTheme[] = BASE_PRESET_THEMES.map((preset) => ({
  ...preset,
  colors: withDedicatedUiStateColors(preset.colors),
}));

