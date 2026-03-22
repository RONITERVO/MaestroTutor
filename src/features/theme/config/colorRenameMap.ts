// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { PURCHASABLE_THEME_PRESETS } from './purchasableThemePresets';
import type { ThemeProductId } from './themeProducts';

/**
 * Old color key -> new color key(s).
 *
 * Split mappings intentionally fan one legacy value out to multiple
 * per-element tokens so old custom themes still render after migration.
 */
export const COLOR_RENAME_MAP: Record<string, string[]> = {
  // Core palette (legacy shared tokens)
  background: ['page-bg'],
  foreground: ['page-text', 'ai-msg-text', 'status-msg-text', 'sugg-input-text', 'bookmark-input-text', 'gate-text', 'theme-panel-text'],
  paper: ['paper-surface', 'input-error-text', 'profile-accept-text', 'annotation-btn-text'],
  'paper-dark': ['paper-stripe', 'suggestion-hover'],
  ink: ['deep-ink'],

  primary: ['user-msg-bg', 'chat-input-bg', 'profile-btn-bg', 'mode-toggle-bg', 'debug-btn-bg', 'live-idle-btn-bg'],
  'primary-foreground': ['user-msg-text', 'chat-input-text', 'chat-input-icon', 'profile-btn-text', 'mode-toggle-text', 'debug-btn-text', 'live-idle-btn-text'],

  secondary: ['status-msg-bg', 'thinking-bubble-bg', 'ai-file-bg', 'sugg-outer-bg', 'history-peek-bg', 'history-btn-hover', 'web-results-bg', 'audio-player-bg', 'suggestion-bg', 'media-sugg-bg'],
  'secondary-foreground': ['status-msg-text'],

  accent: ['chat-outer-bg', 'delete-msg-bg', 'save-sugg-bg', 'audio-play-btn', 'audio-bar', 'bookmark-bg', 'suggestion-ring', 'suggestion-active-bg', 'media-chat-bg', 'media-empty-bg', 'gate-btn-bg', 'gate-accent', 'cta-btn-bg', 'camera-toggle-text'],
  'accent-foreground': ['chat-outer-text', 'delete-msg-text', 'save-sugg-text', 'clear-sugg-text', 'audio-play-text', 'bookmark-text', 'suggestion-active-text', 'media-empty-text', 'gate-btn-text', 'cta-btn-text'],

  card: ['ai-msg-bg', 'sugg-input-bg', 'send-btn-bg', 'history-btn-bg', 'bookmark-input-bg', 'gate-bg', 'gate-input-bg', 'theme-panel-bg', 'theme-input-bg', 'theme-preset-btn'],
  'card-foreground': ['ai-msg-text'],
  popover: ['theme-panel-bg'],
  'popover-foreground': ['theme-panel-text'],

  muted: ['theme-panel-bg'],
  'muted-foreground': ['thinking-bubble-text', 'ai-file-text', 'sugg-input-icon', 'audio-time-text', 'ctrl-muted-text', 'debug-btn-muted', 'gate-muted-text', 'theme-muted-text'],

  destructive: ['error-msg-bg', 'error-msg-text', 'gate-error-text'],
  'destructive-foreground': ['gate-btn-text'],

  border: ['line-border'],
  input: ['input-outline'],
  ring: ['focus-ring'],

  pencil: ['pencil-stroke', 'pencil-emphasis', 'ai-msg-placeholder', 'profile-accept-bg', 'save-chat-text', 'annotation-btn-bg'],
  'pencil-light': ['sketch-line', 'input-focus-ring', 'history-peek-icon', 'bookmark-divider', 'loading-spinner', 'theme-input-border'],
  'pencil-mark': ['pencil-emphasis'],
  watercolor: ['watercolor-wash', 'web-results-link', 'profile-label-text', 'profile-input-accent'],
  highlight: ['marker-target-bg', 'marker-native-bg'],
  'highlight-text': ['marker-target-text', 'marker-native-text'],
  'marker-bg': ['marker-target-bg', 'marker-native-bg'],
  'marker-text': ['marker-target-text', 'marker-native-text'],
  correction: ['correction-pen', 'img-error-text', 'input-error-bg'],
  eraser: ['clear-sugg-bg'],
  'sketch-shadow': ['sketch-shadow'],

  // Maestro flag rename
  'status-hold-bg': ['flag-hold-bg'],
  'status-hold-border': ['flag-hold-border'],
  'status-hold-text': ['flag-hold-text'],
  'status-speaking-bg': ['flag-speaking-bg'],
  'status-speaking-border': ['flag-speaking-border'],
  'status-speaking-text': ['flag-speaking-text'],
  'status-typing-bg': ['flag-typing-bg'],
  'status-typing-border': ['flag-typing-border'],
  'status-typing-text': ['flag-typing-text'],
  'status-listening-bg': ['flag-listening-bg'],
  'status-listening-border': ['flag-listening-border'],
  'status-listening-text': ['flag-listening-text'],
  'status-observing-bg': ['flag-observing-bg'],
  'status-observing-border': ['flag-observing-border'],
  'status-observing-text': ['flag-observing-text'],
  'status-observing-high-bg': ['flag-engaging-bg'],
  'status-observing-high-border': ['flag-engaging-border'],
  'status-observing-high-text': ['flag-engaging-text'],
  'status-idle-bg': ['flag-idle-bg'],
  'status-idle-border': ['flag-idle-border'],
  'status-idle-text': ['flag-idle-text'],
  'status-busy-bg': ['flag-busy-bg'],
  'status-busy-border': ['flag-busy-border'],
  'status-busy-text': ['flag-busy-text'],

  // API key button rename
  'api-key-valid-bg': ['apikey-ok-bg'],
  'api-key-valid-hover-bg': ['apikey-ok-hover'],
  'api-key-valid-text': ['apikey-ok-text'],
  'api-key-missing-bg': ['apikey-missing-bg'],
  'api-key-missing-hover-bg': ['apikey-missing-hover'],
  'api-key-missing-text': ['apikey-missing-text'],

  // Mic rename
  'recording-mic-armed-bg': ['mic-record-bg'],
  'recording-mic-armed-text': ['mic-record-icon'],
  'recording-mic-armed-ring': ['mic-record-ring'],
  'recording-mic-listening-bg': ['mic-stt-bg'],
  'recording-mic-listening-text': ['mic-stt-icon'],
  'recording-mic-pulse-outer': ['mic-pulse-outer'],
  'recording-mic-pulse-inner': ['mic-pulse-inner'],

  // Live/video controls rename
  'recording-live-chip-bg': ['live-badge-bg'],
  'recording-live-chip-text': ['live-badge-text'],
  'recording-live-chip-dot': ['live-badge-dot'],
  'recording-live-stop-bg': ['live-stop-bg'],
  'recording-live-stop-hover-bg': ['live-stop-hover'],
  'recording-live-stop-text': ['live-stop-text'],
  'recording-live-stop-icon': ['live-stop-icon'],
  'recording-local-stop-bg': ['vid-stop-bg'],
  'recording-local-stop-hover-bg': ['vid-stop-hover'],
  'recording-local-stop-text': ['vid-stop-text'],
  'recording-local-stop-icon': ['vid-stop-icon'],
  'recording-remove-bg': ['remove-attach-bg'],
  'recording-remove-hover-bg': ['remove-attach-hover'],
  'recording-remove-text': ['remove-attach-icon'],
  'recording-indicator-dot': ['rec-dot'],
  'recording-inline-error-bg': ['rec-error-bg'],
  'recording-inline-error-text': ['rec-error-text'],

  'live-session-button-active-bg': ['top-live-active-bg'],
  'live-session-button-active-hover-bg': ['top-live-active-hover'],
  'live-session-button-active-text': ['top-live-active-text'],
  'live-session-button-error-bg': ['top-live-error-bg'],
  'live-session-button-error-hover-bg': ['top-live-error-hover'],
  'live-session-button-error-text': ['top-live-error-text'],
  'live-overlay-button-error-bg': ['overlay-live-error-bg'],
  'live-overlay-button-error-hover-bg': ['overlay-live-error-hover'],
  'live-overlay-button-error-text': ['overlay-live-error-text'],

  // Action panel rename
  'action-load': ['action-load-bg'],
  'action-load-text': ['action-load-text'],
  'action-danger': ['action-delete-bg'],
  'action-danger-text': ['action-delete-text'],
  'action-export': ['action-export-bg'],
  'action-export-text': ['action-export-text'],
  'action-combine': ['action-combine-bg'],
  'action-combine-text': ['action-combine-text'],
  'action-trim': ['action-trim-bg'],
  'action-trim-text': ['action-trim-text'],
  'action-danger-shortcut-hover-bg': ['delete-shortcut-hover-bg'],
  'action-danger-shortcut-hover-text': ['delete-shortcut-hover-text'],
  'action-trim-shortcut-hover-bg': ['trim-shortcut-hover-bg'],
  'action-trim-shortcut-hover-text': ['trim-shortcut-hover-text'],
};

