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
    groupName: 'Page',
    groupDescription: 'Main app background and text',
    colors: [
      { cssVar: 'background', friendlyName: 'Background', description: 'The overall background of the app' },
      { cssVar: 'foreground', friendlyName: 'Text', description: 'Main text color throughout the app' },
    ],
  },
  {
    groupName: 'Buttons & Accents',
    groupDescription: 'Colors for buttons, links, and highlights',
    colors: [
      { cssVar: 'accent', friendlyName: 'Accent', description: 'Main action buttons and the tutor status badge when speaking' },
      { cssVar: 'accent-foreground', friendlyName: 'Accent Text', description: 'Text on accent-colored buttons' },
      { cssVar: 'primary', friendlyName: 'Primary', description: 'Dark ink used for important elements and controls pill' },
      { cssVar: 'primary-foreground', friendlyName: 'Primary Text', description: 'Text on primary-colored elements' },
      { cssVar: 'secondary', friendlyName: 'Secondary', description: 'Less prominent buttons and the tutor badge when observing' },
      { cssVar: 'secondary-foreground', friendlyName: 'Secondary Text', description: 'Text on secondary elements' },
      { cssVar: 'destructive', friendlyName: 'Warning', description: 'Warning and error highlights' },
      { cssVar: 'destructive-foreground', friendlyName: 'Warning Text', description: 'Text on warning elements' },
    ],
  },
  {
    groupName: 'Tutor Status Badge',
    groupDescription: 'The flag in the top-left showing what the tutor is doing',
    colors: [
      { cssVar: 'status-hold', friendlyName: 'Hold', description: 'Badge color when you pause the tutor (hold mode)' },
      { cssVar: 'status-hold-text', friendlyName: 'Hold Text', description: 'Text on the hold badge' },
    ],
  },
  {
    groupName: 'Cards & Popups',
    groupDescription: 'Chat message cards and popup dialogs',
    colors: [
      { cssVar: 'card', friendlyName: 'Card', description: 'Background of chat messages and cards' },
      { cssVar: 'card-foreground', friendlyName: 'Card Text', description: 'Text inside cards' },
      { cssVar: 'popover', friendlyName: 'Popup', description: 'Background of popup menus and dialogs' },
      { cssVar: 'popover-foreground', friendlyName: 'Popup Text', description: 'Text inside popups' },
    ],
  },
  {
    groupName: 'Notebook Style',
    groupDescription: 'The hand-drawn paper and pencil look',
    colors: [
      { cssVar: 'paper', friendlyName: 'Paper', description: 'Notebook paper background' },
      { cssVar: 'paper-dark', friendlyName: 'Paper Shade', description: 'Darker paper areas like alternate rows' },
      { cssVar: 'pencil', friendlyName: 'Pencil', description: 'Dark graphite pencil color and tutor badge when listening' },
      { cssVar: 'pencil-light', friendlyName: 'Light Pencil', description: 'Lighter pencil for subtle elements' },
      { cssVar: 'pencil-mark', friendlyName: 'Pencil Mark', description: 'Pencil stroke marks and borders' },
      { cssVar: 'sketch-shadow', friendlyName: 'Shadow', description: 'Sketchy shadow effects' },
      { cssVar: 'eraser', friendlyName: 'Eraser', description: 'Eraser-pink for cancel and remove buttons' },
      { cssVar: 'watercolor', friendlyName: 'Watercolor', description: 'Soft watercolor blue wash for accents and busy tasks' },
      { cssVar: 'ink', friendlyName: 'Ink', description: 'Deep ink blue for strong text' },
    ],
  },
  {
    groupName: 'Highlights & Corrections',
    groupDescription: 'Highlighter, red pen marks',
    colors: [
      { cssVar: 'highlight', friendlyName: 'Highlighter', description: 'Yellow highlighter for important words' },
      { cssVar: 'highlight-text', friendlyName: 'Highlight Text', description: 'Text on highlighted areas' },
      { cssVar: 'correction', friendlyName: 'Red Pen', description: 'Red pen marks for corrections and errors' },
    ],
  },
  {
    groupName: 'Subtle & Borders',
    groupDescription: 'Borders, input fields, disabled elements',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'muted', friendlyName: 'Muted', description: 'Background for disabled or subtle elements and the idle badge' },
      { cssVar: 'muted-foreground', friendlyName: 'Muted Text', description: 'Text for less important information' },
      { cssVar: 'border', friendlyName: 'Borders', description: 'Lines between sections and around cards' },
      { cssVar: 'input', friendlyName: 'Input Border', description: 'Border color for text inputs' },
      { cssVar: 'ring', friendlyName: 'Focus Ring', description: 'Glow that appears when an element is focused' },
    ],
  },
  {
    groupName: 'Action Panels',
    groupDescription: 'Confirmation panels for saving, loading, deleting',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'action-load', friendlyName: 'Load', description: 'Panel color when loading/importing chats' },
      { cssVar: 'action-load-text', friendlyName: 'Load Text', description: 'Text in the load panel' },
      { cssVar: 'action-danger', friendlyName: 'Delete', description: 'Panel color when deleting or resetting data' },
      { cssVar: 'action-danger-text', friendlyName: 'Delete Text', description: 'Text in the delete panel' },
      { cssVar: 'action-export', friendlyName: 'Export', description: 'Panel color when exporting a single chat' },
      { cssVar: 'action-export-text', friendlyName: 'Export Text', description: 'Text in the export panel' },
      { cssVar: 'action-combine', friendlyName: 'Combine', description: 'Panel color when merging chats together' },
      { cssVar: 'action-combine-text', friendlyName: 'Combine Text', description: 'Text in the combine panel' },
      { cssVar: 'action-trim', friendlyName: 'Trim', description: 'Panel color when trimming old messages' },
      { cssVar: 'action-trim-text', friendlyName: 'Trim Text', description: 'Text in the trim panel' },
    ],
  },
  {
    groupName: 'Voice Characters',
    groupDescription: 'Identity colors for each tutor voice',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'voice-zephyr', friendlyName: 'Zephyr', description: 'Color for the Zephyr voice (teal/cyan)' },
      { cssVar: 'voice-puck', friendlyName: 'Puck', description: 'Color for the Puck voice (amber/gold)' },
      { cssVar: 'voice-charon', friendlyName: 'Charon', description: 'Color for the Charon voice (gray)' },
      { cssVar: 'voice-kore', friendlyName: 'Kore', description: 'Color for the Kore voice (blue)' },
      { cssVar: 'voice-fenrir', friendlyName: 'Fenrir', description: 'Color for the Fenrir voice (red)' },
    ],
  },
  {
    groupName: 'API Key',
    groupDescription: 'The API key button next to traffic logs',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'api-key-valid', friendlyName: 'Key Set', description: 'Button color when an API key is configured' },
      { cssVar: 'api-key-valid-text', friendlyName: 'Key Set Text', description: 'Text when an API key is configured' },
      { cssVar: 'api-key-missing', friendlyName: 'Key Missing', description: 'Button color when no API key is set' },
      { cssVar: 'api-key-missing-text', friendlyName: 'Key Missing Text', description: 'Text when no API key is set' },
    ],
  },
  {
    groupName: 'Recording',
    groupDescription: 'Microphone and video recording indicators',
    collapsedByDefault: true,
    colors: [
      { cssVar: 'recording', friendlyName: 'Recording', description: 'Color of the recording dot and mic button when active' },
      { cssVar: 'recording-text', friendlyName: 'Recording Text', description: 'Text and icons on recording indicators' },
    ],
  },
];

export const ALL_COLOR_VARS: ColorVariable[] = COLOR_GROUPS.flatMap(g => g.colors);
