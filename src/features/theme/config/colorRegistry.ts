// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface ColorVariable {
  cssVar: string;
  friendlyName: string;
}

export interface ColorGroup {
  groupName: string;
  colors: ColorVariable[];
}

export const COLOR_GROUPS: ColorGroup[] = [
  {
    groupName: 'Page',
    colors: [
      { cssVar: 'background', friendlyName: 'Background' },
      { cssVar: 'foreground', friendlyName: 'Text' },
    ],
  },
  {
    groupName: 'Cards & Popups',
    colors: [
      { cssVar: 'card', friendlyName: 'Card' },
      { cssVar: 'card-foreground', friendlyName: 'Card Text' },
      { cssVar: 'popover', friendlyName: 'Popup' },
      { cssVar: 'popover-foreground', friendlyName: 'Popup Text' },
    ],
  },
  {
    groupName: 'Buttons & Actions',
    colors: [
      { cssVar: 'primary', friendlyName: 'Primary' },
      { cssVar: 'primary-foreground', friendlyName: 'Primary Text' },
      { cssVar: 'secondary', friendlyName: 'Secondary' },
      { cssVar: 'secondary-foreground', friendlyName: 'Secondary Text' },
      { cssVar: 'accent', friendlyName: 'Accent' },
      { cssVar: 'accent-foreground', friendlyName: 'Accent Text' },
      { cssVar: 'destructive', friendlyName: 'Warning' },
      { cssVar: 'destructive-foreground', friendlyName: 'Warning Text' },
    ],
  },
  {
    groupName: 'Subtle & Muted',
    colors: [
      { cssVar: 'muted', friendlyName: 'Muted' },
      { cssVar: 'muted-foreground', friendlyName: 'Muted Text' },
      { cssVar: 'border', friendlyName: 'Borders' },
      { cssVar: 'input', friendlyName: 'Inputs' },
      { cssVar: 'ring', friendlyName: 'Focus Ring' },
    ],
  },
  {
    groupName: 'Notebook',
    colors: [
      { cssVar: 'paper', friendlyName: 'Paper' },
      { cssVar: 'paper-dark', friendlyName: 'Paper Dark' },
      { cssVar: 'pencil', friendlyName: 'Pencil' },
      { cssVar: 'pencil-light', friendlyName: 'Pencil Light' },
      { cssVar: 'pencil-mark', friendlyName: 'Pencil Mark' },
      { cssVar: 'sketch-shadow', friendlyName: 'Shadow' },
      { cssVar: 'eraser', friendlyName: 'Eraser' },
      { cssVar: 'watercolor', friendlyName: 'Watercolor' },
      { cssVar: 'ink', friendlyName: 'Ink' },
    ],
  },
  {
    groupName: 'Highlights',
    colors: [
      { cssVar: 'highlight', friendlyName: 'Highlighter' },
      { cssVar: 'highlight-text', friendlyName: 'Highlight Text' },
      { cssVar: 'correction', friendlyName: 'Red Pen' },
    ],
  },
];

export const ALL_COLOR_VARS: ColorVariable[] = COLOR_GROUPS.flatMap(g => g.colors);