const LEGACY_KEYS = new Set(Object.keys(COLOR_RENAME_MAP));

const DERIVED_COLOR_FALLBACKS: Array<{ source: string; targets: string[] }> = [
  {
    source: 'ai-msg-text',
    targets: ['attachment-inline-target-text', 'attachment-game-target-text'],
  },
  {
    source: 'ai-file-text',
    targets: ['attachment-inline-native-text', 'attachment-game-native-text'],
  },
  {
    source: 'attachment-inline-target-text',
    targets: ['attachment-audio-target-text'],
  },
  {
    source: 'attachment-inline-native-text',
    targets: ['attachment-audio-native-text'],
  },
  {
    source: 'attachment-game-target-text',
    targets: ['attachment-svg-target-text'],
  },
  {
    source: 'attachment-game-native-text',
    targets: ['attachment-svg-native-text'],
  },
  {
    source: 'user-msg-text',
    targets: [
      'user-attachment-inline-text',
      'user-attachment-audio-text',
      'user-attachment-overlay-text',
      'user-attachment-svg-text',
      'user-attachment-game-text',
      'attachment-overlay-target-text',
      'attachment-overlay-native-text',
    ],
  },
];

const expandDerivedColorTokens = (colors: Record<string, string>): Record<string, string> => {
  const expanded = { ...colors };

  for (const { source, targets } of DERIVED_COLOR_FALLBACKS) {
    const sourceValue = expanded[source];
    if (typeof sourceValue !== 'string' || !sourceValue.trim()) continue;

    for (const target of targets) {
      if (!(target in expanded)) {
        expanded[target] = sourceValue;
      }
    }
  }

  return expanded;
};

