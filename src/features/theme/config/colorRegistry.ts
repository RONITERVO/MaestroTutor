// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface ColorVariable {
  cssVar: string;
  friendlyName: string;
  /** Short description of what this color affects, for non-technical users. */
  description?: string;
}

export interface ColorGroup {
  groupName: string;
  /** Short explanation of this group shown below the group title. */
  groupDescription?: string;
  colors: ColorVariable[];
  /** If true, group starts collapsed in the panel. */
  collapsedByDefault?: boolean;
}

export const COLOR_GROUPS: ColorGroup[] = [
  {
    groupName: 'App Canvas',
    groupDescription: 'The full-screen paper and main writing color',
    colors: [
      { cssVar: 'background', friendlyName: 'Screen Background', description: 'Main app background behind everything' },
      { cssVar: 'foreground', friendlyName: 'Main Text', description: 'Default text color used in most places' },
      { cssVar: 'paper', friendlyName: 'Paper Surface', description: 'Notebook paper areas in the main content' },
      { cssVar: 'paper-dark', friendlyName: 'Paper Shade', description: 'Darker paper stripes and paper depth' },
      { cssVar: 'ink', friendlyName: 'Ink', description: 'Strong deep-ink text and marks' },
    ],
  },
  {
    groupName: 'Cards and Popups',
    groupDescription: 'Message cards, dialogs, and secondary text',
    colors: [
      { cssVar: 'card', friendlyName: 'Card Background', description: 'Background of assistant/user message bubbles and cards' },
      { cssVar: 'card-foreground', friendlyName: 'Card Text', description: 'Text inside cards and bubbles' },
      { cssVar: 'popover', friendlyName: 'Popup Background', description: 'Popup menu and dialog background color' },
      { cssVar: 'popover-foreground', friendlyName: 'Popup Text', description: 'Text inside popups and dialogs' },
      { cssVar: 'muted', friendlyName: 'Soft Surface', description: 'Soft backgrounds for low-priority sections' },
      { cssVar: 'muted-foreground', friendlyName: 'Soft Text', description: 'Low-emphasis helper text' },
    ],
  },
  {
    groupName: 'Buttons and Actions',
    groupDescription: 'Main button colors for normal and danger actions',
    colors: [
      { cssVar: 'primary', friendlyName: 'Primary Button', description: 'Main dark button background' },
      { cssVar: 'primary-foreground', friendlyName: 'Primary Button Text', description: 'Text and icons on primary buttons' },
      { cssVar: 'secondary', friendlyName: 'Secondary Button', description: 'Secondary button background' },
      { cssVar: 'secondary-foreground', friendlyName: 'Secondary Button Text', description: 'Text and icons on secondary buttons' },
      { cssVar: 'accent', friendlyName: 'Accent Button', description: 'Bright action buttons and highlights' },
      { cssVar: 'accent-foreground', friendlyName: 'Accent Button Text', description: 'Text and icons on accent buttons' },
      { cssVar: 'destructive', friendlyName: 'Danger Button', description: 'Danger or delete action color' },
      { cssVar: 'destructive-foreground', friendlyName: 'Danger Button Text', description: 'Text on danger buttons and alerts' },
      { cssVar: 'eraser', friendlyName: 'Eraser Action', description: 'Pink eraser-style actions like remove/cancel' },
    ],
  },
  {
    groupName: 'Borders and Focus',
    groupDescription: 'Outlines and focus glow',
    colors: [
      { cssVar: 'border', friendlyName: 'Default Border', description: 'Most borders and separator lines' },
      { cssVar: 'input', friendlyName: 'Input Border', description: 'Text input outlines' },
      { cssVar: 'ring', friendlyName: 'Focus Glow', description: 'Glow shown when controls are focused' },
      { cssVar: 'pencil-light', friendlyName: 'Light Pencil Line', description: 'Thin sketchy outlines and subtle notebook lines' },
    ],
  },
  {
    groupName: 'Status Flag: Hold',
    groupDescription: 'Top-left flag when you pause maestro',
    colors: [
      { cssVar: 'status-hold-bg', friendlyName: 'Hold Background', description: 'Flag background in hold mode' },
      { cssVar: 'status-hold-border', friendlyName: 'Hold Border', description: 'Flag border in hold mode' },
      { cssVar: 'status-hold-text', friendlyName: 'Hold Text', description: 'Flag icon/text in hold mode' },
    ],
  },
  {
    groupName: 'Status Flag: Speaking and Typing',
    groupDescription: 'Top-left flag while maestro is actively responding',
    colors: [
      { cssVar: 'status-speaking-bg', friendlyName: 'Speaking Background', description: 'Flag background while maestro is speaking' },
      { cssVar: 'status-speaking-border', friendlyName: 'Speaking Border', description: 'Flag border while maestro is speaking' },
      { cssVar: 'status-speaking-text', friendlyName: 'Speaking Text', description: 'Flag icon/text while maestro is speaking' },
      { cssVar: 'status-typing-bg', friendlyName: 'Typing Background', description: 'Flag background while maestro is typing' },
      { cssVar: 'status-typing-border', friendlyName: 'Typing Border', description: 'Flag border while maestro is typing' },
      { cssVar: 'status-typing-text', friendlyName: 'Typing Text', description: 'Flag icon/text while maestro is typing' },
    ],
  },
  {
    groupName: 'Status Flag: Listening, Observing, Idle',
    groupDescription: 'Top-left flag while maestro is listening for your voice, watching, or idle',
    colors: [
      { cssVar: 'status-listening-bg', friendlyName: 'Listening Background', description: 'Flag background while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-listening-border', friendlyName: 'Listening Border', description: 'Flag border while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-listening-text', friendlyName: 'Listening Text', description: 'Flag icon/text while maestro is actively listening for your voice (microphone on)' },
      { cssVar: 'status-observing-bg', friendlyName: 'Observing Background', description: 'Flag background while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-border', friendlyName: 'Observing Border', description: 'Flag border while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-text', friendlyName: 'Observing Text', description: 'Flag icon/text while maestro is resting (💤) or quietly watching the camera' },
      { cssVar: 'status-observing-high-bg', friendlyName: 'About To Engage Background', description: 'Flag background when maestro is about to engage' },
      { cssVar: 'status-observing-high-border', friendlyName: 'About To Engage Border', description: 'Flag border when maestro is about to engage' },
      { cssVar: 'status-observing-high-text', friendlyName: 'About To Engage Text', description: 'Flag icon/text when maestro is about to engage' },
      { cssVar: 'status-idle-bg', friendlyName: 'Idle Background', description: 'Flag background when nothing is running' },
      { cssVar: 'status-idle-border', friendlyName: 'Idle Border', description: 'Flag border when nothing is running' },
      { cssVar: 'status-idle-text', friendlyName: 'Idle Text', description: 'Flag icon/text when nothing is running' },
      { cssVar: 'status-busy-bg', friendlyName: 'Busy Background', description: 'Flag background tint while background tasks are active' },
      { cssVar: 'status-busy-border', friendlyName: 'Busy Border', description: 'Flag border tint while background tasks are active' },
      { cssVar: 'status-busy-text', friendlyName: 'Busy Text', description: 'Flag icon/text while background tasks are active' },
    ],
  },
  {
    groupName: 'API Key Button',
    groupDescription: 'Top-right button that shows key present/missing',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'api-key-valid-bg', friendlyName: 'Key Present Background', description: 'API key button background when key exists' },
      { cssVar: 'api-key-valid-hover-bg', friendlyName: 'Key Present Hover', description: 'API key button hover color when key exists' },
      { cssVar: 'api-key-valid-text', friendlyName: 'Key Present Text', description: 'API key button text/icon when key exists' },
      { cssVar: 'api-key-missing-bg', friendlyName: 'Key Missing Background', description: 'API key button background when key is missing' },
      { cssVar: 'api-key-missing-hover-bg', friendlyName: 'Key Missing Hover', description: 'API key button hover color when key is missing' },
      { cssVar: 'api-key-missing-text', friendlyName: 'Key Missing Text', description: 'API key button text/icon when key is missing' },
    ],
  },
  {
    groupName: 'Microphone Recording Button',
    groupDescription: 'Hold-to-record and listening mic states',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'recording-mic-armed-bg', friendlyName: 'Mic Recording Background', description: 'Mic button while hold-to-record is active' },
      { cssVar: 'recording-mic-armed-text', friendlyName: 'Mic Recording Icon', description: 'Mic icon while hold-to-record is active' },
      { cssVar: 'recording-mic-armed-ring', friendlyName: 'Mic Recording Ring', description: 'Ring around mic while hold-to-record is active' },
      { cssVar: 'recording-mic-listening-bg', friendlyName: 'Mic Listening Background', description: 'Mic button while speech-to-text is listening' },
      { cssVar: 'recording-mic-listening-text', friendlyName: 'Mic Listening Icon', description: 'Mic icon while speech-to-text is listening' },
      { cssVar: 'recording-mic-pulse-outer', friendlyName: 'Mic Pulse Outer', description: 'Outer pulse ring while hold-to-record is active' },
      { cssVar: 'recording-mic-pulse-inner', friendlyName: 'Mic Pulse Inner', description: 'Inner pulse ring while hold-to-record is active' },
    ],
  },
  {
    groupName: 'Live and Attachment Recording Controls',
    groupDescription: 'Live chip, stop squares, remove buttons, and inline recording errors',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'recording-live-chip-bg', friendlyName: 'Live Chip Background', description: 'The small LIVE badge background in live preview' },
      { cssVar: 'recording-live-chip-text', friendlyName: 'Live Chip Text', description: 'Text color inside the LIVE badge' },
      { cssVar: 'recording-live-chip-dot', friendlyName: 'Live Chip Dot', description: 'Blinking dot inside the LIVE badge' },
      { cssVar: 'recording-live-stop-bg', friendlyName: 'Live Stop Button Background', description: 'Round stop button background while live session is active' },
      { cssVar: 'recording-live-stop-hover-bg', friendlyName: 'Live Stop Button Hover', description: 'Round stop button hover color while live session is active' },
      { cssVar: 'recording-live-stop-text', friendlyName: 'Live Stop Button Foreground', description: 'Foreground color on live-session stop button' },
      { cssVar: 'recording-live-stop-icon', friendlyName: 'Live Stop Square Icon', description: 'Square stop icon for live-session stop button' },
      { cssVar: 'recording-local-stop-bg', friendlyName: 'Local Stop Button Background', description: 'Round stop button background while local video recording is active' },
      { cssVar: 'recording-local-stop-hover-bg', friendlyName: 'Local Stop Button Hover', description: 'Round stop button hover while local video recording is active' },
      { cssVar: 'recording-local-stop-text', friendlyName: 'Local Stop Button Foreground', description: 'Foreground color on local-video stop button' },
      { cssVar: 'recording-local-stop-icon', friendlyName: 'Local Stop Square Icon', description: 'Square stop icon for local-video recording stop button' },
      { cssVar: 'recording-remove-bg', friendlyName: 'Remove Attachment Background', description: 'Round X button background for removing an attachment' },
      { cssVar: 'recording-remove-hover-bg', friendlyName: 'Remove Attachment Hover', description: 'Round X button hover color for removing an attachment' },
      { cssVar: 'recording-remove-text', friendlyName: 'Remove Attachment Icon', description: 'X icon color on remove-attachment button' },
      { cssVar: 'recording-indicator-dot', friendlyName: 'REC Dot', description: 'Tiny REC indicator dot while local recording is active' },
      { cssVar: 'recording-inline-error-bg', friendlyName: 'Inline Recording Error Background', description: 'Inline error background related to recording/live issues' },
      { cssVar: 'recording-inline-error-text', friendlyName: 'Inline Recording Error Text', description: 'Inline error text related to recording/live issues' },
      { cssVar: 'live-session-button-active-bg', friendlyName: 'Live Session Stop Button Background', description: 'Stop button background shown in the camera area while a live session is running' },
      { cssVar: 'live-session-button-active-hover-bg', friendlyName: 'Live Session Stop Button Hover', description: 'Stop button hover color in the camera area while a live session is running' },
      { cssVar: 'live-session-button-active-text', friendlyName: 'Live Session Stop Button Text', description: 'Stop button text color in the camera area while a live session is running' },
      { cssVar: 'live-session-button-error-bg', friendlyName: 'Live Session Retry Button Background', description: 'Retry button background shown in the camera area after a live session connection error' },
      { cssVar: 'live-session-button-error-hover-bg', friendlyName: 'Live Session Retry Button Hover', description: 'Retry button hover color in the camera area after a live session connection error' },
      { cssVar: 'live-session-button-error-text', friendlyName: 'Live Session Retry Button Text', description: 'Retry button text color in the camera area after a live session connection error' },
      { cssVar: 'live-overlay-button-error-bg', friendlyName: 'Overlay Live Button Error Background', description: 'Overlay LIVE button background when retry is needed' },
      { cssVar: 'live-overlay-button-error-hover-bg', friendlyName: 'Overlay Live Button Error Hover', description: 'Overlay LIVE button hover color when retry is needed' },
      { cssVar: 'live-overlay-button-error-text', friendlyName: 'Overlay Live Button Error Text', description: 'Overlay LIVE button text when retry is needed' },
    ],
  },
  {
    groupName: 'Action Confirmation Panels',
    groupDescription: 'Panels for load, delete, export, combine, and trim actions',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'action-load', friendlyName: 'Load Action Color', description: 'Accent color for load/import confirmation (button, input tint, and label)' },
      { cssVar: 'action-load-text', friendlyName: 'Load Action Text', description: 'Text color on the load action confirm button and inside the input field' },
      { cssVar: 'action-danger', friendlyName: 'Delete Action Color', description: 'Accent color for delete/reset confirmation (button, input tint, and label)' },
      { cssVar: 'action-danger-text', friendlyName: 'Delete Action Text', description: 'Text color on the delete action confirm button and inside the input field' },
      { cssVar: 'action-export', friendlyName: 'Export Action Color', description: 'Accent color for export confirmation (button, input tint, and label)' },
      { cssVar: 'action-export-text', friendlyName: 'Export Action Text', description: 'Text color on the export action confirm button and inside the input field' },
      { cssVar: 'action-combine', friendlyName: 'Combine Action Color', description: 'Accent color for combine/merge confirmation (button, input tint, and label)' },
      { cssVar: 'action-combine-text', friendlyName: 'Combine Action Text', description: 'Text color on the combine action confirm button and inside the input field' },
      { cssVar: 'action-trim', friendlyName: 'Trim Action Color', description: 'Accent color for trim confirmation (button, input tint, and label)' },
      { cssVar: 'action-trim-text', friendlyName: 'Trim Action Text', description: 'Text color on the trim action confirm button and inside the input field' },
      { cssVar: 'action-danger-shortcut-hover-bg', friendlyName: 'Delete Shortcut Hover Background', description: 'Hover background of the small delete shortcut button in session controls' },
      { cssVar: 'action-danger-shortcut-hover-text', friendlyName: 'Delete Shortcut Hover Icon', description: 'Hover icon color of the small delete shortcut button in session controls' },
      { cssVar: 'action-trim-shortcut-hover-bg', friendlyName: 'Trim Shortcut Hover Background', description: 'Hover background of the small trim shortcut button in session controls' },
      { cssVar: 'action-trim-shortcut-hover-text', friendlyName: 'Trim Shortcut Hover Icon', description: 'Hover icon color of the small trim shortcut button in session controls' },
    ],
  },
  {
    groupName: 'Voice Identity',
    groupDescription: 'Color ring identity for each voice character',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'voice-zephyr', friendlyName: 'Voice: Zephyr', description: 'Identity color for Zephyr voice' },
      { cssVar: 'voice-puck', friendlyName: 'Voice: Puck', description: 'Identity color for Puck voice' },
      { cssVar: 'voice-charon', friendlyName: 'Voice: Charon', description: 'Identity color for Charon voice' },
      { cssVar: 'voice-kore', friendlyName: 'Voice: Kore', description: 'Identity color for Kore voice' },
      { cssVar: 'voice-fenrir', friendlyName: 'Voice: Fenrir', description: 'Identity color for Fenrir voice' },
    ],
  },
  {
    groupName: 'Notebook Style Details',
    groupDescription: 'Sketch lines, marker highlights, and correction marks',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'pencil', friendlyName: 'Pencil', description: 'Dark sketch strokes and strong notebook outlines' },
      { cssVar: 'pencil-mark', friendlyName: 'Pencil Mark', description: 'Emphasized pencil strokes and markups' },
      { cssVar: 'sketch-shadow', friendlyName: 'Sketch Shadow', description: 'Shadows in hand-drawn elements' },
      { cssVar: 'watercolor', friendlyName: 'Watercolor Wash', description: 'Soft watercolor accent wash' },
      { cssVar: 'highlight', friendlyName: 'Highlighter', description: 'Highlight marker background for active words' },
      { cssVar: 'highlight-text', friendlyName: 'Highlighter Text', description: 'Text color when highlighted' },
      { cssVar: 'correction', friendlyName: 'Correction Pen', description: 'Correction/error red pen color' },
    ],
  },
];

export const ALL_COLOR_VARS: ColorVariable[] = COLOR_GROUPS.flatMap(g => g.colors);
