// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

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
    colors: {
      // Page Canvas
      'page-bg': '210 20% 97%',
      'page-text': '220 30% 20%',
      'paper-surface': '210 20% 97%',
      'paper-stripe': '210 15% 92%',
      'deep-ink': '230 40% 20%',

      // Chat Message Bubbles
      'user-msg-bg': '220 30% 20%',
      'user-msg-text': '210 20% 98%',
      'ai-msg-bg': '210 25% 99%',
      'ai-msg-text': '220 30% 20%',
      'status-msg-bg': '210 15% 90%',
      'status-msg-text': '220 30% 20%',
      'error-msg-bg': '350 70% 50%',
      'error-msg-text': '350 70% 50%',
      'thinking-bubble-bg': '210 15% 90%',
      'thinking-bubble-text': '220 15% 45%',

      // Message Sub-elements
      'ai-msg-placeholder': '220 25% 30%',
      'ai-file-bg': '210 15% 90%',
      'ai-file-text': '220 15% 45%',
      'img-error-text': '350 70% 50%',

      // Chat Input Area
      'chat-input-bg': '220 30% 20%',
      'chat-input-text': '210 20% 98%',
      'chat-input-icon': '210 20% 98%',
      'chat-input-icon-hover-bg': '0 0% 100%',
      'sugg-input-bg': '210 25% 99%',
      'sugg-input-text': '220 30% 20%',
      'sugg-input-icon': '220 15% 45%',
      'send-btn-bg': '210 25% 99%',
      'send-btn-text': '220 30% 20%',
      'send-sugg-btn-bg': '210 25% 99%',
      'send-sugg-btn-text': '220 30% 20%',
      'input-focus-ring': '220 15% 65%',
      'input-error-bg': '350 70% 50%',
      'input-error-text': '210 20% 97%',
      'snapshot-error-bg': '220 25% 30%',
      'chat-outer-bg': '220 70% 55%',
      'chat-outer-text': '210 25% 99%',
      'sugg-outer-bg': '210 15% 90%',

      // Chat Interface Chrome
      'history-peek-bg': '210 15% 90%',
      'history-peek-icon': '220 15% 65%',
      'history-btn-bg': '210 25% 99%',
      'history-btn-hover': '210 15% 90%',
      'delete-msg-bg': '220 70% 55%',
      'delete-msg-text': '210 25% 99%',
      'save-sugg-bg': '220 70% 55%',
      'save-sugg-text': '210 25% 99%',
      'clear-sugg-bg': '350 70% 65%',
      'clear-sugg-text': '210 25% 99%',
      'web-results-bg': '210 15% 90%',
      'web-results-link': '190 60% 55%',

      // Audio Player
      'audio-player-bg': '210 15% 90%',
      'audio-play-btn': '220 70% 55%',
      'audio-play-text': '210 25% 99%',
      'audio-bar': '220 70% 55%',
      'audio-time-text': '220 15% 45%',

      // Bookmark Actions
      'bookmark-bg': '220 70% 55%',
      'bookmark-text': '210 25% 99%',
      'bookmark-input-bg': '210 25% 99%',
      'bookmark-input-text': '220 30% 20%',
      'bookmark-divider': '220 15% 65%',

      // Suggestions List
      'suggestion-bg': '210 15% 90%',
      'suggestion-hover': '210 15% 92%',
      'suggestion-ring': '220 70% 55%',
      'suggestion-double-ring': '220 70% 55%',
      'suggestion-active-bg': '220 70% 55%',
      'suggestion-active-text': '210 25% 99%',

      // Session Controls
      'profile-label-text': '190 60% 55%',
      'profile-input-accent': '190 60% 55%',
      'scroll-wheel-target-accent': '142 71% 45%',
      'globe-native-accent': '190 60% 55%',
      'globe-target-accent': '142 71% 60%',
      'maestro-avatar-glow': '22 53% 49%',
      'profile-btn-bg': '220 30% 20%',
      'profile-btn-text': '210 20% 98%',
      'profile-accept-bg': '220 25% 30%',
      'profile-accept-text': '210 20% 97%',
      'mode-toggle-bg': '220 30% 20%',
      'mode-toggle-text': '210 20% 98%',
      'save-chat-text': '220 25% 30%',
      'ctrl-muted-text': '220 15% 45%',

      // Header
      'debug-btn-bg': '220 30% 20%',
      'debug-btn-text': '210 20% 98%',
      'debug-btn-muted': '220 15% 45%',
      'loading-spinner': '220 15% 65%',

      // Live Session Idle Button
      'live-idle-btn-bg': '220 30% 20%',
      'live-idle-btn-text': '210 20% 98%',
      'live-idle-sugg-btn-bg': '210 15% 90%',
      'live-idle-sugg-btn-text': '220 30% 20%',
      'live-idle-spinner': '210 20% 98%',

      // Media Attachments
      'media-chat-bg': '220 70% 55%',
      'media-sugg-bg': '210 15% 90%',
      'media-empty-bg': '220 70% 55%',
      'media-empty-text': '210 25% 99%',
      'camera-toggle-text': '220 70% 55%',

      // API Key Gate
      'gate-bg': '210 25% 99%',
      'gate-text': '220 30% 20%',
      'gate-muted-text': '220 15% 45%',
      'gate-input-bg': '210 25% 99%',
      'gate-btn-bg': '220 70% 55%',
      'gate-btn-text': '210 25% 99%',
      'gate-error-text': '350 70% 50%',
      'gate-accent': '220 70% 55%',

      // Theme Customizer
      'theme-panel-bg': '210 25% 99%',
      'theme-panel-text': '220 30% 20%',
      'theme-muted-text': '220 15% 45%',
      'theme-input-bg': '210 25% 99%',
      'theme-input-border': '220 15% 65%',
      'theme-preset-btn': '210 25% 99%',

      // CTA Buttons in Messages
      'cta-btn-bg': '220 70% 55%',
      'cta-btn-text': '210 25% 99%',

      // Annotation Save Button
      'annotation-btn-bg': '220 25% 30%',
      'annotation-btn-text': '210 20% 97%',
      'annotation-btn-hover': '220 25% 24%',
      'annotation-btn-focus': '220 25% 30%',

      // Translation Highlight
      'marker-bg': '60 85% 80%',
      'marker-text': '220 30% 20%',

      // Notebook Marks
      'pencil-stroke': '220 25% 30%',
      'pencil-emphasis': '220 25% 30%',
      'sketch-line': '220 15% 65%',
      'sketch-shadow': '220 30% 20%',
      'watercolor-wash': '190 60% 55%',
      'correction-pen': '350 70% 50%',

      // Global Borders & Focus
      'line-border': '210 15% 82%',
      'input-outline': '210 15% 82%',
      'focus-ring': '220 40% 40%',

      // Maestro Flag
      'flag-hold-bg': '292 84% 61%',
      'flag-hold-border': '292 84% 61%',
      'flag-hold-text': '0 0% 100%',
      'flag-speaking-bg': '220 70% 55%',
      'flag-speaking-border': '220 70% 55%',
      'flag-speaking-text': '210 25% 99%',
      'flag-typing-bg': '220 70% 49%',
      'flag-typing-border': '220 70% 49%',
      'flag-typing-text': '210 25% 99%',
      'flag-listening-bg': '220 25% 30%',
      'flag-listening-border': '220 25% 30%',
      'flag-listening-text': '210 20% 97%',
      'flag-observing-bg': '210 15% 90%',
      'flag-observing-border': '210 15% 82%',
      'flag-observing-text': '220 15% 45%',
      'flag-engaging-bg': '220 68% 53%',
      'flag-engaging-border': '220 68% 53%',
      'flag-engaging-text': '210 25% 99%',
      'flag-idle-bg': '210 15% 92%',
      'flag-idle-border': '210 15% 82%',
      'flag-idle-text': '220 15% 45%',
      'flag-busy-bg': '190 60% 55%',
      'flag-busy-border': '190 60% 55%',
      'flag-busy-text': '190 60% 55%',

      // Action Confirmation Panels
      'action-load-bg': '217 91% 60%',
      'action-load-text': '214 95% 93%',
      'action-delete-bg': '0 72% 51%',
      'action-delete-text': '0 86% 97%',
      'action-export-bg': '188 95% 43%',
      'action-export-text': '188 100% 94%',
      'action-combine-bg': '263 70% 50%',
      'action-combine-text': '263 70% 93%',
      'action-trim-bg': '25 95% 53%',
      'action-trim-text': '33 100% 96%',
      'delete-shortcut-hover-bg': '0 75% 54%',
      'delete-shortcut-hover-text': '0 86% 97%',
      'trim-shortcut-hover-bg': '28 95% 56%',
      'trim-shortcut-hover-text': '34 100% 97%',

      // Voice Character Identity
      'voice-zephyr': '188 79% 41%',
      'voice-puck': '43 96% 56%',
      'voice-charon': '220 15% 65%',
      'voice-kore': '217 91% 60%',
      'voice-fenrir': '350 70% 50%',

      // API Key Button
      'apikey-ok-bg': '161 94% 30%',
      'apikey-ok-hover': '161 94% 25%',
      'apikey-ok-text': '0 0% 100%',
      'apikey-missing-bg': '347 77% 50%',
      'apikey-missing-hover': '347 77% 45%',
      'apikey-missing-text': '0 0% 100%',

      // Microphone Recording Button
      'mic-record-bg': '0 72% 51%',
      'mic-record-icon': '0 0% 100%',
      'mic-record-ring': '0 72% 51%',
      'mic-stt-bg': '0 72% 56%',
      'mic-stt-icon': '0 0% 100%',
      'mic-pulse-outer': '0 72% 51%',
      'mic-pulse-inner': '0 72% 51%',

      // Live & Video Controls
      'live-badge-bg': '0 72% 51%',
      'live-badge-text': '0 0% 100%',
      'live-badge-dot': '0 0% 100%',
      'live-stop-bg': '0 72% 51%',
      'live-stop-hover': '0 72% 45%',
      'live-stop-text': '0 0% 100%',
      'live-stop-icon': '0 0% 100%',
      'vid-stop-bg': '0 72% 56%',
      'vid-stop-hover': '0 72% 48%',
      'vid-stop-text': '0 0% 100%',
      'vid-stop-icon': '0 0% 100%',
      'remove-attach-bg': '0 72% 51%',
      'remove-attach-hover': '0 72% 45%',
      'remove-attach-icon': '0 0% 100%',
      'rec-dot': '0 72% 51%',
      'rec-error-bg': '0 72% 51%',
      'rec-error-text': '0 0% 100%',
      'top-live-active-bg': '0 72% 51%',
      'top-live-active-hover': '0 72% 45%',
      'top-live-active-text': '0 0% 100%',
      'top-live-error-bg': '220 70% 55%',
      'top-live-error-hover': '220 70% 50%',
      'top-live-error-text': '220 30% 20%',
      'overlay-live-error-bg': '220 74% 59%',
      'overlay-live-error-hover': '220 74% 53%',
      'overlay-live-error-text': '210 25% 99%',
    },
  },
];