const hasSameEntries = (left: Record<string, string>, right?: Record<string, string>): boolean => {
  if (!right) return false;
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  if (leftKeys.length !== rightKeys.length) return false;
  return leftKeys.every((key) => left[key] === right[key]);
};

export const hasLegacyColorKeys = (colors?: Record<string, string>): boolean => {
  if (!colors) return false;
  return Object.keys(colors).some((key) => LEGACY_KEYS.has(key));
};

export const migrateLegacyColorMap = (colors?: Record<string, string>): Record<string, string> => {
  if (!colors) return {};

  const migrated: Record<string, string> = {};

  // Pass 1: preserve modern keys as-is.
  for (const [key, value] of Object.entries(colors)) {
    if (!LEGACY_KEYS.has(key) && typeof value === 'string') {
      migrated[key] = value;
    }
  }

  // Pass 2: fan out legacy keys to modern destinations if those are not set yet.
  for (const [legacyKey, value] of Object.entries(colors)) {
    if (typeof value !== 'string') continue;
    const targets = COLOR_RENAME_MAP[legacyKey];
    if (!targets || targets.length === 0) continue;

    for (const targetKey of targets) {
      if (!(targetKey in migrated)) {
        migrated[targetKey] = value;
      }
    }
  }

  const expanded = expandDerivedColorTokens(migrated);
  return hasSameEntries(expanded, colors) ? colors : expanded;
};

// ---------------------------------------------------------------------------
// Theme forward-fill: auto-populate tokens added to paid themes after user
// applied the theme. Runs after rename migration on app startup.
// ---------------------------------------------------------------------------

/**
 * Two highly distinctive tokens per paid theme. Both must match exactly for
 * the theme to be identified — reduces false-positive risk to near zero.
 */
const PAID_THEME_FINGERPRINTS: Record<ThemeProductId, readonly [string, string][]> = {
  theme_ocean_blue:  [['page-bg', '204 46% 95%'], ['user-msg-bg', '204 69% 39%']],
  theme_sunset_gold: [['page-bg', '36 67% 95%'],  ['user-msg-bg', '24 70% 46%']],
  theme_dark_neon:   [['page-bg', '230 24% 9%'],  ['user-msg-bg', '282 78% 56%']],
  theme_scholar:     [['page-bg', '39 37% 94%'],  ['user-msg-bg', '248 41% 27%']],
  theme_pure_light:  [['page-bg', '210 25% 98%'], ['user-msg-bg', '222 47% 20%']],
  theme_obsidian:    [['page-bg', '222 38% 8%'],  ['user-msg-bg', '214 80% 46%']],
  theme_forest:      [['page-bg', '80 15% 95%'],  ['user-msg-bg', '90 25% 20%']],
  theme_lavender:    [['page-bg', '267 35% 97%'], ['user-msg-bg', '262 52% 24%']],
  theme_spectrum:    [['page-bg', '0 0% 98%'],    ['user-msg-bg', '217 60% 26%']],
  theme_graphite:    [['page-bg', '40 8% 97%'],   ['user-msg-bg', '220 8% 14%']],
};

const detectPaidThemeId = (colors: Record<string, string>): ThemeProductId | null => {
  const entries = Object.entries(PAID_THEME_FINGERPRINTS) as [ThemeProductId, readonly [string, string][]][];
  for (const [themeId, fingerprint] of entries) {
    if (fingerprint.every(([token, value]) => colors[token] === value)) {
      return themeId;
    }
  }
  return null;
};

/**
 * Fills in any tokens that are defined in the user's active paid theme but
 * absent from their saved customColors (i.e. tokens added to the theme after
 * the user first applied it). Existing user values are never overwritten.
 *
 * Only applied to the live customColors map — saved user presets are left
 * untouched because they are intentional snapshots.
 */
export const forwardFillThemeTokens = (colors: Record<string, string>): Record<string, string> => {
  if (!colors || Object.keys(colors).length === 0) return colors;

  const themeId = detectPaidThemeId(colors);
  if (!themeId) return colors;

  const preset = PURCHASABLE_THEME_PRESETS[themeId];
  if (!preset) return colors;

  let changed = false;
  const filled = { ...colors };
  for (const [token, value] of Object.entries(preset.colors)) {
    if (!(token in filled)) {
      filled[token] = value;
      changed = true;
    }
  }
  return changed ? filled : colors;
};
